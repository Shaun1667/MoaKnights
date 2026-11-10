using System.Collections.Generic;
using System.Linq;
using MoaKnights.Core.Map;
using MoaKnights.Core.Sim;
using NUnit.Framework;

namespace MoaKnights.Tests
{
    public sealed class CombatTests
    {
        private const int Tps = 30;
        private const int OneCellPerSecond = 100;
        private const int UnitsPerCell = Tps * 100;
        private const string EnemyId = "e";
        private const string Melee = "melee";
        private const string Ranged = "ranged";

        // 일직선 길: 입구 (0,0) → 출구 (7,0). 위 줄은 언덕.
        private static readonly string[] Map =
        {
            "HHHHHHHH",
            "S......E",
        };

        private const string Route = "0,0 7,0";

        private sealed class Setup
        {
            public int EnemyHp = 60;
            public int EnemyAttack = 10;
            public int EnemyInterval = 45;
            public int MeleeHp = 100;
            public int MeleeAttack = 20;
            public int MeleeInterval = 30;
            public int MeleeBlock = 2;
            public string[] MeleeRange = { "@X" };
            public string[] RangedRange = { "@" };
            public long[] Spawns = { 0 };
        }

        private static Simulation Build(Setup s)
        {
            var map = GridMap.Parse(Map);
            var definition = new BattleDefinition(
                map, Tps, 10,
                new[] { new EnemyDefinition(EnemyId, OneCellPerSecond, 1, new CombatStats(s.EnemyHp, s.EnemyAttack, s.EnemyInterval)) },
                new[] { EnemyRoute.Parse(map, Route) },
                s.Spawns.Select(t => new SpawnEvent(t, EnemyId, 0)),
                new CostRules(99, 99, 60),
                new[]
                {
                    new UnitDefinition(Melee, 10, UnitTerrain.Ground, new CombatStats(s.MeleeHp, s.MeleeAttack, s.MeleeInterval), s.MeleeBlock, AttackRange.Parse(s.MeleeRange)),
                    new UnitDefinition(Ranged, 12, UnitTerrain.Hill, new CombatStats(60, 15, 36), 0, AttackRange.Parse(s.RangedRange)),
                },
                TestRules.Deploy);
            return new Simulation(World.Create(1, "s", 10, 99), definition);
        }

        private static void Run(Simulation simulation, int steps)
        {
            for (var i = 0; i < steps; i++) simulation.Step();
        }

        /// <summary>적이 칸 경계(반 칸 앞)에 닿는 틱 수. 등장 틱 1 + 이동 틱.</summary>
        private static int StepsUntilEnemyEnters(int cell) => 1 + (cell * UnitsPerCell - UnitsPerCell / 2 + OneCellPerSecond - 1) / OneCellPerSecond;

        // ---- 공격 범위 ----

        [Test]
        public void Range_Parse_UsesOriginAndRightAsFront()
        {
            var range = AttackRange.Parse(new[] { ".XXX", "OXXX", ".XXX" });
            Assert.AreEqual(9, range.Offsets.Count);
            Assert.IsTrue(range.Contains(new GridPos(0, 0), Direction.Right, new GridPos(1, 0)));
            Assert.IsTrue(range.Contains(new GridPos(0, 0), Direction.Right, new GridPos(3, 1)));
            Assert.IsFalse(range.Contains(new GridPos(0, 0), Direction.Right, new GridPos(0, 0)));
            Assert.IsFalse(range.Contains(new GridPos(0, 0), Direction.Right, new GridPos(-1, 0)));
        }

        [Test]
        public void Range_AtSign_IncludesOwnCell()
        {
            var range = AttackRange.Parse(new[] { "@X" });
            CollectionAssert.AreEquivalent(new[] { new GridPos(5, 5), new GridPos(6, 5) }, range.Cells(new GridPos(5, 5), Direction.Right).ToArray());
        }

        [TestCase(Direction.Right, 1, 0)]
        [TestCase(Direction.Up, 0, 1)]
        [TestCase(Direction.Left, -1, 0)]
        [TestCase(Direction.Down, 0, -1)]
        public void Range_RotatesFrontWithDirection(Direction direction, int x, int y)
        {
            Assert.AreEqual(new GridPos(x, y), AttackRange.Rotate(new GridPos(1, 0), direction));
        }

        [Test]
        public void Range_RotatesSideCells()
        {
            // 오른쪽 기준 왼편 앞 (1,1)은 위를 볼 때 (-1,1), 아래를 볼 때 (1,-1).
            Assert.AreEqual(new GridPos(-1, 1), AttackRange.Rotate(new GridPos(1, 1), Direction.Up));
            Assert.AreEqual(new GridPos(-1, -1), AttackRange.Rotate(new GridPos(1, 1), Direction.Left));
            Assert.AreEqual(new GridPos(1, -1), AttackRange.Rotate(new GridPos(1, 1), Direction.Down));
        }

        [TestCase("XX")]            // 자리 없음
        [TestCase("O@")]            // 자리 둘
        [TestCase("O")]             // 공격 칸 없음
        [TestCase("OZ")]            // 모르는 글자
        public void Range_RejectsBadInput(string row)
        {
            Assert.Throws<System.FormatException>(() => AttackRange.Parse(new[] { row }));
        }

        // ---- 저지 ----

        [Test]
        public void Enemy_IsBlocked_AtUnitCell_AndStops()
        {
            var simulation = Build(new Setup { MeleeAttack = 0 });
            simulation.TryPlaceUnit(Melee, new GridPos(3, 0), Direction.Left);

            Run(simulation, StepsUntilEnemyEnters(3) - 1);
            Assert.IsFalse(simulation.World.Enemies[0].IsBlocked);

            Run(simulation, 1);
            var enemy = simulation.World.Enemies[0];
            Assert.IsTrue(enemy.IsBlocked);
            var stoppedAt = enemy.Progress;

            Run(simulation, 300);
            Assert.AreEqual(stoppedAt, simulation.World.Enemies[0].Progress);
            Assert.AreEqual(10, simulation.World.DefenseHp);
        }

        [Test]
        public void Block_Count_IsLimited_OverflowPassesThrough()
        {
            var simulation = Build(new Setup { MeleeAttack = 0, MeleeBlock = 2, EnemyAttack = 0, Spawns = new long[] { 0, 10, 20 } });
            simulation.TryPlaceUnit(Melee, new GridPos(3, 0), Direction.Left);

            Run(simulation, 400);
            var unit = simulation.World.Units[0];
            Assert.AreEqual(2, simulation.World.BlockedCount(unit.Id));
            Assert.AreEqual(2, simulation.World.Enemies.Count);
            Assert.AreEqual(9, simulation.World.DefenseHp);
        }

        [Test]
        public void UnitWithZeroBlock_DoesNotBlock()
        {
            var simulation = Build(new Setup { MeleeAttack = 0, MeleeBlock = 0 });
            simulation.TryPlaceUnit(Melee, new GridPos(3, 0), Direction.Left);
            Run(simulation, 7 * Tps + 1);
            CollectionAssert.IsEmpty(simulation.World.Enemies);
            Assert.AreEqual(9, simulation.World.DefenseHp);
        }

        // ---- 공격 ----

        [Test]
        public void Unit_AttacksEveryInterval_AndKills()
        {
            var simulation = Build(new Setup { EnemyHp = 60, MeleeAttack = 20, MeleeInterval = 30, EnemyAttack = 0 });
            simulation.TryPlaceUnit(Melee, new GridPos(3, 0), Direction.Left);
            var hits = new List<long>();
            var killed = new List<int>();
            simulation.EnemyDamaged += (e, d) => hits.Add(simulation.World.Tick);
            simulation.EnemyKilled += e => killed.Add(e.Id);

            Run(simulation, 400);

            Assert.AreEqual(3, hits.Count);
            Assert.AreEqual(30, hits[1] - hits[0]);
            Assert.AreEqual(30, hits[2] - hits[1]);
            Assert.AreEqual(1, killed.Count);
            CollectionAssert.IsEmpty(simulation.World.Enemies);
            Assert.AreEqual(10, simulation.World.DefenseHp);
        }

        [Test]
        public void Unit_AttacksEnemyInFrontCell_BeforeBlocking()
        {
            // "@X"로 왼쪽(입구 쪽)을 보면, 적이 바로 앞 칸에 들어선 순간부터 맞는다.
            var simulation = Build(new Setup { EnemyAttack = 0 });
            simulation.TryPlaceUnit(Melee, new GridPos(3, 0), Direction.Left);
            long firstHit = -1;
            simulation.EnemyDamaged += (e, d) => { if (firstHit < 0) firstHit = simulation.World.Tick; };

            Run(simulation, StepsUntilEnemyEnters(3));
            Assert.That(firstHit, Is.GreaterThanOrEqualTo(0));
            Assert.That(firstHit, Is.LessThan(StepsUntilEnemyEnters(3) - 1));
        }

        [Test]
        public void Unit_FacingAway_DoesNotHitEnemyBehind()
        {
            // 오른쪽(출구 쪽)을 보는 "X" 한 칸 범위는 자기 칸도 안 친다. 막기만 한다.
            var simulation = Build(new Setup { MeleeRange = new[] { "OX" }, EnemyAttack = 0 });
            simulation.TryPlaceUnit(Melee, new GridPos(3, 0), Direction.Right);
            var hits = 0;
            simulation.EnemyDamaged += (e, d) => hits++;

            Run(simulation, 400);
            Assert.AreEqual(0, hits);
            Assert.IsTrue(simulation.World.Enemies[0].IsBlocked);
        }

        [Test]
        public void Unit_TargetsEnemyClosestToExit()
        {
            // 원거리가 언덕 (4,1)에서 아래를 보며 (4,0) · (3,0)... 을 친다. 앞선 적부터.
            var simulation = Build(new Setup { RangedRange = new[] { "OX" }, Spawns = new long[] { 0, 30 } });
            simulation.TryPlaceUnit(Ranged, new GridPos(4, 1), Direction.Down);
            var targets = new List<int>();
            simulation.EnemyDamaged += (e, d) => targets.Add(e.Id);

            Run(simulation, StepsUntilEnemyEnters(4) + 2);
            Assert.IsNotEmpty(targets);
            Assert.AreEqual(2, targets[0]);   // 먼저 나온 적 (ID 2; 능력자가 ID 1)
        }

        [Test]
        public void RangedOnHill_HitsEnemiesInRange_AndDoesNotBlock()
        {
            var simulation = Build(new Setup { RangedRange = new[] { ".X.", "XOX", ".X." }, EnemyAttack = 0 });
            simulation.TryPlaceUnit(Ranged, new GridPos(4, 1), Direction.Right);
            var hits = 0;
            simulation.EnemyDamaged += (e, d) => hits++;

            Run(simulation, 7 * Tps + 1);
            Assert.That(hits, Is.GreaterThan(0));
            Assert.IsFalse(simulation.World.Enemies.Any(e => e.IsBlocked));
        }

        // ---- 반격 ----

        [Test]
        public void BlockedEnemy_HitsBlocker_AndUnitFallsAtZero_ReleasingEnemies()
        {
            var simulation = Build(new Setup { MeleeAttack = 0, MeleeHp = 25, EnemyAttack = 10, EnemyInterval = 45 });
            simulation.TryPlaceUnit(Melee, new GridPos(3, 0), Direction.Left);
            var damage = new List<int>();
            var defeated = new List<int>();
            simulation.UnitDamaged += (u, d) => damage.Add(d);
            simulation.UnitDefeated += u => defeated.Add(u.Id);

            Run(simulation, StepsUntilEnemyEnters(3) + 45 * 3 + 2);

            CollectionAssert.AreEqual(new[] { 10, 10, 10 }, damage);
            Assert.AreEqual(1, defeated.Count);
            CollectionAssert.IsEmpty(simulation.World.Units);
            Assert.IsFalse(simulation.World.Enemies[0].IsBlocked);

            // 풀린 적은 다시 걸어 출구로 샌다.
            Run(simulation, 7 * Tps);
            Assert.AreEqual(9, simulation.World.DefenseHp);
        }

        [Test]
        public void DefeatedUnit_WaitsForRedeployCooldown_WithoutRefund()
        {
            var simulation = Build(new Setup { MeleeAttack = 0, MeleeHp = 10, EnemyAttack = 10 });
            simulation.TryPlaceUnit(Melee, new GridPos(3, 0), Direction.Left);

            // 쓰러지는 피격과 쓰러짐은 같은 틱 안에서 이어서 온다. 그 사이 코스트가 그대로면 환급이 없다는 뜻.
            var costAtHit = -1;
            var costAtDefeat = -2;
            simulation.UnitDamaged += (u, d) => costAtHit = simulation.World.Cost;
            simulation.UnitDefeated += u => costAtDefeat = simulation.World.Cost;
            Run(simulation, StepsUntilEnemyEnters(3) + 2);
            CollectionAssert.IsEmpty(simulation.World.Units);
            Assert.AreEqual(costAtHit, costAtDefeat);

            Assert.AreEqual(PlacementResult.OnCooldown, simulation.CheckPlacement(Melee, new GridPos(5, 0)));
            Run(simulation, TestRules.Deploy.RedeployCooldownTicks);
            Assert.AreEqual(PlacementResult.Ok, simulation.CheckPlacement(Melee, new GridPos(5, 0)));
        }

        [Test]
        public void EventsWorkWithoutSubscribers()
        {
            var simulation = Build(new Setup { Spawns = new long[] { 0, 20, 40, 60 } });
            simulation.TryPlaceUnit(Melee, new GridPos(3, 0), Direction.Left);
            simulation.TryPlaceUnit(Ranged, new GridPos(2, 1), Direction.Down);
            Run(simulation, 600);
            CollectionAssert.IsEmpty(simulation.World.Enemies);
        }

        // ---- 결정성 · 스냅샷 ----

        [Test]
        public void SameInputs_GiveSameState()
        {
            var a = Build(new Setup { Spawns = new long[] { 0, 15, 30, 45, 60 } });
            var b = Build(new Setup { Spawns = new long[] { 0, 15, 30, 45, 60 } });
            foreach (var s in new[] { a, b })
            {
                s.TryPlaceUnit(Melee, new GridPos(3, 0), Direction.Left);
                s.TryPlaceUnit(Ranged, new GridPos(2, 1), Direction.Right);
            }
            Run(a, 350);
            Run(b, 350);
            AssertSameState(a.World, b.World);
        }

        [Test]
        public void Snapshot_MidFight_ContinuesIdentically()
        {
            var original = Build(new Setup { Spawns = new long[] { 0, 15, 30, 45, 60 } });
            original.TryPlaceUnit(Melee, new GridPos(3, 0), Direction.Left);
            original.TryPlaceUnit(Ranged, new GridPos(2, 1), Direction.Right);
            Run(original, StepsUntilEnemyEnters(3) + 20);
            Assert.IsTrue(original.World.Enemies.Any(e => e.IsBlocked), "snapshot should be taken mid-fight");

            var restored = new Simulation(World.FromSnapshot(original.World.ToSnapshot()), original.Definition);
            AssertSameState(original.World, restored.World);

            Run(original, 300);
            Run(restored, 300);
            AssertSameState(original.World, restored.World);
        }

        private static void AssertSameState(World a, World b)
        {
            Assert.AreEqual(a.Tick, b.Tick);
            Assert.AreEqual(a.DefenseHp, b.DefenseHp);
            Assert.AreEqual(a.Cost, b.Cost);
            Assert.AreEqual(a.NextEntityId, b.NextEntityId);
            CollectionAssert.AreEqual(a.UnitSlots.Select(s => (s.DefinitionId, s.Deployments, s.ReadyTick)).ToArray(),
                b.UnitSlots.Select(s => (s.DefinitionId, s.Deployments, s.ReadyTick)).ToArray());
            CollectionAssert.AreEqual(a.Enemies.Select(e => (e.Id, e.Progress, e.Hp, e.AttackCooldown, e.BlockedBy)).ToArray(),
                b.Enemies.Select(e => (e.Id, e.Progress, e.Hp, e.AttackCooldown, e.BlockedBy)).ToArray());
            CollectionAssert.AreEqual(a.Units.Select(u => (u.Id, u.Hp, u.AttackCooldown, u.Stage, u.SpentCost)).ToArray(),
                b.Units.Select(u => (u.Id, u.Hp, u.AttackCooldown, u.Stage, u.SpentCost)).ToArray());
        }
    }
}
