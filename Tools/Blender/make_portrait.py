# MoaKnights — 컨셉 그림에서 카드용 얼굴 그림을 자른다.
#
# 실행 (Blender, 화면 없이):
#   blender -b -P Tools/Blender/make_portrait.py -- <concept.png> <out.png> [size=256]
#
# 흰 바탕이 아닌 영역(캐릭터)의 위쪽에서 머리를 찾아 정사각형으로 자르고, 흰 바탕은 투명하게 만든다.
# 2~3등신이라 머리는 캐릭터 높이의 위쪽 약 40%다.

import sys

import bpy
import numpy as np

argv = sys.argv[sys.argv.index("--") + 1:]
src, out = argv[0], argv[1]
size = int(argv[2]) if len(argv) > 2 else 256

HEAD_RATIO = 0.40      # 캐릭터 높이에 대한 머리(+목) 높이
WHITE = 0.92           # 이보다 밝은 무채색은 바탕 (캐릭터 범위를 잴 때)
BG_LO = 0.85           # 가장자리에서 이어진 이 밝기 이상은 바탕 후보
BG_HI = 0.95           # 바탕 중 이 밝기 이상은 완전히 투명


def log(*parts):
    print("[portrait]", *parts, flush=True)


img = bpy.data.images.load(src)
w, h = img.size
px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)   # 아래 줄이 0
px = px[::-1]                                                       # 위 줄이 0이 되게 뒤집는다

rgb = px[:, :, :3]
solid = rgb.min(axis=2) < WHITE
ys, xs = np.nonzero(solid)
top, bottom = ys.min(), ys.max()
height = bottom - top
head_bottom = top + int(height * HEAD_RATIO)

# 머리 줄들에서 가로 가운데를 잡는다 (무기 · 머리카락 끝이 튀어나와도 중앙값은 머리에 있다).
head_xs = xs[(ys >= top) & (ys <= head_bottom)]
cx = int(np.median(head_xs))
side = int(height * HEAD_RATIO * 1.05)
x0 = max(0, cx - side // 2)
y0 = max(0, top - int(side * 0.04))
x1 = min(w, x0 + side)
y1 = min(h, y0 + side)
log("bbox", (int(xs.min()), int(top), int(xs.max()), int(bottom)), "crop", (x0, y0, x1, y1))

crop = px[y0:y1, x0:x1].copy()
# 흰 바탕 → 투명. 그림 가장자리에서 이어진 밝은 영역만 바탕으로 본다 (흰 옷 · 두건은 남는다).
lum_min = crop[:, :, :3].min(axis=2)
light = lum_min > BG_LO
bg = np.zeros_like(light)
bg[0, :] = light[0, :]; bg[-1, :] = light[-1, :]; bg[:, 0] = light[:, 0]; bg[:, -1] = light[:, -1]
while True:
    grown = bg.copy()
    grown[1:, :] |= bg[:-1, :]; grown[:-1, :] |= bg[1:, :]; grown[:, 1:] |= bg[:, :-1]; grown[:, :-1] |= bg[:, 1:]
    grown &= light
    if (grown == bg).all():
        break
    bg = grown
# 바탕 안에서는 밝을수록 투명하게 (BG_HI 이상 완전 투명), 경계를 부드럽게.
fade = np.clip((BG_HI - lum_min) / (BG_HI - BG_LO), 0.0, 1.0)
alpha = np.where(bg, fade, 1.0)
crop[:, :, 3] = np.minimum(crop[:, :, 3], alpha)

ch, cw = crop.shape[:2]
canvas = np.zeros((side, side, 4), dtype=np.float32)
canvas[:ch, :cw] = crop
canvas = canvas[::-1]   # Blender 픽셀 순서(아래 줄이 0)로 되돌린다

outimg = bpy.data.images.new("portrait", side, side, alpha=True)
outimg.pixels[:] = canvas.ravel()
outimg.scale(size, size)
outimg.filepath_raw = out
outimg.file_format = "PNG"
outimg.save()
log("saved", out, size)
