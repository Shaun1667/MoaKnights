using UnityEngine;

namespace MoaKnights.Game
{
    /// <summary>전투 수치 한 곳. 표는 BALANCE.md 1 · 2번, 값은 에셋에 둔다.</summary>
    [CreateAssetMenu(fileName = "BattleBalance", menuName = "MoaKnights/Battle Balance")]
    public sealed class BattleBalanceAsset : ScriptableObject
    {
        [SerializeField] private int _startCost;
        [SerializeField] private float _costRegenSeconds;
        [SerializeField] private int _maxCost;
        [SerializeField] private int _ticksPerSecond;

        public int StartCost => _startCost;
        public float CostRegenSeconds => _costRegenSeconds;
        public int MaxCost => _maxCost;
        public int TicksPerSecond => _ticksPerSecond;
    }
}
