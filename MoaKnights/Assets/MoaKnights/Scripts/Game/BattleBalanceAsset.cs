using System;
using MoaKnights.Core.Sim;
using UnityEngine;

namespace MoaKnights.Game
{
    /// <summary>전투 수치 한 곳 (뽑기 · 재화 포함). 표는 BALANCE.md 1 · 2 · 3 · 5 · 6 · 7 · 8 · 9 · 10번, 값은 에셋에 둔다. 공격 범위 글자는 AttackRange 설명을 따른다.</summary>
    [CreateAssetMenu(fileName = "BattleBalance", menuName = "MoaKnights/Battle Balance")]
    public sealed class BattleBalanceAsset : ScriptableObject
    {
        [Serializable]
        public struct EnemyRow
        {
            public string Id;
            public float SpeedCellsPerSecond;
            public int LeakDamage;
            public int MaxHp;
            public int Attack;
            public float AttackIntervalSeconds;

            // 특성 (6-2, BALANCE 3-7 ~ 3-13). 0 · false · 빈 칸이면 없음.
            public int Armor;
            public bool Flying;
            public int RangedReach;
            public int HealAmount;
            public float HealIntervalSeconds;

            /// <summary>막힐 때 쓰는 저지 수. 0이면 1로 본다.</summary>
            public int BlockWeight;
            public string SplitInto;
            public int SplitCount;
        }

        [Serializable]
        public struct UnitRow
        {
            public string Id;
            public int Cost;
            public UnitTerrain Terrain;
            public int MaxHp;
            public int Attack;
            public float AttackIntervalSeconds;
            public int BlockCount;
            public string[] Range;
            public UnitRole Role;
            public SpType SpType;
            public int MaxSp;
            public float NaturalSpSeconds;
            public SkillRow[] Skills;

            /// <summary>등급(성). 지금 능력자는 모두 3성 (결정 2026-11-11, 사용자). 뽑기 확률 표가 쓴다.</summary>
            public int Rarity;
        }

        /// <summary>스킬 한 단계. 칸 0이 1단계. 회복은 즉시, 나머지는 지속 시간 동안.</summary>
        [Serializable]
        public struct SkillRow
        {
            public int HealPercent;
            public int AttackBonusPercent;
            public int BlockBonus;
            public float DurationSeconds;
        }

        /// <summary>뽑기 등급 하나의 가중치.</summary>
        [Serializable]
        public struct RarityWeightRow
        {
            public int Rarity;
            public int Weight;
        }

        [SerializeField] private int _startCost;
        [SerializeField] private float _costRegenSeconds;
        [SerializeField] private int _maxCost;
        [SerializeField] private int _ticksPerSecond;
        [SerializeField] private int _defenseHp;
        [SerializeField] private EnemyRow[] _enemies;
        [SerializeField] private UnitRow[] _units;
        [SerializeField] private int[] _stackHpPercents;
        [SerializeField] private int[] _stackAttackPercents;
        [SerializeField] private int[] _stackBlockBonus;
        [SerializeField] private int _stackCostPercent;
        [SerializeField] private float _redeployCooldownSeconds;
        [SerializeField] private int _redeployCostIncrease;
        [SerializeField] private int _retreatRefundPercent;
        [SerializeField] private int _twoStarHpPercent;
        [SerializeField] private int _deckSize;
        [SerializeField] private int _dragSlowPercent;
        [SerializeField] private float _firstSpawnDelaySeconds;
        [SerializeField] private int _gachaSingleCost;
        [SerializeField] private int _gachaMultiCount;
        [SerializeField] private int _gachaMultiCost;
        [SerializeField] private int _gachaDuplicateRefund;
        [SerializeField] private RarityWeightRow[] _gachaRarityWeights;
        [SerializeField] private int _currencyPerStar;
        [SerializeField] private int _startingCurrency;
        [SerializeField] private string[] _startingUnits;

        public int StartCost => _startCost;
        public float CostRegenSeconds => _costRegenSeconds;
        public int MaxCost => _maxCost;
        public int TicksPerSecond => _ticksPerSecond;

        /// <summary>카드를 맵 칸 위로 끄는 동안의 시간 비율(%). BALANCE 2-2.</summary>
        public int DragSlowPercent => _dragSlowPercent;

        /// <summary>판 시작 뒤 적이 나오기까지의 준비 시간(초). 모든 스테이지 등장 표에 더한다. BALANCE 4-0.</summary>
        public float FirstSpawnDelaySeconds => _firstSpawnDelaySeconds;
        public int DefenseHp => _defenseHp;
        public EnemyRow[] Enemies => _enemies;
        public UnitRow[] Units => _units;

        /// <summary>중첩 단계별 HP 배율(퍼센트). 칸 0이 1단계, 길이가 최대 단계다.</summary>
        public int[] StackHpPercents => _stackHpPercents;
        public int[] StackAttackPercents => _stackAttackPercents;
        public int[] StackBlockBonus => _stackBlockBonus;
        public int StackCostPercent => _stackCostPercent;
        public float RedeployCooldownSeconds => _redeployCooldownSeconds;
        public int RedeployCostIncrease => _redeployCostIncrease;
        public int RetreatRefundPercent => _retreatRefundPercent;

        /// <summary>별 2개 기준: 남은 디펜스 HP가 최대의 이 퍼센트 이상 (BALANCE 8번).</summary>
        public int TwoStarHpPercent => _twoStarHpPercent;

        /// <summary>한 판에 출전하는 능력자 수 (BALANCE 9번). 능력자 목록 순서의 앞 이만큼이 기본 편성이다.</summary>
        public int DeckSize => _deckSize;

        /// <summary>뽑기 1회 · 여러 회 값 (BALANCE 10번).</summary>
        public int GachaSingleCost => _gachaSingleCost;
        public int GachaMultiCount => _gachaMultiCount;
        public int GachaMultiCost => _gachaMultiCost;

        /// <summary>이미 가진 능력자가 나오면 돌려주는 재화.</summary>
        public int GachaDuplicateRefund => _gachaDuplicateRefund;
        public RarityWeightRow[] GachaRarityWeights => _gachaRarityWeights;

        /// <summary>승리하면 별 하나에 받는 재화.</summary>
        public int CurrencyPerStar => _currencyPerStar;

        /// <summary>처음 시작할 때(저장 파일이 없을 때) 가진 재화와 능력자.</summary>
        public int StartingCurrency => _startingCurrency;
        public string[] StartingUnits => _startingUnits;
    }
}
