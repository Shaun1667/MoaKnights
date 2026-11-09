# BALANCE — MoaKnights

## 0. 데이터 에셋 위치
| 표 | 에셋 | 코드 |
|---|---|---|
| 1. 코스트 | `Assets/MoaKnights/Data/Balance/BattleBalance.asset` | `BattleBalanceAsset` |

## 1. 코스트
| 번호 | 항목 | 값 | 출처 |
|---|---|---|---|
| 1-1 | 시작 코스트 | 20 | 기획서 |
| 1-2 | 1 회복에 걸리는 초 | 2 | 기획서, 작업 시트 |
| 1-3 | 최대 코스트 | 99 | 작업 시트 |
| 1-4 | 직접 퇴각 환급 비율 | 1/2, 내림 | 작업 시트 (코드 미구현, 에셋 없음) |

검산: `BalanceTests.BattleBalance_MatchesBalanceDoc`.
