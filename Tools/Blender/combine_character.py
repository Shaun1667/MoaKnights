# MoaKnights — 몸 · 겉옷 메시를 하나로 합치고 폴리곤을 줄인다.
#
# 실행 (Blender 4.3, 화면 없이):
#   blender -b -P Tools/Blender/combine_character.py -- <body.glb> <outfit.glb> <body.png> <outfit.png> <out_dir> <name> [target_tris] [height_m]
#
# 순서
#   1. 두 원본 그림(같은 구도)에서 흰 바탕이 아닌 영역의 위치를 잰다.
#   2. 두 GLB를 불러와 메시마다 하나로 합치고 변환을 적용한다.
#   3. 겉옷을 그림 비율대로 몸에 맞춘다: 몸의 "그림 1픽셀 = 몇 m"로 겉옷 높이 · 아래 · 가로 위치를 정하고, 깊이는 몸 가운데에 맞춘다.
#   4. 겉옷에 가려 안 보이는 몸 면과 겉옷을 뚫고 나온 몸 면을 지운다 (면 법선 앞뒤로 가까이 겉옷이 있으면).
#      몸 그림의 회색 바탕옷은 피부색으로 바꾼다 (겉옷 밖으로 보이는 팔 등).
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
poke_distance_ratio = 0.025    # 키에 대한 비율: 면 안쪽 이 거리 안에 겉옷이 있으면 겉옷을 뚫고 나온 몸 면
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
    return obj


def world_box(obj):
    vs = [obj.matrix_world @ v.co for v in obj.data.vertices]
    lo = Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs)))
    hi = Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs)))
    return lo, hi


def tri_count(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


# 빈 장면에서 시작
bpy.ops.wm.read_factory_settings(use_empty=True)

bb = image_box(body_png)
ob = image_box(outfit_png)
log("image body", bb, "outfit", ob)

body = import_mesh(body_glb, "Body")
outfit = import_mesh(outfit_glb, "Outfit")
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
outfit.location += Vector((target_cx - (olo.x + ohi.x) / 2, target_cy - (olo.y + ohi.y) / 2, target_bottom - olo.z))
bpy.ops.object.transform_apply(location=True)
olo, ohi = world_box(outfit)
log("outfit fitted", tuple(olo), tuple(ohi), "scale", scale)

# 가려진 몸 면 지우기
deps = bpy.context.evaluated_depsgraph_get()
tree = BVHTree.FromObject(outfit, deps)
limit = (bhi.z - blo.z) * hidden_distance_ratio
poke = (bhi.z - blo.z) * poke_distance_ratio
bm = bmesh.new()
bm.from_mesh(body.data)
hidden = []
for f in bm.faces:
    c = f.calc_center_median()
    n = f.normal
    if tree.ray_cast(c + n * 1e-4, n, limit)[0] is not None or tree.ray_cast(c - n * 1e-4, -n, poke)[0] is not None:
        hidden.append(f)
before_faces = len(bm.faces)
bmesh.ops.delete(bm, geom=hidden, context="FACES")
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


skin_recolor(body)
# Unity가 이름으로 재질을 연결하도록 재질 이름을 body / outfit으로 둔다 (FBX 서브메시 이름)
body.data.materials[0].name = "body"
outfit.data.materials[0].name = "outfit"
body_material = "body"

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
    mesh_smooth_type="FACE",
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
