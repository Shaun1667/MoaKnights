# BALANCE — MoaKnights

## 0. 데이터 에셋 위치
| 표 | 에셋 | 코드 |
|---|---|---|
| 1. 코스트 · 2. 시간 | `Assets/MoaKnights/Data/Balance/BattleBalance.asset` | `BattleBalanceAsset` |

## 1. 코스트
| 번호 | 항목 | 값 | 출처 |
|---|---|---|---|
| 1-1 | 시작 코스트 | 20 | 기획서 |
| 1-2 | 1 회복에 걸리는 초 | 2 | 기획서, 작업 시트 |
| 1-3 | 최대 코스트 | 99 | 작업 시트 |
| 1-4 | 직접 퇴각 환급 비율 | 1/2, 내림 | 작업 시트 (코드 미구현, 에셋 없음) |

## 2. 시간
| 번호 | 항목 | 값 | 출처 |
|---|---|---|---|
| 2-1 | 초당 틱 | 30 | 결정 2026-11-09, 사용자 |

검산: `ProjectRuleTests.BattleBalance_MatchesBalanceDoc`.
