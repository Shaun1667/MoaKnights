using System.Collections.Generic;
using MoaKnights.Core.Map;
using MoaKnights.Core.Sim;
using UnityEngine;

namespace MoaKnights.Game
{
    /// <summary>데이터 에셋을 규칙 입력으로 바꾼다. 초 단위 값은 여기서 틱과 정수로 바뀐다.</summary>
    public static class BattleDefinitionFactory
    {
        private const float SpeedPerTickPerCellPerSecond = 100f;

        public static BattleDefinition Create(StageAsset stage, BattleBalanceAsset balance)
        {
            var map = GridMap.Parse(stage.MapRows);
            var ticksPerSecond = balance.TicksPerSecond;

            var enemies = new List<EnemyDefinition>();
            foreach (var row in balance.Enemies)
            {
                var speed = Mathf.RoundToInt(row.SpeedCellsPerSecond * SpeedPerTickPerCellPerSecond);
                var stats = new CombatStats(row.MaxHp, row.Attack, Ticks(row.AttackIntervalSeconds, ticksPerSecond));
                enemies.Add(new EnemyDefinition(row.Id, speed, row.LeakDamage, stats));
            }

            var routes = new List<EnemyRoute>();
            foreach (var corners in stage.Routes) routes.Add(EnemyRoute.Parse(map, corners));

            var spawns = new List<SpawnEvent>();
            foreach (var group in stage.SpawnGroups)
            {
                for (var i = 0; i < group.Count; i++)
                {
                    var seconds = group.StartSeconds + group.IntervalSeconds * i;
                    spawns.Add(new SpawnEvent(Mathf.RoundToInt(seconds * ticksPerSecond), group.EnemyId, group.RouteIndex));
                }
            }

            var cost = new CostRules(
                balance.StartCost,
                balance.MaxCost,
                Mathf.RoundToInt(balance.CostRegenSeconds * ticksPerSecond));

            var units = new List<UnitDefinition>();
            foreach (var row in balance.Units)
            {
                var stats = new CombatStats(row.MaxHp, row.Attack, Ticks(row.AttackIntervalSeconds, ticksPerSecond));
                units.Add(new UnitDefinition(row.Id, row.Cost, row.Terrain, stats, row.BlockCount, AttackRange.Parse(row.Range)));
            }

            var deploy = new DeployRules(
                balance.StackHpPercents,
                balance.StackAttackPercents,
                balance.StackBlockBonus,
                balance.StackCostPercent,
                Ticks(balance.RedeployCooldownSeconds, ticksPerSecond),
                balance.RedeployCostIncrease,
                balance.RetreatRefundPercent);

            return new BattleDefinition(map, ticksPerSecond, balance.DefenseHp, enemies, routes, spawns, cost, units, deploy);
        }

        private static int Ticks(float seconds, int ticksPerSecond) => Mathf.RoundToInt(seconds * ticksPerSecond);
    }
}
