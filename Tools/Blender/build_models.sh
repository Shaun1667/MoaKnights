#!/usr/bin/env bash
# MoaKnights — ArtSource/<ID>/의 원본으로 게임용 모델을 만든다 (Git Bash).
#   Tools/Blender/build_models.sh <ID> [<ID> ...]
# 몸 · 겉옷(body.glb + outfit.glb)이 있으면 능력자 → Art/Units/<ID>/, 한 덩어리(model.glb)면 적 → Art/Enemies/<ID>/.
# 능력자는 컨셉 그림에서 카드용 얼굴 그림(<ID>_portrait.png)도 만든다. 얼굴만 다시 만들려면 PORTRAIT_ONLY=1.
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

# 삼각형 수: 3성 3천, 4성 8천, 5성 1만5천 (장식 · 프릴이 많아 적게 줄이면 옷이 찢어진 듯 뭉개짐).
tris_of() {
  case "$1" in
    blade_01) echo 8000 ;;
    irene_01) echo 15000 ;;
    *) echo 3000 ;;
  esac
}

# 겉옷 등 쪽이 비어 속옷이 보이는 캐릭터: 몸 텍스처의 갈색 속옷을 칠할 겉옷 바탕색 (없으면 안 칠함).
underwear_color_of() {
  case "$1" in
    irene_01) echo "0.93,0.90,0.96" ;;
    *) echo "" ;;
  esac
}

for id in "$@"; do
  tris=$(tris_of "$id")
  export UNDERWEAR_COLOR="$(underwear_color_of "$id")"
  src=ArtSource/$id
  h=$(height_of "$id")
  if [ -f "$src/outfit.glb" ]; then
    out=$ASSETS/Units/$id
    mkdir -p "$out"
    "$BLENDER" -b -P "$(w Tools/Blender/make_portrait.py)" -- "$(w $src/concept.png)" "$(w $out/${id}_portrait.png)" 256 | grep '\[portrait\] saved'
    [ "${PORTRAIT_ONLY:-0}" = 1 ] && continue
    "$BLENDER" -b -P "$(w Tools/Blender/combine_character.py)" -- "$(w $src/body.glb)" "$(w $src/outfit.glb)" "$(w $src/body.png)" "$(w $src/outfit.png)" "$(w $out)" "$id" "$tris" "$h" | grep '\[combine\]'
  else
    out=$ASSETS/Enemies/$id
    mkdir -p "$out"
    "$BLENDER" -b -P "$(w Tools/Blender/combine_character.py)" -- "$(w $src/model.glb)" - - - "$(w $out)" "$id" 3000 "$h" | grep '\[combine\]'
  fi
  mv "$out"/"${id}"_preview_*.png "$src"/
  rm -f "$out"/"${id}"_*_normal.png   # 노멀맵은 쓰지 않는다 (화면에서 작음)
done
