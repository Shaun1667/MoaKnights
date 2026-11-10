#!/usr/bin/env bash
# MoaKnights — ArtSource/<ID>/의 원본으로 게임용 모델을 만든다 (Git Bash).
#   Tools/Blender/build_models.sh <ID> [<ID> ...]
# 몸 · 겉옷(body.glb + outfit.glb)이 있으면 능력자 → Art/Units/<ID>/, 한 덩어리(model.glb)면 적 → Art/Enemies/<ID>/.
# 높이 · 삼각형 수는 아래 표. Blender 위치는 BLENDER 환경 변수로 바꾼다.
set -euo pipefail
cd "$(dirname "$0")/../.."
BLENDER="${BLENDER:-/d/blender.exe}"
ASSETS=MoaKnights/Assets/MoaKnights/Art
w() { cygpath -w "$PWD/$1"; }

height_of() {
  case "$1" in
    tank_01) echo 1.0 ;;
    dizes_fast) echo 0.7 ;;
    dizes_tank) echo 1.0 ;;
    dizes_01) echo 0.7 ;;
    *) echo 0.9 ;;
  esac
}

for id in "$@"; do
  src=ArtSource/$id
  h=$(height_of "$id")
  if [ -f "$src/outfit.glb" ]; then
    out=$ASSETS/Units/$id
    mkdir -p "$out"
    "$BLENDER" -b -P "$(w Tools/Blender/combine_character.py)" -- "$(w $src/body.glb)" "$(w $src/outfit.glb)" "$(w $src/body.png)" "$(w $src/outfit.png)" "$(w $out)" "$id" 3000 "$h" | grep '\[combine\]'
  else
    out=$ASSETS/Enemies/$id
    mkdir -p "$out"
    "$BLENDER" -b -P "$(w Tools/Blender/combine_character.py)" -- "$(w $src/model.glb)" - - - "$(w $out)" "$id" 3000 "$h" | grep '\[combine\]'
  fi
  mv "$out"/"${id}"_preview_*.png "$src"/
  rm -f "$out"/"${id}"_*_normal.png   # 노멀맵은 쓰지 않는다 (화면에서 작음)
done
