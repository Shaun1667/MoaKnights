using System.Collections.Generic;
using System.Linq;
using MoaKnights.Core.Map;
using MoaKnights.Core.Sim;
using NUnit.Framework;

namespace MoaKnights.Tests
{
    public sealed class HealerTests
    {
        private const int Tps = 30;
        private const string EnemyId = "e";
        private const string Tank = "tank";
        private const string Melee = "melee";
        private const string Healer = "healer";

        // y=1: H H H H H
        // y=0: S . . . E   (길 0,0 → 4,0)
        private static readonly string[] Map = { "HHHHH", "S...E" };

        private static Simulation Build(int healAmount = 15, int enemyAttack = 20, long[] spawns = null, SkillSet healerSkills = null)
        {
            var map = GridMap.Parse(Map);
            var definition = new BattleDefinition(
                map, Tps, 10,
                new[] { new EnemyDefinition(EnemyId, 100, 1, new CombatStats(100000, enemyAttack, 30)) },
                new[] { EnemyRoute.Parse(map, "0,0 4,0") },
                (spawns ?? new long[0]).Select(t => new SpawnEvent(t, EnemyId, 0)),
                new CostRules(99, 99, 1000000),
                new[]
                {
                    new UnitDefinition(Tank, 10, UnitTerrain.Ground, new CombatStats(200, 0, 30), 2, AttackRange.Parse(new[] { "@" }), TestRules.NoSkills(), UnitRole.Tank),
                    new UnitDefinition(Melee, 10, UnitTerrain.Ground, new CombatStats(100, 0, 30), 1, AttackRange.Parse(new[] { "@" }), TestRules.NoSkills(), UnitRole.Dealer),
                    new UnitDefinition(Healer, 10, UnitTerrain.Hill, new CombatStats(50, healAmount, 30), 0, AttackRange.Parse(new[] { "XXX", "X@X", "XXX" }),
                        healerSkills ?? new SkillSet(SpType.OnAttack, 3, 0, new[] { new SkillDefinition(0, 100, 0, 60), new SkillDefinition(0, 100, 0, 60), new SkillDefinition(0, 100, 0, 60) }),
                        UnitRole.Healer),
                },
                TestRules.Deploy,
                TestRules.TwoStarHpPercent);
            return new Simulation(World.Create(1, "s", 10, 99), definition);
        }

        private static void Run(Simulation s, int steps)
        {
            for (var i = 0; i < steps; i++) s.Step();
        }

        private static Unit Of(Simulation s, string id) => s.World.UnitOf(id);

        [Test]
        public void Healer_HealsInjuredAllyInRange()
        {
            // 탱커 (2,0)이 적을 막고 맞는다. 힐러는 바로 위 (2,1)에서 둘레 3x3을 치유한다.
            var s = Build(spawns: new long[] { 0 });
            s.TryPlaceUnit(Tank, new GridPos(2, 0), Direction.Left);
            s.TryPlaceUnit(Healer, new GridPos(2, 1), Direction.Down);
            var healed = new List<(int id, int amount)>();
            s.UnitHealed += (u, h) => healed.Add((u.Id, h));

            Run(s, 100);
            Assert.IsNotEmpty(healed);
            Assert.IsTrue(healed.All(h => h.id == Of(s, Tank).Id));
            Assert.IsTrue(healed.All(h => h.amount <= 15));
        }

        [Test]
        public void Healer_DoesNothing_WhenNobodyIsHurt_AndKeepsCooldownReady()
        {
            var s = Build();
            s.TryPlaceUnit(Tank, new GridPos(2, 0), Direction.Left);
            s.TryPlaceUnit(Healer, new GridPos(2, 1), Direction.Down);
            var healed = 0;
            s.UnitHealed += (u, h) => healed++;
            Run(s, 100);
            Assert.AreEqual(0, healed);
            Assert.AreEqual(0, Of(s, Healer).AttackCooldown);
            Assert.AreEqual(0, Of(s, Healer).Sp);
        }

        [Test]
        public void Healer_NeverHealsAboveMax()
        {
            // 공격 5짜리 적이 한 대 치면 탱커가 195. 치유 15라도 200까지만 오른다.
            var s = Build(healAmount: 15, enemyAttack: 5, spawns: new long[] { 0 });
            s.TryPlaceUnit(Tank, new GridPos(2, 0), Direction.Left);
            s.TryPlaceUnit(Healer, new GridPos(2, 1), Direction.Down);
            var healed = new List<int>();
            s.UnitHealed += (u, h) => healed.Add(h);
            Run(s, 200);
            Assert.IsNotEmpty(healed);
            Assert.IsTrue(healed.All(h => h == 5));
            Assert.That(Of(s, Tank).Hp, Is.LessThanOrEqualTo(200));
        }

        [Test]
        public void Healer_PicksLowestHpRatio()
        {
            // 탱커는 막고 맞고(200 중), 근거리도 바로 옆에서 나중에 맞는다. 비율이 낮은 쪽을 먼저 치유한다.
            var s = Build(healAmount: 1, enemyAttack: 30, spawns: new long[] { 0, 2 });
            s.TryPlaceUnit(Melee, new GridPos(1, 0), Direction.Left);
            s.TryPlaceUnit(Tank, new GridPos(2, 0), Direction.Left);
            s.TryPlaceUnit(Healer, new GridPos(2, 1), Direction.Down);

            // 치유가 일어나는 순간(이벤트 안)에 두 아군의 치유 전 비율을 비교한다.
            var checks = 0;
            s.UnitHealed += (target, amount) =>
            {
                var melee = Of(s, Melee);
                var tank = Of(s, Tank);
                if (melee == null || tank == null) return;
                var meleeBefore = (melee.Hp - (target == melee ? amount : 0)) / (double)s.MaxHp(melee);
                var tankBefore = (tank.Hp - (target == tank ? amount : 0)) / (double)s.MaxHp(tank);
                if (meleeBefore == tankBefore || meleeBefore >= 1 || tankBefore >= 1) return;
                Assert.AreSame(meleeBefore < tankBefore ? melee : tank, target);
                checks++;
            };

            Run(s, 300);
            Assert.Greater(checks, 0, "no heal happened while both allies were hurt with different HP ratios");
        }

        [Test]
        public void Healer_DoesNotAttackEnemies()
        {
            var s = Build(spawns: new long[] { 0 });
            s.TryPlaceUnit(Healer, new GridPos(1, 1), Direction.Down);
            var hits = 0;
            s.EnemyDamaged += (e, d) => hits++;
            Run(s, 200);
            Assert.AreEqual(0, hits);
        }

        [Test]
        public void Healer_GainsAttackSp_FromHeals()
        {
            var s = Build(spawns: new long[] { 0 });
            s.TryPlaceUnit(Tank, new GridPos(2, 0), Direction.Left);
            s.TryPlaceUnit(Healer, new GridPos(2, 1), Direction.Down);
            var heals = 0;
            s.UnitHealed += (u, h) => heals++;
            Run(s, 150);
            Assert.Greater(heals, 0);
            Assert.AreEqual(System.Math.Min(heals, 3), Of(s, Healer).Sp);
        }

        [Test]
        public void HealerSkill_AttackBonus_RaisesHealAmount()
        {
            // 스킬 +100% → 치유 15가 30. 적 공격 40으로 깊게 다치게 한다.
            var s = Build(healAmount: 15, enemyAttack: 40, spawns: new long[] { 0 });
            s.TryPlaceUnit(Tank, new GridPos(2, 0), Direction.Left);
            s.TryPlaceUnit(Healer, new GridPos(2, 1), Direction.Down);
            var amounts = new List<int>();
            s.UnitHealed += (u, h) => amounts.Add(h);

            var healer = Of(s, Healer);
            for (var i = 0; i < 400 && s.CheckSkill(healer.Id) != PlacementResult.Ok; i++) s.Step();
            Assert.AreEqual(PlacementResult.Ok, s.TryUseSkill(healer.Id));
            Assert.AreEqual(30, s.AttackOf(healer));

            amounts.Clear();
            Run(s, 59);
            Assert.IsTrue(amounts.Contains(30));
        }

        [Test]
        public void Definition_KeepsRole()
        {
            var s = Build();
            s.Definition.TryGetUnit(Healer, out var healer);
            s.Definition.TryGetUnit(Tank, out var tank);
            Assert.AreEqual(UnitRole.Healer, healer.Role);
            Assert.AreEqual(UnitRole.Tank, tank.Role);
        }
    }
}
