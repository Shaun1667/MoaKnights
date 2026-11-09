using System;
using UnityEngine;

namespace MoaKnights.Game
{
    /// <summary>전투 수치 한 곳. 표는 BALANCE.md 1 · 2 · 3번, 값은 에셋에 둔다.</summary>
    [CreateAssetMenu(fileName = "BattleBalance", menuName = "MoaKnights/Battle Balance")]
    public sealed class BattleBalanceAsset : ScriptableObject
    {
        [Serializable]
        public struct EnemyRow
        {
            public string Id;
            public float SpeedCellsPerSecond;
            public int LeakDamage;
        }

        [SerializeField] private int _startCost;
        [SerializeField] private float _costRegenSeconds;
        [SerializeField] private int _maxCost;
        [SerializeField] private int _ticksPerSecond;
        [SerializeField] private int _defenseHp;
        [SerializeField] private EnemyRow[] _enemies;

        public int StartCost => _startCost;
        public float CostRegenSeconds => _costRegenSeconds;
        public int MaxCost => _maxCost;
        public int TicksPerSecond => _ticksPerSecond;
        public int DefenseHp => _defenseHp;
        public EnemyRow[] Enemies => _enemies;
    }
}
