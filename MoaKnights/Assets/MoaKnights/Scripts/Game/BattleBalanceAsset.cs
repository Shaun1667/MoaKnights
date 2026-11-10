using System;
using MoaKnights.Core.Sim;
using UnityEngine;

namespace MoaKnights.Game
{
    /// <summary>전투 수치 한 곳. 표는 BALANCE.md 1 · 2 · 3 · 5번, 값은 에셋에 둔다. 공격 범위 글자는 AttackRange 설명을 따른다.</summary>
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
        }

        [SerializeField] private int _startCost;
        [SerializeField] private float _costRegenSeconds;
        [SerializeField] private int _maxCost;
        [SerializeField] private int _ticksPerSecond;
        [SerializeField] private int _defenseHp;
        [SerializeField] private EnemyRow[] _enemies;
        [SerializeField] private UnitRow[] _units;

        public int StartCost => _startCost;
        public float CostRegenSeconds => _costRegenSeconds;
        public int MaxCost => _maxCost;
        public int TicksPerSecond => _ticksPerSecond;
        public int DefenseHp => _defenseHp;
        public EnemyRow[] Enemies => _enemies;
        public UnitRow[] Units => _units;
    }
}
