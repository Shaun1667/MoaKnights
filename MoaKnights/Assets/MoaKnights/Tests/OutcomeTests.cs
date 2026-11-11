using System.Collections.Generic;
using System.Linq;
using MoaKnights.Core.Map;
using MoaKnights.Core.Sim;
using NUnit.Framework;

namespace MoaKnights.Tests
{
    public sealed class OutcomeTests
    {
        private const int Tps = 30;
        private const string EnemyId = "e";
        private const string Melee = "melee";

        // y=1: H H H H
        // y=0: S . . E   (길 0,0 → 3,0, 3칸 = 90틱)
        private static readonly string[] Map = { "HHHH", "S..E" };
        private const int RouteTicks = 3 * Tps;

        private static Simulation Build(int defenseHp, long[] spawns, int leakDamage = 1, int meleeAttack = 1000)
        {
            var map = GridMap.Parse(Map);
            var definition = new BattleDefinition(
                map, Tps, defenseHp,
                new[] { new EnemyDefinition(EnemyId, 100, leakDamage, new CombatStats(10, 0, 30)) },
                new[] { EnemyRoute.Parse(map, "0,0 3,0") },
                spawns.Select(t => new SpawnEvent(t, EnemyId, 0)),
                new CostRules(99, 99, 60),
                new[] { new UnitDefinition(Melee, 10, UnitTerrain.Ground, new CombatStats(100, meleeAttack, 30), 1, AttackRange.Parse(new[] { "@X" }), TestRules.NoSkills(), UnitRole.Dealer) },
                TestRules.Deploy,
                TestRules.TwoStarHpPercent);
            return new Simulation(World.Create(1, "s", defenseHp, 99), definition);
        }

        private static int RunUntilOver(Simulation simulation, int limit = 10000)
        {
            var steps = 0;
            while (!simulation.IsOver && steps < limit)
            {
                simulation.Step();
                steps++;
            }
            return steps;
        }

        /// <summary>디버그 바로 클리어: 판 위 적은 쓰러짐 이벤트로 사라지고, 남은 등장은 건너뛰고, 그 자리에서 승리.</summary>
        [Test]
        public void DebugClearAll_KillsEnemies_SkipsSpawns_AndWins()
        {
            var s = Build(10, new long[] { 0, 10, 500, 900 });
            for (var i = 0; i < 20; i++) s.Step();
            Assert.AreEqual(2, s.World.Enemies.Count);

            var killed = 0;
            var ended = new List<BattleOutcome>();
            s.EnemyKilled += e => killed++;
            s.BattleEnded += ended.Add;
            var tick = s.World.Tick;

            s.DebugClearAll();
            Assert.AreEqual(2, killed);
            CollectionAssert.IsEmpty(s.World.Enemies);
            Assert.AreEqual(4, s.World.NextSpawnIndex);
            Assert.AreEqual(BattleOutcome.Victory, s.World.Outcome);
            CollectionAssert.AreEqual(new[] { BattleOutcome.Victory }, ended);
            Assert.AreEqual(tick, s.World.Tick);
            Assert.AreEqual(3, s.Stars());
        }

        [Test]
        public void DebugClearAll_StarsFollowCurrentHp_AndDoesNothingAfterEnd()
        {
            var s = Build(10, new long[] { 0 });
            s.World.DamageDefense(6);
            s.DebugClearAll();
            Assert.AreEqual(BattleOutcome.Victory, s.World.Outcome);
            Assert.AreEqual(1, s.Stars());

            var ended = 0;
            s.BattleEnded += o => ended++;
            s.DebugClearAll();
            Assert.AreEqual(0, ended);
        }

        [Test]
        public void AllLeak_DefenseZero_IsDefeat_WithZeroStars()
        {
            var s = Build(2, new long[] { 0, 10 });
            var ended = new List<BattleOutcome>();
            s.BattleEnded += ended.Add;

            RunUntilOver(s);
            Assert.AreEqual(BattleOutcome.Defeat, s.World.Outcome);
            CollectionAssert.AreEqual(new[] { BattleOutcome.Defeat }, ended);
            Assert.AreEqual(0, s.Stars());
        }

        [Test]
        public void AllKilled_NoLeak_IsVictory_WithThreeStars()
        {
            var s = Build(10, new long[] { 0, 30, 60 });
            s.TryPlaceUnit(Melee, new GridPos(1, 0), Direction.Left);
            RunUntilOver(s);
            Assert.AreEqual(BattleOutcome.Victory, s.World.Outcome);
            Assert.AreEqual(10, s.World.DefenseHp);
            Assert.AreEqual(3, s.Stars());
        }

        [Test]
        public void SomeLeak_HpAtLeastHalf_IsTwoStars()
        {
            var s = Build(10, new long[] { 0, 10, 20 }, leakDamage: 2);   // 셋 다 새면 HP 4 → 패배 아님, 1개
            RunUntilOver(s);
            Assert.AreEqual(BattleOutcome.Victory, s.World.Outcome);
            Assert.AreEqual(4, s.World.DefenseHp);
            Assert.AreEqual(1, s.Stars());

            var two = Build(10, new long[] { 0, 10 }, leakDamage: 2);    // 둘 새면 HP 6 → 50% 이상, 2개
            RunUntilOver(two);
            Assert.AreEqual(6, two.World.DefenseHp);
            Assert.AreEqual(2, two.Stars());
        }

        [Test]
        public void ExactlyHalfHp_IsTwoStars()
        {
            var s = Build(10, new long[] { 0 }, leakDamage: 5);
            RunUntilOver(s);
            Assert.AreEqual(5, s.World.DefenseHp);
            Assert.AreEqual(2, s.Stars());
        }

        [Test]
        public void Victory_WaitsUntilAllSpawnsAreOut()
        {
            // 첫 적이 금방 죽어도 뒤에 나올 적이 남아 있으면 끝나지 않는다.
            var s = Build(10, new long[] { 0, 300 });
            s.TryPlaceUnit(Melee, new GridPos(1, 0), Direction.Left);
            for (var i = 0; i < 200; i++) s.Step();
            Assert.IsFalse(s.IsOver);
            Assert.AreEqual(0, s.World.Enemies.Count);
            RunUntilOver(s);
            Assert.AreEqual(BattleOutcome.Victory, s.World.Outcome);
        }

        [Test]
        public void NoSpawns_NeverEnds()
        {
            var s = Build(10, new long[0]);
            for (var i = 0; i < 500; i++) s.Step();
            Assert.IsFalse(s.IsOver);
            Assert.AreEqual(500L, s.World.Tick);
        }

        [Test]
        public void AfterEnd_TimeStops_AndEndedEventFiresOnce()
        {
            var s = Build(1, new long[] { 0 });
            var ended = 0;
            s.BattleEnded += o => ended++;
            RunUntilOver(s);
            var tick = s.World.Tick;
            var cost = s.World.Cost;

            for (var i = 0; i < 100; i++) s.Step();
            Assert.AreEqual(tick, s.World.Tick);
            Assert.AreEqual(cost, s.World.Cost);
            Assert.AreEqual(1, ended);
        }

        [Test]
        public void AfterEnd_PlayerCommandsAreRejected()
        {
            var s = Build(10, new long[] { 0 });
            s.TryPlaceUnit(Melee, new GridPos(1, 0), Direction.Left);
            RunUntilOver(s);
            var unit = s.World.UnitOf(Melee);

            Assert.AreEqual(PlacementResult.BattleOver, s.CheckPlacement(Melee, new GridPos(2, 0)));
            Assert.AreEqual(PlacementResult.BattleOver, s.TryStackUnit(Melee));
            Assert.AreEqual(PlacementResult.BattleOver, s.TryRetreat(unit.Id));
            Assert.AreEqual(PlacementResult.BattleOver, s.TryUseSkill(unit.Id));
            Assert.AreEqual(1, s.World.Units.Count);
        }

        [Test]
        public void EndsOnTheTickTheLastEnemyLeaks()
        {
            // 한 마리: 나온 틱 0, 이동 90틱 → 틱 90에서 새고 그 틱에 승리.
            var s = Build(10, new long[] { 0 });
            var steps = RunUntilOver(s);
            Assert.AreEqual(RouteTicks + 1, steps);
            Assert.AreEqual(BattleOutcome.Victory, s.World.Outcome);
            Assert.AreEqual(9, s.World.DefenseHp);
        }

        [Test]
        public void Snapshot_KeepsOutcome()
        {
            var s = Build(1, new long[] { 0 });
            RunUntilOver(s);
            var restored = new Simulation(World.FromSnapshot(s.World.ToSnapshot()), s.Definition);
            Assert.AreEqual(BattleOutcome.Defeat, restored.World.Outcome);
            Assert.IsTrue(restored.IsOver);
            var tick = restored.World.Tick;
            restored.Step();
            Assert.AreEqual(tick, restored.World.Tick);
        }

        [Test]
        public void Definition_RejectsBadStarRule()
        {
            var map = GridMap.Parse(Map);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new BattleDefinition(map, Tps, 10, new EnemyDefinition[0], new EnemyRoute[0], new SpawnEvent[0],
                new CostRules(10, 99, 60), new UnitDefinition[0], TestRules.Deploy, 150));
        }
    }
}
