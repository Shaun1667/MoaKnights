using System.Collections.Generic;
using System.Linq;
using MoaKnights.Core.Map;
using MoaKnights.Core.Sim;
using UnityEngine;

namespace MoaKnights.Game
{
    /// <summary>데이터 에셋을 규칙 입력으로 바꾼다. 초 단위 값은 여기서 틱과 정수로 바뀐다.</summary>
    public static class BattleDefinitionFactory
    {
        private const float SpeedPerTickPerCellPerSecond = 100f;

        /// <summary>기본 편성: 능력자 목록 순서의 앞 DeckSize명.</summary>
        public static string[] DefaultDeck(BattleBalanceAsset balance) =>
            balance.Units.Take(balance.DeckSize).Select(u => u.Id).ToArray();

        /// <summary>편성이 맞는지: 1명 이상, DeckSize 이하, 모두 목록에 있고 겹치지 않음.</summary>
        public static bool IsValidDeck(BattleBalanceAsset balance, IReadOnlyList<string> deck)
        {
            if (deck == null || deck.Count == 0 || deck.Count > balance.DeckSize) return false;
            if (deck.Distinct().Count() != deck.Count) return false;
            return deck.All(id => balance.Units.Any(u => u.Id == id));
        }

        /// <summary>기본 편성으로 판을 만든다.</summary>
        public static BattleDefinition Create(StageAsset stage, BattleBalanceAsset balance) => Create(stage, balance, DefaultDeck(balance));

        /// <summary>판을 만든다. 출전 능력자는 deck 순서대로 카드가 된다. 맞지 않는 편성이면 기본 편성으로 바꾼다.</summary>
        public static BattleDefinition Create(StageAsset stage, BattleBalanceAsset balance, IReadOnlyList<string> deck)
        {
            if (!IsValidDeck(balance, deck)) deck = DefaultDeck(balance);

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
            foreach (var id in deck)
            {
                var row = balance.Units.First(u => u.Id == id);
                var stats = new CombatStats(row.MaxHp, row.Attack, Ticks(row.AttackIntervalSeconds, ticksPerSecond));
                var skills = new List<SkillDefinition>();
                foreach (var skill in row.Skills)
                    skills.Add(new SkillDefinition(skill.HealPercent, skill.AttackBonusPercent, skill.BlockBonus, Ticks(skill.DurationSeconds, ticksPerSecond)));
                var naturalTicks = row.SpType == SpType.Natural ? Ticks(row.NaturalSpSeconds, ticksPerSecond) : 0;
                var skillSet = new SkillSet(row.SpType, row.MaxSp, naturalTicks, skills);

                units.Add(new UnitDefinition(row.Id, row.Cost, row.Terrain, stats, row.BlockCount, AttackRange.Parse(row.Range), skillSet, row.Role));
            }

            var deploy = new DeployRules(
                balance.StackHpPercents,
                balance.StackAttackPercents,
                balance.StackBlockBonus,
                balance.StackCostPercent,
                Ticks(balance.RedeployCooldownSeconds, ticksPerSecond),
                balance.RedeployCostIncrease,
                balance.RetreatRefundPercent);

            return new BattleDefinition(map, ticksPerSecond, balance.DefenseHp, enemies, routes, spawns, cost, units, deploy, balance.TwoStarHpPercent);
        }

        private static int Ticks(float seconds, int ticksPerSecond) => Mathf.RoundToInt(seconds * ticksPerSecond);
    }
}
