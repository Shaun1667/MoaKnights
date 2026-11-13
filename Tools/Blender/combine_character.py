# MoaKnights — 몸 · 겉옷 메시를 하나로 합치고 폴리곤을 줄인다.
#
# 실행 (Blender 4.3, 화면 없이):
#   blender -b -P Tools/Blender/combine_character.py -- <body.glb> <outfit.glb> <body.png> <outfit.png> <out_dir> <name> [target_tris] [height_m]
#   겉옷 없이 한 덩어리(적 등)는 outfit.glb와 outfit.png 자리에 - 를 넣는다. 그러면 3 · 4단계를 건너뛰고 재질은 body 하나다.
#
# 순서
#   1. 두 원본 그림(같은 구도)에서 흰 바탕이 아닌 영역의 위치를 잰다.
#   2. 두 GLB를 불러와 메시마다 하나로 합치고 변환을 적용한 뒤, 텍스처 이음매에서 끊긴 꼭짓점을 이어 붙인다 (weld).
#   3. 겉옷을 그림 비율대로 몸에 맞춘다: 몸의 "그림 1픽셀 = 몇 m"로 겉옷 높이 · 아래 · 가로 위치를 정하고, 깊이는 몸 가운데에 맞춘다.
#   4. 겉옷에 가려 안 보이는 몸 면과 겉옷을 뚫고 나온 몸 면을 지운다 (면 법선 앞뒤로 가까이 겉옷이 있으면).
#      (예전 회색 타이즈 몸 그림은 SKIN_RECOLOR=1로 회색을 피부색으로 바꿨다. 지금 몸 그림은 속옷 차림이라 끔.)
#   5. 하나로 합치고 Decimate로 목표 삼각형 수까지 줄인 뒤 삼각형화한다.
#   6. 앞이 Unity +X를 보게 돌리고 높이를 맞춰 FBX와 텍스처 PNG, 확인용 렌더 PNG를 낸다.

import os
import sys

import bpy
import bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree

argv = sys.argv[sys.argv.index("--") + 1:]
body_glb, outfit_glb, body_png, outfit_png, out_dir, name = argv[:6]
target_tris = int(argv[6]) if len(argv) > 6 else 3000
height_m = float(argv[7]) if len(argv) > 7 else 1.0
hidden_distance_ratio = 0.06   # 키에 대한 비율: 면 바깥쪽 이 거리 안에 겉옷이 있으면 가려진 몸 면
poke_distance_ratio = float(os.environ.get("POKE_RATIO", "0.05"))   # 키에 대한 비율: 면 안쪽 이 거리 안에 겉옷이 있으면 겉옷을 뚫고 나온 몸 면 (근거리 melee_01은 0.025로 만듦)
outfit_push_ratio = float(os.environ.get("OUTFIT_PUSH", "0.01"))   # 키에 대한 비율: 겉옷을 법선 방향으로 이만큼 부풀려 몸을 덮게 한다 (melee_01은 0)
lower_mask_ratio = float(os.environ.get("LOWER_MASK", "0.38"))   # 키에 대한 비율: 아래에서 이 높이까지(다리)는 겉옷 그림에서 옷이 덮은 자리의 몸 면을 모두 지운다 (0이면 끔)
torso_poke_ratio = float(os.environ.get("TORSO_POKE", "0"))   # 키에 대한 비율: 몸통 구역(다리 위 ~ 턱 아래, 가운데 폭)에서만 쓰는 더 깊은 "뚫고 나옴" 거리. 몸통 속옷이 겉옷 등판보다 두꺼워 뒤로 나올 때 (0이면 끔)
TORSO_TOP_RATIO = 0.68     # 키에 대한 비율: 턱 아래 높이 (2.5 ~ 3등신 기준)
TORSO_HALF_WIDTH_RATIO = 0.15   # 키에 대한 비율: 몸통 반폭 (어깨까지. 아래팔 · 머리카락 제외)
COLLAR_FRONT_TOP_RATIO = 0.705   # 키에 대한 비율: 앞쪽 옷깃 높이 (입 아래까지만)
NECK_BACK_TOP_RATIO = 0.76     # 키에 대한 비율: 목 뒤 옷깃 높이 (뒤쪽 절반에서만 턱보다 높이 본다)
depth_align = os.environ.get("DEPTH_ALIGN", "box")   # 겉옷 깊이 맞추기: box = 상자 가운데(예전), front = 가슴 앞면 (방패 · 망토 · 긴 무기가 있는 캐릭터)
CHEST_BOTTOM_RATIO = 0.48   # 키에 대한 비율: 가슴 높이 구간 (2.5등신)
CHEST_TOP_RATIO = 0.60
CHEST_HALF_WIDTH_RATIO = 0.07   # 가운데 기둥 반폭
CHEST_GAP_RATIO = 0.012     # 겉옷 앞면이 몸 앞면보다 이만큼 앞
JAW_BOTTOM_RATIO = 0.645   # 키에 대한 비율: 턱 밑 그림자를 찾는 높이 (속옷 목선 위 ~ 입 아래)
JAW_TOP_RATIO = 0.74
JAW_SHADE = 0.86           # 턱 밑 그림자 자리에 칠하는 피부색의 밝기 (1 = 그늘 없음)
os.makedirs(out_dir, exist_ok=True)


def log(*parts):
    print("[combine]", *parts, flush=True)


def image_box(path, threshold=0.92):
    """흰 바탕이 아닌 픽셀의 범위 (x0, x1, y0, y1), 그림 좌표(위가 0)."""
    img = bpy.data.images.load(path)
    w, h = img.size
    px = img.pixels[:]
    xs, ys = [], []
    step = 2
    for y in range(0, h, step):
        row = y * w * 4
        for x in range(0, w, step):
            i = row + x * 4
            if min(px[i], px[i + 1], px[i + 2]) < threshold:
                xs.append(x)
                ys.append(h - 1 - y)   # Blender 픽셀은 아래가 0
    bpy.data.images.remove(img)
    return min(xs), max(xs), min(ys), max(ys)


def image_mask(path, threshold=0.92):
    """흰 바탕이 아닌 픽셀이 참인 표 [그림 y(위가 0)][x]."""
    import numpy
    img = bpy.data.images.load(path)
    w, h = img.size
    px = numpy.array(img.pixels[:], dtype=numpy.float32).reshape(h, w, 4)[::-1]
    bpy.data.images.remove(img)
    return px[:, :, :3].min(axis=2) < threshold


def import_mesh(path, label):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    meshes = [o for o in new if o.type == "MESH"]
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    # 부모(빈 오브젝트)에서 떼고 변환 적용
    mat = obj.matrix_world.copy()
    obj.parent = None
    obj.matrix_world = mat
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for o in new:
        if o is not obj:
            bpy.data.objects.remove(o, do_unlink=True)
    obj.name = label
    weld(obj)
    return obj


def weld(obj):
    """Varco GLB는 텍스처 이음매마다 꼭짓점이 끊겨 있다 (모서리 만여 개가 열림). 그대로 줄이면 이음매마다 찢어지고
    법선 방향으로 부풀릴 때 틈이 벌어진다. 같은 자리의 꼭짓점을 이어 붙인다 — UV는 면 모서리마다 따로라 그대로 남는다."""
    me = obj.data
    bm = bmesh.new()
    bm.from_mesh(me)
    before_verts = len(bm.verts)
    open_before = sum(1 for e in bm.edges if e.is_boundary)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    open_after = sum(1 for e in bm.edges if e.is_boundary)
    bm.normal_update()
    bm.to_mesh(me)
    after_verts = len(bm.verts)
    bm.free()
    # 불러온 법선(이음매에서 갈라짐) 대신 이어 붙인 메시의 부드러운 법선을 쓴다
    if me.has_custom_normals:
        bpy.context.view_layer.objects.active = obj
        bpy.ops.mesh.customdata_custom_splitnormals_clear()
    for poly in me.polygons:
        poly.use_smooth = True
    me.update()
    log("weld", obj.name, "verts", before_verts, "->", after_verts, "open edges", open_before, "->", open_after)


def world_box(obj):
    vs = [obj.matrix_world @ v.co for v in obj.data.vertices]
    lo = Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs)))
    hi = Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs)))
    return lo, hi


def tri_count(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


# 빈 장면에서 시작
bpy.ops.wm.read_factory_settings(use_empty=True)

single = outfit_glb == "-"
body = import_mesh(body_glb, "Body")
if not single:
    bb = image_box(body_png)
    ob = image_box(outfit_png)
    log("image body", bb, "outfit", ob)
    outfit = import_mesh(outfit_glb, "Outfit")


def fit_outfit_and_hide_body():
    global outfit
    blo, bhi = world_box(body)
    olo, ohi = world_box(outfit)
    log("mesh body", tuple(blo), tuple(bhi), "outfit", tuple(olo), tuple(ohi))

    # 몸 기준 그림 1픽셀의 길이 (높이로 잼). 가로 비율은 확인용으로만 찍는다.
    body_px_h = bb[3] - bb[2]
    m_per_px = (bhi.z - blo.z) / body_px_h
    log("m/px from height", m_per_px, "from width", (bhi.x - blo.x) / (bb[1] - bb[0]))

    outfit_px_h = ob[3] - ob[2]
    scale = outfit_px_h * m_per_px / (ohi.z - olo.z)
    outfit.scale = (scale, scale, scale)
    bpy.context.view_layer.objects.active = outfit
    bpy.ops.object.select_all(action="DESELECT")
    outfit.select_set(True)
    bpy.ops.object.transform_apply(scale=True)
    olo, ohi = world_box(outfit)

    # 그림 좌표: 아래쪽 끝(y 최대)과 가로 가운데를 맞춘다. 그림 오른쪽 = +X (앞에서 본 모습).
    target_bottom = blo.z + (bb[3] - ob[3]) * m_per_px
    body_cx_px = (bb[0] + bb[1]) / 2
    outfit_cx_px = (ob[0] + ob[1]) / 2
    target_cx = (blo.x + bhi.x) / 2 + (outfit_cx_px - body_cx_px) * m_per_px
    target_cy = (blo.y + bhi.y) / 2
    shift = Vector((target_cx - (olo.x + ohi.x) / 2, target_cy - (olo.y + ohi.y) / 2, target_bottom - olo.z))
    if depth_align == "front":
        # 방패 · 망토 · 무기가 앞뒤로 튀어나오면 상자 가운데가 몸통에서 벗어나 겉옷이 통째로 뒤(앞)로 밀린다.
        # 가슴 높이 · 가운데 기둥에서 몸의 앞면과 겉옷의 앞면을 재서, 겉옷 앞면이 몸 앞면 바로 앞에 오게 한다 (앞이 -Y).
        height = bhi.z - blo.z
        z0, z1 = blo.z + height * CHEST_BOTTOM_RATIO, blo.z + height * CHEST_TOP_RATIO
        half = height * CHEST_HALF_WIDTH_RATIO
        body_cx_world = (blo.x + bhi.x) / 2

        def front_of(obj, dx, dz):
            ys = sorted(v.co.y for v in obj.data.vertices if z0 < v.co.z + dz < z1 and abs(v.co.x + dx - body_cx_world) < half)
            return ys[int(len(ys) * 0.03)] if len(ys) >= 20 else None

        body_front = front_of(body, 0.0, 0.0)
        outfit_front = front_of(outfit, shift.x, shift.z)
        if body_front is not None and outfit_front is not None:
            log("depth align front: body", body_front, "outfit", outfit_front, "box shift", shift.y, "-> front shift", body_front - height * CHEST_GAP_RATIO - outfit_front)
            shift.y = body_front - height * CHEST_GAP_RATIO - outfit_front
    outfit.location += shift
    bpy.ops.object.transform_apply(location=True)
    olo, ohi = world_box(outfit)
    log("outfit fitted", tuple(olo), tuple(ohi), "scale", scale)

    if outfit_push_ratio > 0:
        push = (bhi.z - blo.z) * outfit_push_ratio
        for v in outfit.data.vertices:
            v.co += v.normal * push
        outfit.data.update()
        log("outfit pushed", push)

    # 가려진 몸 면 지우기
    deps = bpy.context.evaluated_depsgraph_get()
    tree = BVHTree.FromObject(outfit, deps)
    limit = (bhi.z - blo.z) * hidden_distance_ratio
    poke = (bhi.z - blo.z) * poke_distance_ratio
    # 다리: 몸 그림과 겉옷 그림의 다리 자세가 조금 달라 바지 · 스타킹 옆으로 몸 다리가 비친다.
    # 아래쪽(다리 높이)에서는 앞에서 본 자리가 겉옷 그림의 옷 픽셀이면 그 몸 면을 지운다. 옷이 없는 맨살 자리(흰 픽셀)는 남는다.
    mask = image_mask(outfit_png) if lower_mask_ratio > 0 else None
    lower_top = blo.z + (bhi.z - blo.z) * lower_mask_ratio
    body_cx = (blo.x + bhi.x) / 2

    torso_top = blo.z + (bhi.z - blo.z) * TORSO_TOP_RATIO
    torso_half = (bhi.z - blo.z) * TORSO_HALF_WIDTH_RATIO
    torso_poke = (bhi.z - blo.z) * torso_poke_ratio
    global body_low_z, body_height
    body_low_z, body_height = blo.z, bhi.z - blo.z

    def poke_of(c):
        in_torso = torso_poke > 0 and lower_top < c.z < torso_top and abs(c.x - body_cx) < torso_half
        return max(poke, torso_poke) if in_torso else poke

    # 얼굴: 턱 아래를 향한 면은 옷깃과 가까워 "가려진 면"으로 잘못 지워져 턱이 잘린다. 턱 높이부터 위, 앞쪽 절반의 몸 면은 지우지 않는다.
    body_cy = (blo.y + bhi.y) / 2

    def is_face(c):
        return c.z >= torso_top and c.y < body_cy and abs(c.x - body_cx) < torso_half

    def under_outfit_pixel(c):
        if mask is None or c.z > lower_top:
            return False
        x = int(round(body_cx_px + (c.x - body_cx) / m_per_px))
        y = int(round(bb[3] - (c.z - blo.z) / m_per_px))
        return 0 <= y < mask.shape[0] and 0 <= x < mask.shape[1] and bool(mask[y, x])

    bm = bmesh.new()
    bm.from_mesh(body.data)
    hidden = []
    masked = 0
    for f in bm.faces:
        c = f.calc_center_median()
        n = f.normal
        if is_face(c):
            continue
        if tree.ray_cast(c + n * 1e-4, n, limit)[0] is not None or tree.ray_cast(c - n * 1e-4, -n, poke_of(c))[0] is not None:
            hidden.append(f)
        elif under_outfit_pixel(c):
            hidden.append(f)
            masked += 1
    log("body faces under outfit pixels (legs)", masked)
    before_faces = len(bm.faces)
    bmesh.ops.delete(bm, geom=hidden, context="FACES")

    # 다리 높이에 떨어져 남은 작은 몸 조각(지우고 남은 부스러기)을 지운다. 맨살 다리처럼 큰 덩어리는 남는다.
    if mask is not None:
        bm.faces.index_update()   # 지운 뒤 번호가 어긋나므로 다시 매긴다
        seen = set()
        crumbs = []
        crumb_limit = max(1, before_faces // 100)
        for start in bm.faces:
            if start.index in seen:
                continue
            island = [start]
            seen.add(start.index)
            i = 0
            while i < len(island):
                for e in island[i].edges:
                    for g in e.link_faces:
                        if g.index not in seen:
                            seen.add(g.index)
                            island.append(g)
                i += 1
            if len(island) < crumb_limit and all(g.calc_center_median().z <= lower_top for g in island):
                crumbs.extend(island)
        bmesh.ops.delete(bm, geom=crumbs, context="FACES")
        log("body crumbs removed", len(crumbs))
    bm.to_mesh(body.data)
    bm.free()
    log("body faces", before_faces, "-> hidden removed", len(hidden))


# 몸 바탕색(회색 바탕옷) → 피부색. 채도가 낮고 밝은 픽셀만 바꾸고, 피부색은 같은 그림의 살색 픽셀 평균.
import numpy as np


def skin_recolor(obj):
    for mat in obj.data.materials:
        for node in mat.node_tree.nodes:
            if node.type != "TEX_IMAGE" or node.image is None:
                continue
            if not any(l.to_socket.name == "Base Color" for o in node.outputs for l in o.links):
                continue
            img = node.image
            px = np.array(img.pixels[:], dtype=np.float32).reshape(-1, 4)
            rgb = px[:, :3]
            mx = rgb.max(axis=1)
            mn = rgb.min(axis=1)
            sat = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1e-6), 0)
            suit = (sat < 0.10) & (mx > 0.45)
            skin_mask = (rgb[:, 0] > rgb[:, 1] * 1.15) & (rgb[:, 1] > rgb[:, 2] * 1.05) & (mx > 0.55) & (sat > 0.2) & (sat < 0.6)
            skin = rgb[skin_mask].mean(axis=0) if skin_mask.any() else np.array([0.9, 0.6, 0.45])
            # 회색의 밝기 차이(주름 · 그늘)를 살려 피부색에 곱한다
            shade = (mx[suit] / np.median(mx[suit]))[:, None]
            rgb[suit] = np.clip(skin[None, :] * shade, 0, 1)
            px[:, :3] = rgb
            img.pixels[:] = px.ravel()
            img.update()
            log("skin recolor", img.name, int(suit.sum()), "px, skin", tuple(np.round(skin, 3)))


def base_color_images(obj):
    for mat in obj.data.materials:
        for node in mat.node_tree.nodes:
            if node.type == "TEX_IMAGE" and node.image is not None and any(l.to_socket.name == "Base Color" for o in node.outputs for l in o.links):
                yield node.image


def uv_mask(polys, uv, w, h):
    """면들의 UV 삼각형을 칠해 "이 면들이 쓰는 텍스처 자리" 표 [y][x]를 만든다 (가장자리 1픽셀 여유)."""
    allowed = np.zeros((h, w), dtype=bool)
    for p in polys:
        pts = [(uv[li].uv[0] * w, uv[li].uv[1] * h) for li in p.loop_indices]
        for k in range(1, len(pts) - 1):
            (ax, ay), (bx, by), (cx, cy) = pts[0], pts[k], pts[k + 1]
            x0, x1 = int(max(0, min(ax, bx, cx) - 1)), int(min(w - 1, max(ax, bx, cx) + 1))
            y0, y1 = int(max(0, min(ay, by, cy) - 1)), int(min(h - 1, max(ay, by, cy) + 1))
            if x1 < x0 or y1 < y0:
                continue
            gx, gy = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
            d = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
            if abs(d) < 1e-9:
                continue
            u = ((by - cy) * (gx - cx) + (cx - bx) * (gy - cy)) / d
            v = ((cy - ay) * (gx - cx) + (ax - cx) * (gy - cy)) / d
            m = 1.5 / max(1.0, min(x1 - x0, y1 - y0) + 1)   # 가장자리 여유
            allowed[y0:y1 + 1, x0:x1 + 1] |= (u >= -m) & (v >= -m) & (u + v <= 1 + m)
    return allowed


def jaw_shadow_recolor(obj):
    """Varco가 턱 밑에 짙은 그림자 띠를 텍스처로 구워 넣으면 목에 검은 줄(초커)처럼 보인다.
    턱 밑을 향한 면(아래를 보는 면, 옷깃 위 ~ 입 아래)이 쓰는 텍스처 자리의 어두운 픽셀을 같은 그림의 피부색(조금 어둡게)으로 칠한다."""
    me = obj.data
    uv = me.uv_layers.active.data
    low = body_low_z + body_height * JAW_BOTTOM_RATIO
    high = body_low_z + body_height * JAW_TOP_RATIO
    jaw = [p for p in me.polygons if p.normal.z < -0.25 and low < (obj.matrix_world @ p.center).z < high]
    for img in base_color_images(obj):
        w, h = img.size
        allowed = uv_mask(jaw, uv, w, h)
        px = np.array(img.pixels[:], dtype=np.float32).reshape(-1, 4)
        rgb = px[:, :3]
        mx = rgb.max(axis=1)
        mn = rgb.min(axis=1)
        sat = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1e-6), 0)
        skin_mask = (rgb[:, 0] > rgb[:, 1] * 1.15) & (rgb[:, 1] > rgb[:, 2] * 1.05) & (mx > 0.55) & (sat > 0.2) & (sat < 0.6)
        skin = rgb[skin_mask].mean(axis=0) if skin_mask.any() else np.array([0.9, 0.6, 0.45])
        dark = (mx < 0.5) & allowed.ravel()
        if not dark.any():
            continue
        rgb[dark] = np.clip(skin * JAW_SHADE, 0, 1)[None, :]
        px[:, :3] = rgb
        img.pixels[:] = px.ravel()
        img.update()
        log("jaw shadow recolor", img.name, int(dark.sum()), "px, faces", len(jaw))


def underwear_recolor(obj, color):
    """몸 텍스처의 어두운 갈색 속옷 픽셀을 겉옷 바탕색으로 칠한다 (겉옷 밖으로 속옷이 보이는 캐릭터용). 주름 밝기는 살린다.
    턱 아래 면이 쓰는 텍스처 자리만 칠한다 — 입 안 · 눈가 같은 얼굴의 어두운 갈색은 건드리지 않는다."""
    me = obj.data
    uv = me.uv_layers.active.data
    chin = body_low_z + body_height * COLLAR_FRONT_TOP_RATIO
    neck_back = body_low_z + body_height * NECK_BACK_TOP_RATIO
    lo, hi = world_box(obj)
    mid_y = (lo.y + hi.y) / 2   # 앞이 -Y: 가운데보다 뒤쪽이면 얼굴이 아니다

    def is_clothing_zone(p):
        c = obj.matrix_world @ p.center
        return c.z < chin or (c.z < neck_back and c.y > mid_y)

    below = [p for p in me.polygons if is_clothing_zone(p)]
    for mat in obj.data.materials:
        for node in mat.node_tree.nodes:
            if node.type != "TEX_IMAGE" or node.image is None:
                continue
            if not any(l.to_socket.name == "Base Color" for o in node.outputs for l in o.links):
                continue
            img = node.image
            w, h = img.size
            allowed = uv_mask(below, uv, w, h)
            px = np.array(img.pixels[:], dtype=np.float32).reshape(-1, 4)
            rgb = px[:, :3]
            mx = rgb.max(axis=1)
            brown = (mx < 0.50) & (rgb[:, 0] >= rgb[:, 1] - 0.02) & (rgb[:, 1] >= rgb[:, 2] - 0.03) & allowed.ravel()   # 검은 박음질 선 · 회갈색 얼룩까지
            if not brown.any():
                continue
            shade = np.clip(mx[brown] / np.median(mx[brown]), 0.6, 1.15)[:, None]
            rgb[brown] = np.clip(np.array(color, dtype=np.float32)[None, :] * shade, 0, 1)
            px[:, :3] = rgb
            img.pixels[:] = px.ravel()
            img.update()
            log("underwear recolor", img.name, int(brown.sum()), "px")


# Unity가 이름으로 재질을 연결하도록 재질 이름을 body / outfit으로 둔다 (FBX 서브메시 이름)
body.data.materials[0].name = "body"
body_material = "body"
bpy.ops.object.select_all(action="DESELECT")
body.select_set(True)
bpy.context.view_layer.objects.active = body
if not single:
    fit_outfit_and_hide_body()
    # 몸 그림이 회색 전신 타이즈였던 때(G-1 · G-2)만 쓰던 단계. 지금은 속옷 차림이라 끈다 (SKIN_RECOLOR=1로 켬).
    if os.environ.get("SKIN_RECOLOR", "0") == "1":
        skin_recolor(body)
    # 겉옷 등 쪽이 비어 속옷이 보이면 UNDERWEAR_COLOR="r,g,b"(0~1)로 속옷을 겉옷 바탕색으로 칠한다.
    if os.environ.get("UNDERWEAR_COLOR"):
        underwear_recolor(body, [float(x) for x in os.environ["UNDERWEAR_COLOR"].split(",")])
    # 턱 밑에 짙은 그림자 띠가 구워진 캐릭터는 JAW_SHADOW=1로 피부색으로 칠한다.
    if os.environ.get("JAW_SHADOW", "0") == "1":
        jaw_shadow_recolor(body)
    outfit.data.materials[0].name = "outfit"
    # 합치기
    bpy.ops.object.select_all(action="DESELECT")
    body.select_set(True)
    outfit.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.join()
merged = bpy.context.view_layer.objects.active
merged.name = name
before = tri_count(merged)

# 폴리곤 줄이기
if before > target_tris:
    mod = merged.modifiers.new("Decimate", "DECIMATE")
    mod.decimate_type = "COLLAPSE"
    mod.ratio = target_tris / before
    mod.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=mod.name)
tri = merged.modifiers.new("Triangulate", "TRIANGULATE")
bpy.ops.object.modifier_apply(modifier=tri.name)
for poly in merged.data.polygons:
    poly.use_smooth = True
log("tris", before, "->", tri_count(merged))

# 앞(-Y)을 +X로 돌리고, 높이를 맞추고, 바닥 가운데를 원점으로
lo, hi = world_box(merged)
merged.location -= Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))
bpy.ops.object.transform_apply(location=True)
merged.rotation_mode = "XYZ"   # glTF로 불러온 물체는 쿼터니언이라 오일러 값이 무시된다
merged.rotation_euler = (0, 0, -1.5707963)   # -Y(앞) → Blender -X. Unity가 FBX를 불러올 때 X를 뒤집어 Unity +X(격자 Right)가 앞이 된다
s = height_m / (hi.z - lo.z)
merged.scale = (s, s, s)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
lift = float(os.environ.get("LIFT", "0") or 0)   # 떠 있는 적: 바닥을 키의 이 비율만큼 띄운다 (원점은 땅에 남는다)
if lift > 0:
    merged.location.z += height_m * lift
    bpy.ops.object.transform_apply(location=True)
    log("lifted", height_m * lift)
lo, hi = world_box(merged)
log("final box", tuple(lo), tuple(hi))

# 텍스처를 PNG로 내보내고 FBX가 그 파일을 가리키게 한다
for mat in merged.data.materials:
    if mat is None or not mat.use_nodes:
        continue
    for node in mat.node_tree.nodes:
        if node.type == "TEX_IMAGE" and node.image is not None:
            img = node.image
            kind = "albedo" if any(l.to_socket.name == "Base Color" for o in node.outputs for l in o.links) else "normal"
            part = "body" if mat.name == body_material else "outfit"
            fname = f"{name}_{part}_{kind}.png"
            img.filepath_raw = os.path.join(out_dir, fname)
            img.file_format = "PNG"
            img.save()
            log("texture", fname, img.size[0], img.size[1])

fbx_path = os.path.join(out_dir, name + ".fbx")
bpy.ops.export_scene.fbx(
    filepath=fbx_path,
    use_selection=True,
    object_types={"MESH"},
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z",
    axis_up="Y",
    path_mode="RELATIVE",
    embed_textures=False,
    mesh_smooth_type="OFF",   # 꼭짓점 법선만 (부드러운 음영). 예전 FACE는 면마다 각져 보였다
    add_leaf_bones=False,
)
log("fbx", fbx_path)

# 확인용 렌더: 앞(Blender -X) · 비스듬한 위 · 옆 · 뒤
scene = bpy.context.scene
scene.render.engine = "BLENDER_WORKBENCH"
scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "TEXTURE"
scene.render.resolution_x = 512
scene.render.resolution_y = 512
scene.render.film_transparent = False
world = bpy.data.worlds.new("W")
scene.world = world
cam_data = bpy.data.cameras.new("Cam")
cam_data.type = "ORTHO"
cam_data.ortho_scale = height_m * 1.15
cam = bpy.data.objects.new("Cam", cam_data)
scene.collection.objects.link(cam)
scene.camera = cam
for label, loc in (("front", (-3.0, 0.0, height_m * 0.5)), ("angle", (-2.2, 2.2, height_m * 1.8)), ("side", (0.0, 3.0, height_m * 0.5)), ("back", (3.0, 0.0, height_m * 0.5))):
    cam.location = loc
    direction = Vector((0, 0, height_m * 0.5)) - Vector(loc)
    cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = os.path.join(out_dir, f"{name}_preview_{label}.png")
    bpy.ops.render.render(write_still=True)
log("done")
