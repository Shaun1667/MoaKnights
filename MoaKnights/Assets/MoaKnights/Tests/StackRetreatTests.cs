using System.Collections.Generic;
using System.Linq;
using MoaKnights.Core.Map;
using MoaKnights.Core.Sim;
using NUnit.Framework;

namespace MoaKnights.Tests
{
    public sealed class StackRetreatTests
    {
        private const string Melee = "melee";
        private const string Ranged = "ranged";
        private const string EnemyId = "e";
        private const int Cooldown = 300;   // TestRules.Deploy

        // y=1: H H H H H
        // y=0: S . . . E
        private static readonly string[] Map = { "HHHHH", "S...E" };

        private static Simulation Build(int startCost = 99, long[] spawns = null, int enemyAttack = 0)
        {
            var map = GridMap.Parse(Map);
            var definition = new BattleDefinition(
                map, 30, 10,
                new[] { new EnemyDefinition(EnemyId, 100, 1, new CombatStats(1000, enemyAttack, 45)) },
                new[] { EnemyRoute.Parse(map, "0,0 4,0") },
                (spawns ?? new long[0]).Select(t => new SpawnEvent(t, EnemyId, 0)),
                new CostRules(startCost, 99, 1000000),
                new[]
                {
                    new UnitDefinition(Melee, 10, UnitTerrain.Ground, new CombatStats(100, 20, 30), 2, AttackRange.Parse(new[] { "@X" }), TestRules.NoSkills(), UnitRole.Dealer),
                    new UnitDefinition(Ranged, 12, UnitTerrain.Hill, new CombatStats(60, 15, 36), 0, AttackRange.Parse(new[] { "OX" }), TestRules.NoSkills(), UnitRole.Dealer),
                },
                TestRules.Deploy, TestRules.TwoStarHpPercent);
            return new Simulation(World.Create(1, "s", 10, startCost), definition);
        }

        private static void Run(Simulation simulation, int steps)
        {
            for (var i = 0; i < steps; i++) simulation.Step();
        }

        private static Unit Melee1(Simulation s) => s.World.UnitOf(Melee);

        // ---- 중첩 ----

        [Test]
        public void Place_StartsAtStageOne_AndRecordsSpentCost()
        {
            var s = Build();
            s.TryPlaceUnit(Melee, new GridPos(2, 0), Direction.Left);
            Assert.AreEqual(1, Melee1(s).Stage);
            Assert.AreEqual(10, Melee1(s).SpentCost);
            Assert.AreEqual(100, Melee1(s).Hp);
        }

        [Test]
        public void Stack_RaisesStage_StatsAndCost()
        {
            var s = Build();
            s.TryPlaceUnit(Melee, new GridPos(2, 0), Direction.Left);
            var stacked = new List<int>();
            s.UnitStacked += u => stacked.Add(u.Stage);

            Assert.AreEqual(PlacementResult.Ok, s.TryStackUnit(Melee));
            Assert.AreEqual(2, Melee1(s).Stage);
            Assert.AreEqual(130, s.MaxHp(Melee1(s)));
            Assert.AreEqual(130, Melee1(s).Hp);
            Assert.AreEqual(20, Melee1(s).SpentCost);
            Assert.AreEqual(79, s.World.Cost);

            Assert.AreEqual(PlacementResult.Ok, s.TryStackUnit(Melee));
            Assert.AreEqual(3, Melee1(s).Stage);
            Assert.AreEqual(160, s.MaxHp(Melee1(s)));
            Assert.AreEqual(30, Melee1(s).SpentCost);
            CollectionAssert.AreEqual(new[] { 2, 3 }, stacked);
        }

        [Test]
        public void Stack_KeepsCellDirectionAndId()
        {
            var s = Build();
            s.TryPlaceUnit(Melee, new GridPos(2, 0), Direction.Up);
            var before = Melee1(s);
            s.TryStackUnit(Melee);
            Assert.AreSame(before, Melee1(s));
            Assert.AreEqual(new GridPos(2, 0), Melee1(s).Cell);
            Assert.AreEqual(Direction.Up, Melee1(s).Direction);
            Assert.AreEqual(1, s.World.Units.Count);
        }

        [Test]
        public void Stack_KeepsDamageTaken()
        {
            // 적(공격 30)이 막히자마자 한 대 친 뒤에 겹친다.
            var s = Build(spawns: new long[] { 0 }, enemyAttack: 30);
            s.TryPlaceUnit(Melee, new GridPos(2, 0), Direction.Right);
            Run(s, 50);
            Assert.AreEqual(70, Melee1(s).Hp);

            s.TryStackUnit(Melee);
            Assert.AreEqual(100, Melee1(s).Hp);   // 최대 HP가 30 올라 지금 HP도 30 오름
            Assert.AreEqual(130, s.MaxHp(Melee1(s)));
        }

        [Test]
        public void Stack_StopsAtMaxStage()
        {
            var s = Build();
            s.TryPlaceUnit(Melee, new GridPos(2, 0), Direction.Left);
            s.TryStackUnit(Melee);
            s.TryStackUnit(Melee);
            var cost = s.World.Cost;
            Assert.AreEqual(PlacementResult.MaxStackReached, s.TryStackUnit(Melee));
            Assert.AreEqual(3, Melee1(s).Stage);
            Assert.AreEqual(cost, s.World.Cost);
        }

        [Test]
        public void Stack_Rejects_NotDeployed_AndNotEnoughCost()
        {
            var s = Build(startCost: 15);
            Assert.AreEqual(PlacementResult.NotDeployed, s.TryStackUnit(Melee));
            s.TryPlaceUnit(Melee, new GridPos(2, 0), Direction.Left);
            Assert.AreEqual(PlacementResult.NotEnoughCost, s.TryStackUnit(Melee));
            Assert.AreEqual(1, Melee1(s).Stage);
            Assert.AreEqual(5, s.World.Cost);
        }

        [Test]
        public void Stack_ThirdStage_AddsBlock()
        {
            var s = Build(spawns: new long[] { 0, 5, 10, 15 });
            s.TryPlaceUnit(Melee, new GridPos(2, 0), Direction.Right);   // 출구 쪽을 봐서 막기만 한다
            s.TryStackUnit(Melee);
            s.TryStackUnit(Melee);
            Run(s, 200);
            Assert.AreEqual(3, s.World.BlockedCount(Melee1(s).Id));
        }

        [Test]
        public void Stack_RaisesAttack()
        {
            var s = Build(spawns: new long[] { 0 });
            s.TryPlaceUnit(Melee, new GridPos(2, 0), Direction.Left);
            s.TryStackUnit(Melee);
            var hits = new List<int>();
            s.EnemyDamaged += (e, d) => hits.Add(d);
            Run(s, 120);
            Assert.IsNotEmpty(hits);
            Assert.IsTrue(hits.All(d => d == 26));   // 20 x 130%
        }

        // ---- 퇴각 ----

        [Test]
        public void Retreat_RefundsHalfOfSpent_RoundedDown()
        {
            var s = Build();
            s.TryPlaceUnit(Ranged, new GridPos(2, 1), Direction.Down);   // 12
            s.TryStackUnit(Ranged);                                       // +12 = 24
            s.TryStackUnit(Ranged);                                       // +12 = 36
            var cost = s.World.Cost;
            var refunds = new List<int>();
            s.UnitRetreated += (u, r) => refunds.Add(r);

            Assert.AreEqual(PlacementResult.Ok, s.TryRetreat(s.World.UnitOf(Ranged).Id));
            CollectionAssert.AreEqual(new[] { 18 }, refunds);
            Assert.AreEqual(cost + 18, s.World.Cost);
            CollectionAssert.IsEmpty(s.World.Units);
        }

        [Test]
        public void Retreat_OddCost_RoundsDown()
        {
            var deploy = new DeployRules(new[] { 100 }, new[] { 100 }, new[] { 0 }, 100, 0, 0, 50);
            var map = GridMap.Parse(Map);
            var definition = new BattleDefinition(map, 30, 10, new EnemyDefinition[0], new EnemyRoute[0], new SpawnEvent[0],
                new CostRules(20, 99, 1000000),
                new[] { new UnitDefinition("odd", 7, UnitTerrain.Ground, new CombatStats(10, 1, 30), 1, AttackRange.Parse(new[] { "@" }), TestRules.NoSkills(1), UnitRole.Dealer) },
                deploy, TestRules.TwoStarHpPercent);
            var s = new Simulation(World.Create(1, "s", 10, 20), definition);
            s.TryPlaceUnit("odd", new GridPos(1, 0), Direction.Right);
            s.TryRetreat(s.World.UnitOf("odd").Id);
            Assert.AreEqual(13 + 3, s.World.Cost);   // 7의 절반은 3
        }

        [Test]
        public void Retreat_ReleasesBlockedEnemies()
        {
            var s = Build(spawns: new long[] { 0 });
            s.TryPlaceUnit(Melee, new GridPos(2, 0), Direction.Right);
            Run(s, 80);
            Assert.IsTrue(s.World.Enemies[0].IsBlocked);

            s.TryRetreat(Melee1(s).Id);
            Assert.IsFalse(s.World.Enemies[0].IsBlocked);
            var progress = s.World.Enemies[0].Progress;
            Run(s, 1);
            Assert.Greater(s.World.Enemies[0].Progress, progress);
        }

        [Test]
        public void Retreat_UnknownUnit_IsRejected()
        {
            Assert.AreEqual(PlacementResult.NotDeployed, Build().TryRetreat(12345));
        }

        // ---- 재배치 ----

        [Test]
        public void Redeploy_WaitsForCooldown()
        {
            var s = Build();
            s.TryPlaceUnit(Melee, new GridPos(2, 0), Direction.Left);
            s.TryRetreat(Melee1(s).Id);

            Assert.AreEqual(Cooldown, s.RedeployTicksLeft(Melee));
            Assert.AreEqual(PlacementResult.OnCooldown, s.TryPlaceUnit(Melee, new GridPos(2, 0), Direction.Left));

            Run(s, Cooldown - 1);
            Assert.AreEqual(PlacementResult.OnCooldown, s.CheckPlacement(Melee, new GridPos(2, 0)));
            Run(s, 1);
            Assert.AreEqual(0, s.RedeployTicksLeft(Melee));
            Assert.AreEqual(PlacementResult.Ok, s.TryPlaceUnit(Melee, new GridPos(2, 0), Direction.Left));
        }

        [Test]
        public void Redeploy_CostRisesEachTime_AndStageResets()
        {
            var s = Build();
            Assert.AreEqual(10, s.DeployCost(Melee));
            s.TryPlaceUnit(Melee, new GridPos(2, 0), Direction.Left);
            s.TryStackUnit(Melee);
            s.TryRetreat(Melee1(s).Id);
            Run(s, Cooldown);

            Assert.AreEqual(15, s.DeployCost(Melee));
            var cost = s.World.Cost;
            s.TryPlaceUnit(Melee, new GridPos(1, 0), Direction.Right);
            Assert.AreEqual(cost - 15, s.World.Cost);
            Assert.AreEqual(1, Melee1(s).Stage);
            Assert.AreEqual(15, Melee1(s).SpentCost);

            s.TryRetreat(Melee1(s).Id);
            Run(s, Cooldown);
            Assert.AreEqual(20, s.DeployCost(Melee));
        }

        [Test]
        public void Redeploy_DoesNotChangeStackCost()
        {
            var s = Build();
            s.TryPlaceUnit(Melee, new GridPos(2, 0), Direction.Left);
            s.TryRetreat(Melee1(s).Id);
            Run(s, Cooldown);
            s.TryPlaceUnit(Melee, new GridPos(2, 0), Direction.Left);
            var cost = s.World.Cost;
            s.TryStackUnit(Melee);
            Assert.AreEqual(cost - 10, s.World.Cost);
        }

        [Test]
        public void OtherUnit_IsNotAffectedByCooldown()
        {
            var s = Build();
            s.TryPlaceUnit(Melee, new GridPos(2, 0), Direction.Left);
            s.TryRetreat(Melee1(s).Id);
            Assert.AreEqual(PlacementResult.Ok, s.TryPlaceUnit(Ranged, new GridPos(2, 1), Direction.Down));
        }

        // ---- 결정성 · 스냅샷 ----

        [Test]
        public void Snapshot_WithStagesAndCooldowns_ContinuesIdentically()
        {
            var original = Build(spawns: new long[] { 0, 20, 40 });
            original.TryPlaceUnit(Melee, new GridPos(2, 0), Direction.Left);
            original.TryStackUnit(Melee);
            original.TryPlaceUnit(Ranged, new GridPos(3, 1), Direction.Down);
            Run(original, 60);
            original.TryRetreat(original.World.UnitOf(Ranged).Id);
            Run(original, 30);

            var restored = new Simulation(World.FromSnapshot(original.World.ToSnapshot()), original.Definition);
            Assert.AreEqual(original.RedeployTicksLeft(Ranged), restored.RedeployTicksLeft(Ranged));
            Assert.AreEqual(original.DeployCost(Ranged), restored.DeployCost(Ranged));
            Assert.AreEqual(2, restored.World.UnitOf(Melee).Stage);
            Assert.AreEqual(20, restored.World.UnitOf(Melee).SpentCost);

            Run(original, 400);
            Run(restored, 400);
            Assert.AreEqual(original.World.Cost, restored.World.Cost);
            Assert.AreEqual(original.World.DefenseHp, restored.World.DefenseHp);
            CollectionAssert.AreEqual(original.World.Units.Select(u => (u.Id, u.Hp, u.Stage, u.SpentCost)).ToArray(),
                restored.World.Units.Select(u => (u.Id, u.Hp, u.Stage, u.SpentCost)).ToArray());
            CollectionAssert.AreEqual(original.World.Enemies.Select(e => (e.Id, e.Hp, e.Progress, e.BlockedBy)).ToArray(),
                restored.World.Enemies.Select(e => (e.Id, e.Hp, e.Progress, e.BlockedBy)).ToArray());
        }

        [Test]
        public void DeployRules_RejectBadTables()
        {
            Assert.Throws<System.ArgumentException>(() => new DeployRules(new int[0], new int[0], new int[0], 100, 0, 0, 50));
            Assert.Throws<System.ArgumentException>(() => new DeployRules(new[] { 100, 130 }, new[] { 100 }, new[] { 0, 0 }, 100, 0, 0, 50));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new DeployRules(new[] { 100 }, new[] { 100 }, new[] { 0 }, 100, 0, 0, 150));
        }
    }
}
