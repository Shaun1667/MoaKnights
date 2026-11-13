#!/usr/bin/env bash
# MoaKnights 난이도 측정 (6-4): 데이터 에셋을 읽어 규칙 코드(Scripts/Core)만으로 스테이지 10개 x 편성 5개를 자동 배치 봇으로 돌린다.
# Unity 없이 Unity에 들어 있는 Mono로 컴파일 · 실행한다. 결과는 표로 찍는다 (W = 승리 · 별, L = 패배, hp = 남은 디펜스 HP).
#   Tools/BalanceSim/run.sh                 에셋 등장 표
#   Tools/BalanceSim/run.sh proposed        Plans.cs의 실험 표
#   Tools/BalanceSim/run.sh detail10        스테이지 10에서 샌 적 · 쓰러진 능력자
set -euo pipefail
cd "$(dirname "$0")"
MONO="${MONO:-/c/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Data/MonoBleedingEdge}"
CORE=../../MoaKnights/Assets/MoaKnights/Scripts/Core
OUT="${TMPDIR:-/tmp}/moaknights_balancesim"
mkdir -p "$OUT"
python gen_data.py "$OUT/Data.cs" > /dev/null
files=()
while IFS= read -r f; do files+=("$f"); done < <(find "$CORE/Sim" "$CORE/Map" -name "*.cs")
"$MONO/bin/mono.exe" "$MONO/lib/mono/4.5/csc.exe" -nologo -langversion:latest -out:"$OUT/sim.exe" "${files[@]}" "$OUT/Data.cs" Factory.cs Bot.cs Plans.cs
"$MONO/bin/mono.exe" "$OUT/sim.exe" "$@"
