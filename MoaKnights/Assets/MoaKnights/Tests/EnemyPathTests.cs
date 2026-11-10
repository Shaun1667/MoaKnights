using System.Collections.Generic;
using System.Linq;
using MoaKnights.Core.Map;
using MoaKnights.Core.Sim;
using NUnit.Framework;

namespace MoaKnights.Tests
{
    public sealed class EnemyPathTests
    {
        private const int TicksPerSecond = 30;
        private const int OneCellPerSecond = 100;
        private const string EnemyId = "e";

        // 입구 (4,1) → (4,0) → (0,0) → (0,1) 출구. 윗줄 가운데는 언덕이라 아랫줄로 돌아간다.
        private static readonly string[] BentMap =
        {
            "EHHHS",
            ".....",
        };

        private const string BentRoute = "4,1 4,0 0,0 0,1";
        private const int BentRouteLength = 6;

        private static readonly CostRules Cost = new CostRules(20, 99, 60);

        private static GridMap Map() => GridMap.Parse(BentMap);

        private static BattleDefinition Definition(int defenseHp, int leakDamage, int speed, params long[] spawnTicks)
        {
            var map = Map();
            return new BattleDefinition(
                map,
                TicksPerSecond,
                defenseHp,
                new[] { new EnemyDefinition(EnemyId, speed, leakDamage, new CombatStats(60, 10, 45)) },
                new[] { EnemyRoute.Parse(map, BentRoute) },
                spawnTicks.Select(t => new SpawnEvent(t, EnemyId, 0)),
                new CostRules(20, 99, 60),
                new UnitDefinition[0],
                TestRules.Deploy);
        }

        private static Simulation NewSimulation(BattleDefinition definition, int seed = 1) =>
            new Simulation(World.Create(seed, "s", definition.DefenseHp, definition.Cost.Start), definition);

        private static void Run(Simulation simulation, int steps)
        {
            for (var i = 0; i < steps; i++) simulation.Step();
        }

        [Test]
        public void Route_FillsCellsBetweenCorners()
        {
            var route = EnemyRoute.Parse(Map(), BentRoute);
            Assert.AreEqual(BentRouteLength, route.Length);
            CollectionAssert.AreEqual(
                new[]
                {
                    new GridPos(4, 1), new GridPos(4, 0), new GridPos(3, 0), new GridPos(2, 0),
                    new GridPos(1, 0), new GridPos(0, 0), new GridPos(0, 1),
                },
                route.Cells.ToArray());
        }

        [TestCase("")]                          // 비어 있음
        [TestCase("4,1")]                       // 꺾이는 칸이 하나뿐
        [TestCase("4,1 x,y")]                   // 숫자가 아님
        [TestCase("4,1 3,0 0,0 0,1")]           // 대각선
        [TestCase("4,1 4,1 4,0 0,0 0,1")]       // 길이 0인 구간
        [TestCase("4,1 3,1 3,0 0,0 0,1")]       // 언덕을 지남
        [TestCase("4,0 0,0 0,1")]               // 입구에서 시작하지 않음
        [TestCase("4,1 4,0 0,0")]               // 출구에서 끝나지 않음
        [TestCase("4,1 4,-1 0,-1 0,1")]         // 맵 밖
        public void Route_RejectsBadInput(string corners)
        {
            Assert.Throws<System.FormatException>(() => EnemyRoute.Parse(Map(), corners));
        }

        [Test]
        public void Route_Locate_FindsCellsAndFraction()
        {
            var route = EnemyRoute.Parse(Map(), BentRoute);
            route.Locate(1500, 1000, out var from, out var to, out var t);
            Assert.AreEqual(new GridPos(4, 0), from);
            Assert.AreEqual(new GridPos(3, 0), to);
            Assert.AreEqual(0.5f, t);
        }

        [Test]
        public void Route_Locate_AtEnd_StaysOnLastSegment()
        {
            var route = EnemyRoute.Parse(Map(), BentRoute);
            route.Locate(99999, 1000, out var from, out var to, out var t);
            Assert.AreEqual(new GridPos(0, 0), from);
            Assert.AreEqual(new GridPos(0, 1), to);
            Assert.AreEqual(1f, t);
        }

        [Test]
        public void Spawns_AppearAtTheirTick_WithUniqueIds()
        {
            var simulation = NewSimulation(Definition(10, 1, OneCellPerSecond, 0, 60));

            Run(simulation, 1);
            Assert.AreEqual(1, simulation.World.Enemies.Count);

            Run(simulation, 59);
            Assert.AreEqual(1, simulation.World.Enemies.Count);

            Run(simulation, 1);
            Assert.AreEqual(2, simulation.World.Enemies.Count);
            Assert.AreNotEqual(simulation.World.Enemies[0].Id, simulation.World.Enemies[1].Id);
        }

        [Test]
        public void Definition_SortsSpawnsByTick()
        {
            var definition = Definition(10, 1, OneCellPerSecond, 60, 0, 30);
            CollectionAssert.AreEqual(new long[] { 0, 30, 60 }, definition.Spawns.Select(s => s.Tick).ToArray());
        }

        [Test]
        public void Enemy_OneCellPerSecond_TakesExactRouteTime()
        {
            var simulation = NewSimulation(Definition(10, 1, OneCellPerSecond, 0));
            var ticksToExit = BentRouteLength * TicksPerSecond;

            // 나오는 틱에는 움직이지 않으므로 그 한 틱을 더해야 출구에 닿는다.
            Run(simulation, ticksToExit);
            Assert.AreEqual(1, simulation.World.Enemies.Count);
            Assert.AreEqual(10, simulation.World.DefenseHp);

            Run(simulation, 1);
            CollectionAssert.IsEmpty(simulation.World.Enemies);
            Assert.AreEqual(9, simulation.World.DefenseHp);
        }

        [Test]
        public void Enemy_FasterSpeed_ArrivesEarlier()
        {
            var simulation = NewSimulation(Definition(10, 1, OneCellPerSecond * 2, 0));
            Run(simulation, BentRouteLength * TicksPerSecond / 2 + 1);
            CollectionAssert.IsEmpty(simulation.World.Enemies);
        }

        [Test]
        public void Leak_RaisesEventsInOrder()
        {
            var simulation = NewSimulation(Definition(10, 3, OneCellPerSecond, 0));
            var log = new List<string>();
            simulation.EnemySpawned += e => log.Add("spawn " + e.Id);
            simulation.EnemyLeaked += e => log.Add("leak " + e.Id);
            simulation.DefenseDamaged += d => log.Add("damage " + d);

            Run(simulation, BentRouteLength * TicksPerSecond + 1);

            CollectionAssert.AreEqual(new[] { "spawn 1", "leak 1", "damage 3" }, log);
            Assert.AreEqual(7, simulation.World.DefenseHp);
        }

        [Test]
        public void Defense_StopsAtZero_AndSendsNoFurtherDamage()
        {
            var simulation = NewSimulation(Definition(1, 1, OneCellPerSecond, 0, 1));
            var damage = new List<int>();
            simulation.DefenseDamaged += damage.Add;

            Run(simulation, BentRouteLength * TicksPerSecond + 2);

            Assert.AreEqual(0, simulation.World.DefenseHp);
            CollectionAssert.AreEqual(new[] { 1 }, damage);
            CollectionAssert.IsEmpty(simulation.World.Enemies);
        }

        [Test]
        public void Definition_RejectsUnknownEnemyAndRoute()
        {
            var map = Map();
            var enemies = new[] { new EnemyDefinition(EnemyId, OneCellPerSecond, 1, new CombatStats(60, 10, 45)) };
            var routes = new[] { EnemyRoute.Parse(map, BentRoute) };

            Assert.Throws<System.ArgumentException>(() =>
                new BattleDefinition(map, TicksPerSecond, 10, enemies, routes, new[] { new SpawnEvent(0, "none", 0) }, Cost, new UnitDefinition[0], TestRules.Deploy));
            Assert.Throws<System.ArgumentException>(() =>
                new BattleDefinition(map, TicksPerSecond, 10, enemies, routes, new[] { new SpawnEvent(0, EnemyId, 5) }, Cost, new UnitDefinition[0], TestRules.Deploy));
        }

        [Test]
        public void SameSeed_SameDefinition_GivesSameState()
        {
            var definition = Definition(10, 1, OneCellPerSecond, 0, 30, 60);
            var a = NewSimulation(definition, seed: 5);
            var b = NewSimulation(definition, seed: 5);
            Run(a, 200);
            Run(b, 200);
            AssertSameState(a.World, b.World);
        }

        [Test]
        public void Snapshot_RoundTrip_WithEnemies_ContinuesIdentically()
        {
            var definition = Definition(10, 1, OneCellPerSecond, 0, 60, 120, 400);
            var original = NewSimulation(definition);
            Run(original, 200);

            // 복원 시점에 한 마리는 이미 새고, 두 마리는 길 위에, 한 마리는 아직 나오지 않았다.
            Assert.AreEqual(9, original.World.DefenseHp);
            Assert.AreEqual(2, original.World.Enemies.Count);
            Assert.AreEqual(3, original.World.NextSpawnIndex);

            var restored = new Simulation(World.FromSnapshot(original.World.ToSnapshot()), definition);
            AssertSameState(original.World, restored.World);

            Run(original, 300);
            Run(restored, 300);
            AssertSameState(original.World, restored.World);
        }

        private static void AssertSameState(World a, World b)
        {
            Assert.AreEqual(a.Tick, b.Tick);
            Assert.AreEqual(a.DefenseHp, b.DefenseHp);
            Assert.AreEqual(a.NextSpawnIndex, b.NextSpawnIndex);
            Assert.AreEqual(a.NextEntityId, b.NextEntityId);
            CollectionAssert.AreEqual(a.Enemies.Select(e => e.Id).ToArray(), b.Enemies.Select(e => e.Id).ToArray());
            CollectionAssert.AreEqual(a.Enemies.Select(e => e.Progress).ToArray(), b.Enemies.Select(e => e.Progress).ToArray());
            CollectionAssert.AreEqual(a.Enemies.Select(e => e.DefinitionId).ToArray(), b.Enemies.Select(e => e.DefinitionId).ToArray());
            CollectionAssert.AreEqual(a.Enemies.Select(e => e.RouteIndex).ToArray(), b.Enemies.Select(e => e.RouteIndex).ToArray());
        }
    }
}
