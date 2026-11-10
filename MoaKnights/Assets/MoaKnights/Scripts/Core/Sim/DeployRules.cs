using System;
using System.Collections.Generic;

namespace MoaKnights.Core.Sim
{
    /// <summary>
    /// 중첩 · 퇴각 · 재배치 규칙. 배율은 정수 퍼센트(100 = 그대로)이고 계산은 내림이다.
    /// 단계는 1부터 MaxStage까지. 배열 칸 0이 1단계다.
    /// </summary>
    public sealed class DeployRules
    {
        private readonly int[] _hpPercent;
        private readonly int[] _attackPercent;
        private readonly int[] _blockBonus;

        public int MaxStage => _hpPercent.Length;

        /// <summary>한 번 겹칠 때 드는 코스트 = 기본 코스트 x 이 퍼센트 / 100.</summary>
        public int StackCostPercent { get; }

        /// <summary>퇴각 · 쓰러짐 뒤 같은 능력자를 다시 놓을 수 있을 때까지의 틱.</summary>
        public int RedeployCooldownTicks { get; }

        /// <summary>다시 놓을 때마다 배치 코스트에 더해지는 값.</summary>
        public int RedeployCostIncrease { get; }

        /// <summary>직접 퇴각 때 돌려받는 비율 = 들인 코스트 합 x 이 퍼센트 / 100 (내림).</summary>
        public int RetreatRefundPercent { get; }

        public DeployRules(
            IReadOnlyList<int> hpPercent,
            IReadOnlyList<int> attackPercent,
            IReadOnlyList<int> blockBonus,
            int stackCostPercent,
            int redeployCooldownTicks,
            int redeployCostIncrease,
            int retreatRefundPercent)
        {
            if (hpPercent == null || hpPercent.Count == 0) throw new ArgumentException("Stack table is empty.");
            if (attackPercent == null || attackPercent.Count != hpPercent.Count || blockBonus == null || blockBonus.Count != hpPercent.Count)
                throw new ArgumentException("Stack tables must have the same length.");
            if (stackCostPercent < 0) throw new ArgumentOutOfRangeException(nameof(stackCostPercent));
            if (redeployCooldownTicks < 0) throw new ArgumentOutOfRangeException(nameof(redeployCooldownTicks));
            if (redeployCostIncrease < 0) throw new ArgumentOutOfRangeException(nameof(redeployCostIncrease));
            if (retreatRefundPercent < 0 || retreatRefundPercent > 100) throw new ArgumentOutOfRangeException(nameof(retreatRefundPercent));
            foreach (var p in hpPercent) if (p <= 0) throw new ArgumentOutOfRangeException(nameof(hpPercent));

            _hpPercent = Copy(hpPercent);
            _attackPercent = Copy(attackPercent);
            _blockBonus = Copy(blockBonus);
            StackCostPercent = stackCostPercent;
            RedeployCooldownTicks = redeployCooldownTicks;
            RedeployCostIncrease = redeployCostIncrease;
            RetreatRefundPercent = retreatRefundPercent;
        }

        public int MaxHp(UnitDefinition unit, int stage) => Math.Max(1, unit.Stats.MaxHp * _hpPercent[Index(stage)] / 100);

        public int Attack(UnitDefinition unit, int stage) => unit.Stats.Attack * _attackPercent[Index(stage)] / 100;

        public int BlockCount(UnitDefinition unit, int stage) => Math.Max(0, unit.BlockCount + _blockBonus[Index(stage)]);

        public int StackCost(UnitDefinition unit) => unit.Cost * StackCostPercent / 100;

        /// <summary>이 능력자를 이번에 놓는 코스트. deployments는 이전에 놓은 횟수.</summary>
        public int DeployCost(UnitDefinition unit, int deployments) => unit.Cost + RedeployCostIncrease * deployments;

        public int Refund(int spentCost) => spentCost * RetreatRefundPercent / 100;

        private int Index(int stage)
        {
            if (stage < 1 || stage > MaxStage) throw new ArgumentOutOfRangeException(nameof(stage));
            return stage - 1;
        }

        private static int[] Copy(IReadOnlyList<int> source)
        {
            var copy = new int[source.Count];
            for (var i = 0; i < copy.Length; i++) copy[i] = source[i];
            return copy;
        }
    }
}
