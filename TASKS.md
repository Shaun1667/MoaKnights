# TASKS

## 진행 요약 (2026-11-09 기준 — 새 세션은 여기부터 읽는다)
**최신 (2026-11-09): 1-1 틱 · 난수 · 스냅샷 · 격자 맵 그레이박스 끝 — 브랜치 `phase1-foundation`(`phase0-setup` 위), 둘 다 main 병합 전, 테스트 작성만 (한 번도 실행 안 함), 플레이 확인 대기 0-1 · 1-1. 다음: 1-2 (적 이동 · 디펜스 HP) — 착수 전에 착수 질문.**

## Phase 0. 세팅
- [x] **0-1. 프로젝트 세팅** (2026-11-09 사용자 "MY_DEV_GUIDE대로, 기획은 Drive") — 플레이 확인 대기
  - 결정 (2026-11-09, 사용자): 제목은 MoaKnights로 개명, 내용은 세편기록 기획 그대로 — 저장소 이름과 통일.
  - 결정 (2026-11-09, 사용자): 범위는 스테이지 여러 개 + 편성 (가챠 · 성장 · 스토리 제외).
  - 결정 (2026-11-09, 사용자): PC 우선 (원본 기획은 모바일이었음).
  - 결정 (2026-11-09, 사용자): 전부 3D 모델 (원본 기획은 2D였음).
  - 정한 것 (Claude, 확인 필요): 핵심 재미 3가지, 스테이지 3~5개 · 한 판 3~5분, uGUI + TMP, Noto Sans KR, 기본 언어 ko + en 열.
  - 정한 것 (Claude, 확인 필요): `.gitignore`를 `MoaKnights/` 안으로 옮기고 `**/` 패턴을 프로젝트 기준으로 고침 — 저장소 루트 기준이라 `/[Aa]ssets/…` 규칙이 안 먹던 문제.
  - 구현: 어셈블리 Core · Localization(엔진 참조 금지) · Game · Editor · Tests, `Text`/`TextTable`, `GameRoot`, `BattleBalanceAsset`, 메뉴 `MoaKnights/Setup/Run All` · `MoaKnights/Debug/Debug Window`.
  - 수치: BALANCE 1번.
  - 정한 것 (Claude, 확인 필요): 폰트는 Windows에 있던 `NotoSansKR-VF.ttf`(OFL)를 복사해 씀 — 가변 폰트 기본값이라 글자가 얇게 나온다. 일반 굵기 정적 폰트로 교체하는 것은 UI 작업 때 처리.
  - 확인: 어셈블리 5개 컴파일 · Setup 메뉴 실행 · TMP Essentials 가져옴 · 플레이 모드에서 한글 표시(캡처) · 재시작 후 같은 시드 유지(35543125) · 데이터 에셋 20/2/99 읽힘 · 콘솔 오류 0.
  - 확인 못 함: 디버그 창은 메뉴로 열리는 것만 확인하고 버튼을 직접 누르지 않음(재시작은 같은 코드 경로를 호출해 확인), 새 시드 재시작, Setup 재실행 후 씬 내용 동일 여부, 빌드.
  - 테스트 작성, 실행 안 함: `TextTableTests`, `ProjectRuleTests`.
  - 확인 방법: `Assets/MoaKnights/Scenes/Battle.unity` 플레이 → "MoaKnights / 전투 준비 중"이 보이면 정상 → 메뉴 `MoaKnights/Debug/Debug Window`에서 시드 확인, 재시작 버튼 눌러 시드 유지 확인.

## Phase 1. 구조 기반 + 핵심 조작 (후보)
- [x] **1-1. 고정 틱 · 시드 난수 · 스냅샷 뼈대 + 격자 맵 그레이박스** (2026-11-09 사용자 "1-1 시작") — 플레이 확인 대기
  - 결정 (2026-11-09, 사용자): 초당 30틱 — 실시간 디펜스에 충분하고 부담이 적다.
  - 결정 (2026-11-09, 사용자): 맵은 글자 격자 에셋 — 바로 만들 수 있고 git에서 차이가 보인다. 맵 에디터 창은 나중에.
  - 결정 (2026-11-09, 사용자): 비스듬한 쿼터뷰 — 3D와 높낮이가 잘 보이고 참고 게임의 구도와 다르다.
  - 구현: Core `World`(상태 · 스냅샷) · `Simulation`(Step) · `TickClock`(배속 · 일시정지) · `SeededRandom`(용도별 스트림) · `GridMap`(Parse), Game `StageAsset` · `BattleViewAsset` · `MapView` · `BattleCameraRig`, 디버그 창에 틱 · 맵 섹션, 에셋 `Data/Stages/Stage01.asset` · `Data/View/BattleView.asset` · `Art/Greybox/Greybox.mat`.
  - 정한 것 (Claude, 확인 필요): 첫 맵 7x7은 원본 `게임설명.png`를 따름. 직교(orthographic) 카메라 pitch 50 · yaw 45. 언덕 높이 0.4(0.7은 뒤 경로를 가려서 낮춤). `RunContext`는 `World`로 대체해 지움.
  - 수치: BALANCE 2번.
  - 확인: 컴파일 오류 0 · Setup 2회 실행 후 데이터 에셋 동일, 씬 줄 수 동일 · 플레이에서 칸 49개(지상 13 / 언덕 30 / 막힘 4 / 입출구 2) · 틱 진행 · 2배속 0.1초에 6틱 · 일시정지 0틱 · 새 시드 재시작(틱 0부터) · 콘솔 오류 0 · 캡처.
  - 확인 못 함: 디버그 창 버튼을 직접 누르지 않음(같은 `DebugXxx`를 코드로 호출), 실제 2배속 시간 경과(계산 호출만), 빌드, 스냅샷 왕복(테스트만 작성).
  - 테스트 작성, 실행 안 함: `SimFoundationTests`, `ProjectRuleTests`(스테이지 · 뷰 표 · 씬 참조 추가).
  - 확인 방법: `Battle.unity` 플레이 → 어두운 배경에 쿼터뷰 격자, 빨강 입구 · 파랑 출구가 보이면 정상 → 디버그 창에서 틱이 오르는지, 일시정지 · 1틱 · x2 · 재시작 버튼 확인.
  - 피드백: 씬의 `_balance` · `_stage` · `_view`가 비어 플레이 때 NullReference → `NewScene`이 먼저 불러 둔 에셋을 메모리에서 내림 → 씬을 만든 뒤에 경로로 불러오게 수정. 검사: `BattleScene_GameRootHasNoEmptyReferences`.
- [ ] 1-2. 적이 경로를 따라 이동 · 디펜스 HP
- [ ] 1-3. 코스트 회복 · 카드 드래그 배치(방향 선택)

## Phase 2. 시스템 연결 (후보)
- [ ] 2-1. 저지 · 자동 공격 · 공격 범위 · 피해 숫자
- [ ] 2-2. 중첩 3단계 · 퇴각 환급 · 재배치 쿨타임
- [ ] 2-3. SP 3타입 · 스킬 · 2배속 · HUD

## Phase 3. 콘텐츠 (후보)
- [ ] 3-1. 스테이지 데이터 · 승패 · 스테이지 3~5개
- [ ] 3-2. 편성 화면 · 능력자 / 적 표

## Phase G · 4 · 5 · D (후보)
- [ ] G. 3D 에셋 교체 (에셋 출처는 GDD 9-7)
- [ ] 4. 30분 플레이 재미 검증
- [ ] 5. 메뉴 · 저장 · 사운드
- [ ] D. PC 빌드 · 시연

## 다른 PC에서 할 일
- 없음
