using System;
using MoaKnights.Core.Sim;
using UnityEngine;

namespace MoaKnights.Game
{
    /// <summary>전투 수치 한 곳. 표는 BALANCE.md 1 · 2 · 3 · 5 · 6 · 7번, 값은 에셋에 둔다. 공격 범위 글자는 AttackRange 설명을 따른다.</summary>
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
            public SpType SpType;
            public int MaxSp;
            public float NaturalSpSeconds;
            public SkillRow[] Skills;
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

        public int StartCost => _startCost;
        public float CostRegenSeconds => _costRegenSeconds;
        public int MaxCost => _maxCost;
        public int TicksPerSecond => _ticksPerSecond;
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
    }
}
