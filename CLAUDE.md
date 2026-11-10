# CLAUDE.md

## 프로젝트 개요
MoaKnights — 3D 타워 디펜스 RPG. 격자 맵에 코스트로 능력자를 배치해 경로를 따라오는 적을 막고, 같은 능력자를 겹쳐 배치(중첩, 최대 3회)해 강화한다. 원본 기획 「세편기록」을 개명해 쓴다. 장르 참고는 구조만 — 다른 게임의 화면 · UI · 이름 · 아트를 베끼지 않는다.
기획은 GDD.md, 작업 순서와 일지는 TASKS.md, 수치는 BALANCE.md.
- **새 세션을 시작하면** 이 문서 → TASKS.md 맨 위 "진행 요약" 순서로 읽고 다음 항목을 확인한 뒤 시작한다. 다른 PC면 Docs/진행상황.md도. "다음 작업: 미정"이면 후보를 먼저 묻는다.
- 수치는 BALANCE.md와 데이터 에셋 기준이며 코드에 임의로 넣지 않는다.
- **Google Drive 기획 폴더는 읽기 전용이다.** 업로드 · 수정 금지. 그 문서의 개인정보(옛 팀원 연락처 등)와 폴더 링크를 이 저장소에 옮겨 적지 않는다.

## 참조 문서
| 문서 | 역할 | 상태 |
|---|---|---|
| GDD.md | 기획 | 초안 (2026-11-09) |
| TASKS.md | 진행 요약 · 작업 순서 · 일지 | 사용 중 |
| BALANCE.md | 수치 표 | 코스트 · 틱 · 디펜스 · 적 · 등장 |
| Docs/Source/MY_DEV_GUIDE.md | 협업 표준 가이드 원본 | 고치지 않음 |

## 협업 방식
- 한국어. 1인 개발. 설계 결정은 선택지 + 추천안으로 묻고 `결정 (날짜, 사용자)`로 남긴다. 사소한 것은 정하고 "정한 것 (Claude, 확인 필요)"로 보고.
- 한 항목 = 작업 브랜치(`phaseN-내용`) 커밋 + 보고. 지시 없이 main에 커밋 · 푸시하지 않는다. "main에 병합하고 푸시" = ff 병합 · 푸시 · 브랜치 삭제.
- 보고: 한 일 · 확인한 것 · 확인 못 한 것 · 확인 방법 · 커밋 상태.
- 테스트 실행은 사용자가 지시할 때만 (가이드 기본값) — Claude는 작성만, "테스트 작성, 실행 안 함". 컴파일 확인 · Setup 메뉴 · 플레이 모드 확인은 Claude가 한다. 몇 항목마다 "테스트 돌릴 때 됐다"고 알린다.

## 작업 원칙
- 한 번에 기능 하나 (TASKS 현재 항목만). 요청받지 않은 시스템을 미리 만들지 않는다 — 단 핵심 설계 규칙의 구조는 처음부터.
- 구현 전 문서를 읽고 구현 후 문서를 고친다. 다르면 문서를 실제 기준으로 고치고 알린다.
- 에디터 수작업은 목록으로 안내, 반복 생성은 멱등 Setup 메뉴로.
- 완료 = 테스트 통과 + 확인 방법 + 사용자 플레이 확인. 구현만 = `— 플레이 확인 대기`.
- 실패 · 생략 · 미확인은 그대로 보고. 직접 본 것만 됐다고 말한다.

## 기술 스택
| 항목 | 값 | 상태 |
|---|---|---|
| 엔진 | Unity 6000.3.25f1 | 확정 2026-11-09, 변경 금지 |
| 렌더 | URP 17.3 (PC_RPAsset), 리니어 색 공간 | 확정 2026-11-09, 변경 금지 |
| 플랫폼 | PC 우선 (Windows) | 결정 2026-11-09, 사용자 |
| 그래픽 | 전부 3D 모델 (그레이박스로 시작) | 결정 2026-11-09, 사용자 |
| 시점 | 비스듬한 쿼터뷰 (직교 카메라) | 결정 2026-11-09, 사용자 |
| 입력 | Input System 패키지 | 프로젝트 기본값, 변경 금지 |
| UI | uGUI + TextMeshPro | 정한 것 (Claude, 확인 필요) |
| 폰트 | Noto Sans KR (OFL 라이선스) | 정한 것 (Claude, 확인 필요) |

## 폴더 구조
저장소 루트에 문서, Unity 프로젝트는 `MoaKnights/`.
```
MoaKnights/Assets/MoaKnights/
  Scripts/Core/          규칙 코드 — 엔진 참조 금지 (MoaKnights.Core)
  Scripts/Localization/  텍스트 표 — 엔진 참조 금지 (MoaKnights.Localization)
  Scripts/Game/          엔진 어댑터 · GameRoot (MoaKnights.Game)
  Editor/                디버그 창 · Setup 메뉴 (MoaKnights.Editor, 빌드 제외)
  Tests/                 EditMode 테스트 (MoaKnights.Tests)
  Data/Balance/          수치 데이터 에셋
  Data/Stages/           스테이지(맵 글자 격자)
  Data/View/             전투 화면 표현값
  Data/Text/text.csv     화면 글 전부
  Art/Greybox/  Fonts/  Scenes/
```

## 코딩 규칙
- 네임스페이스 `MoaKnights.<어셈블리>`. private 필드 `_camelCase`, public은 PascalCase.
- 화면에 보이는 모든 글은 `Data/Text/text.csv` 한곳 — 코드 · 프리팹 · 데이터에 문장 금지, 코드는 `Text.Get(키)` · `Text.Format(키, 값…)`, 문장을 이어 붙이지 않고 `{0}` 자리표시자. 키는 `분류.대상.용도`. 새 글 = 표 한 줄.
- 규칙 코드는 글을 만들지 않는다. 이벤트와 값만 내보내고, 글은 화면 계층이 표에서 만든다.
- 개발자 디버그 기능은 `MoaKnights/Debug/Debug Window`에 섹션으로, 게임 쪽 진입점은 `GameRoot.DebugXxx`, 상태를 바꾸면 [상태 변경] 표시.
- 씬 연결은 `GameRoot` 한 곳. 싱글턴 금지.
- 전투 수치는 `BattleBalanceAsset`(`Data/Balance/BattleBalance.asset`) 한 곳, 새 수치 = BALANCE 표 + 필드 + 검산 테스트. 스테이지마다 다른 등장 표만 `StageAsset`에 둔다.
- 규칙이 쓰는 값은 `BattleDefinitionFactory`가 에셋에서 `BattleDefinition`으로 바꾼다. 규칙 코드의 시간과 위치는 정수(틱, 진행도)이고 실수는 화면 보간에만 쓴다.
- 에셋 · 씬 생성은 `MoaKnights/Setup/Run All` 한 곳(`ProjectSetup`), 몇 번 실행해도 같은 결과. 데이터 에셋은 없을 때만 만들고, `NewScene` 뒤에 불러와 씬에 연결한다.
- 시뮬레이션은 `Simulation.Step` 한 곳에서만 진행, 새 시스템 = Step에 순서대로 추가. 실제 시간 → 틱, 배속, 일시정지는 `TickClock` 한 곳.
- 플레이어 명령(배치 · 중첩 · 퇴각)은 시간을 진행하지 않으므로 `Simulation.TryXxx`로 바로 처리하고 결과 enum을 돌려준다 (일시정지 중에도 받는다). 미리보기는 같은 판정의 `CheckXxx`를 쓴다.
- 지금 공격력 · 저지 수는 `Simulation.AttackOf` · `BlockOf` 한 곳 (단계 + 쓰고 있는 스킬). SP · 스킬은 `SkillSet`(능력자별, 단계마다 스킬 하나). 스킬 이름 글 키는 `skill.<능력자ID>_<단계>.name`.
- 중첩 · 퇴각 · 재배치 수치는 `DeployRules` 한 곳 (`BattleDefinition.Deploy`). 단계별 HP · 공격 · 저지는 `DeployRules.MaxHp/Attack/BlockCount(정의, 단계)`로만 구한다.
- `Simulation.Step` 순서: 코스트 회복 → 스킬 시간 · 자연 SP → 능력자 공격 → 막힌 적의 반격 → 적 이동(저지 · 누수) → 등장 → 틱 증가. 새 단계는 이 순서 안 어디에 들어가는지 정하고 주석과 이 줄을 고친다.
- 공격 범위는 `AttackRange.Parse` 한 곳, 글자 격자(`O` 자리 · `@` 자리+공격 · `X` 공격 · `.` 빈 칸, 오른쪽이 앞). 적이 서 있는 칸은 `Simulation.EnemyCell`(진행도 반올림) 한 곳.
- 이벤트 호출 `X?.Invoke(...)`의 인자에 상태 변경을 넣지 않는다 — 구독자가 없으면 인자가 계산되지 않는다 (1-2 무한 루프 원인).
- 입력은 Input System (`Mouse.current` 등)과 uGUI EventSystem(`InputSystemUIInputModule`)만 쓴다.
- 판 상태는 `World` 한 곳, 새 상태 = 필드 + `WorldSnapshot` 필드 + `SnapshotVersion` 증가 + 왕복 테스트.
- 난수는 `World.Rng(RngStream.X)`로만, `UnityEngine.Random` · `System.Random` 금지. 새 용도 = enum 끝에 추가.
- 맵은 `GridMap.Parse` 한 곳, 데이터 `StageAsset`(글자 격자: `#` 막힘, `.` 지상, `H` 언덕, `S` 입구, `E` 출구), 새 스테이지 = 에셋 하나. 적의 길은 `EnemyRoute.Parse` 한 곳, 꺾이는 칸을 `6,3 3,3 3,1`처럼 적고 입구에서 출구까지 지상 칸만 지난다.
- 전투 화면 표현값은 `BattleViewAsset` 한 곳, 새 칸 종류 = 표 한 줄. 그리기는 `MapView` · `EnemyView` · `UnitView`(단계 점 포함) · `CardBar` · `BattleHud` · `DamageNumbers`(떠오르는 피해 숫자) · `HealthBar` · `RangeView`(범위 판), 배치 · 중첩 입력은 `PlacementController`, 능력자 클릭 · 퇴각 · 스킬 버튼은 `UnitActionPanel`, 일시정지 · 배속 버튼과 단축키는 `SpeedControls`, 카메라는 `BattleCameraRig`.
- enum은 끝에만 추가한다.

## 핵심 설계 규칙 (변경 금지)
바꾸고 싶어지면 영향 범위를 표로 정리하고 사용자 결정을 받은 뒤에만.
1. 규칙과 표현 분리 — 규칙은 엔진을 모르는 어셈블리, 엔진 쪽은 이벤트를 받아 그리기만 한다.
2. 고정 틱 하나 — 시뮬레이션은 틱으로만 진행한다. 배속은 틱 진행 속도만 바꾼다.
3. 상태는 스냅샷 하나.
4. 시드 난수 주입 · 용도별 분리 · 같은 시드 = 같은 결과.
5. 모든 엔티티 고유 ID.
6. 중첩은 능력자 하나당 한 칸 — 겹쳐도 유닛이 늘지 않고 그 유닛의 단계가 오른다.

## 아트 · 그래픽 규칙
- 전부 3D. 데이터는 비주얼을 ID로만 참조하고, 프리팹이 없으면 그레이박스 도형으로 그린다.
- 전투 화면 구성은 참고 게임과 다르게 잡는다 (원본 기획서가 "참고 게임과 너무 동일한 화면"을 문제로 꼽았다).
- 옛 팀의 일러스트 · UI 원본은 권리를 사용자가 확인하기 전에 쓰지 않는다.

## Git · 환경
- 커밋 메시지 `MM.DD/Phase-항목/PascalCase요약`, 수정은 `…Fix`. 한 항목 = 한 커밋.
- 커밋 전 `git status` → 내 파일만 `git add` → `git diff --cached --stat`. 다른 세션 변경 제외.
- `.gitignore`는 `MoaKnights/` 안에 있다 (패턴이 Unity 프로젝트 기준). 넓은 `**/Build/` 패턴 금지.
- 동적 폰트 에셋은 런타임에 만들어 글자 데이터 변경이 커밋에 섞이지 않게 한다.
