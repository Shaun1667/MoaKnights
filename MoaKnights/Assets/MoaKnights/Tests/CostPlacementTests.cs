using System.Collections.Generic;
using System.Linq;
using MoaKnights.Core.Map;
using MoaKnights.Core.Sim;
using NUnit.Framework;

namespace MoaKnights.Tests
{
    public sealed class CostPlacementTests
    {
        private const string Melee = "melee";
        private const string Ranged = "ranged";
        private const int RegenTicks = 60;

        // y=2: # H H      — (0,2) 막힘, (1,2) · (2,2) 언덕
        // y=1: . . .      — 지상
        // y=0: S . E      — 입구 · 지상 · 출구
        private static readonly string[] Map =
        {
            "#HH",
            "...",
            "S.E",
        };

        private static Simulation NewSimulation(int startCost = 20, int maxCost = 99, int seed = 1)
        {
            var map = GridMap.Parse(Map);
            var definition = new BattleDefinition(
                map,
                30,
                10,
                new EnemyDefinition[0],
                new EnemyRoute[0],
                new SpawnEvent[0],
                new CostRules(startCost, maxCost, RegenTicks),
                new[]
                {
                    new UnitDefinition(Melee, 10, UnitTerrain.Ground),
                    new UnitDefinition(Ranged, 12, UnitTerrain.Hill),
                });
            return new Simulation(World.Create(seed, "s", definition.DefenseHp, definition.Cost.Start), definition);
        }

        private static void Run(Simulation simulation, int steps)
        {
            for (var i = 0; i < steps; i++) simulation.Step();
        }

        [Test]
        public void Cost_StartsAtStartValue()
        {
            Assert.AreEqual(20, NewSimulation().World.Cost);
        }

        [Test]
        public void Cost_RegensOneEveryRegenTicks()
        {
            var simulation = NewSimulation();
            Run(simulation, RegenTicks - 1);
            Assert.AreEqual(20, simulation.World.Cost);

            Run(simulation, 1);
            Assert.AreEqual(21, simulation.World.Cost);

            Run(simulation, RegenTicks * 3);
            Assert.AreEqual(24, simulation.World.Cost);
        }

        [Test]
        public void Cost_StopsAtMax_AndDoesNotBankProgress()
        {
            var simulation = NewSimulation(startCost: 98, maxCost: 99);
            Run(simulation, RegenTicks * 5);
            Assert.AreEqual(99, simulation.World.Cost);
            Assert.AreEqual(0, simulation.World.CostRegenTicks);

            // 최대치에서 쓴 직후에는 처음부터 다시 찬다.
            Assert.AreEqual(PlacementResult.Ok, simulation.TryPlaceUnit(Melee, new GridPos(1, 1), Direction.Right));
            Assert.AreEqual(89, simulation.World.Cost);
            Run(simulation, RegenTicks - 1);
            Assert.AreEqual(89, simulation.World.Cost);
        }

        [Test]
        public void Place_Ok_SpendsCost_AndRaisesEvent()
        {
            var simulation = NewSimulation();
            var placed = new List<Unit>();
            simulation.UnitPlaced += placed.Add;

            var result = simulation.TryPlaceUnit(Melee, new GridPos(1, 1), Direction.Up);

            Assert.AreEqual(PlacementResult.Ok, result);
            Assert.AreEqual(10, simulation.World.Cost);
            Assert.AreEqual(1, placed.Count);
            Assert.AreEqual(new GridPos(1, 1), placed[0].Cell);
            Assert.AreEqual(Direction.Up, placed[0].Direction);
            Assert.AreSame(placed[0], simulation.World.UnitAt(new GridPos(1, 1)));
        }

        [Test]
        public void Place_WorksWithoutSubscribers()
        {
            var simulation = NewSimulation();
            Assert.AreEqual(PlacementResult.Ok, simulation.TryPlaceUnit(Ranged, new GridPos(1, 2), Direction.Left));
            Assert.AreEqual(1, simulation.World.Units.Count);
        }

        [TestCase(Melee, 1, 2, PlacementResult.WrongTerrain)]      // 지상 능력자를 언덕에
        [TestCase(Ranged, 1, 1, PlacementResult.WrongTerrain)]     // 언덕 능력자를 지상에
        [TestCase(Melee, 0, 2, PlacementResult.WrongTerrain)]      // 막힌 칸
        [TestCase(Melee, 0, 0, PlacementResult.WrongTerrain)]      // 입구
        [TestCase(Melee, 2, 0, PlacementResult.WrongTerrain)]      // 출구
        [TestCase(Melee, 3, 0, PlacementResult.OutOfBounds)]
        [TestCase(Melee, -1, 1, PlacementResult.OutOfBounds)]
        [TestCase("nobody", 1, 1, PlacementResult.UnknownUnit)]
        public void Place_Rejects_AndChangesNothing(string unitId, int x, int y, PlacementResult expected)
        {
            var simulation = NewSimulation();
            var placed = 0;
            simulation.UnitPlaced += _ => placed++;

            Assert.AreEqual(expected, simulation.TryPlaceUnit(unitId, new GridPos(x, y), Direction.Right));
            Assert.AreEqual(20, simulation.World.Cost);
            CollectionAssert.IsEmpty(simulation.World.Units);
            Assert.AreEqual(0, placed);
        }

        [Test]
        public void Place_Rejects_NotEnoughCost()
        {
            var simulation = NewSimulation(startCost: 9);
            Assert.AreEqual(PlacementResult.NotEnoughCost, simulation.TryPlaceUnit(Melee, new GridPos(1, 1), Direction.Right));
            Assert.AreEqual(9, simulation.World.Cost);

            Run(simulation, RegenTicks);
            Assert.AreEqual(PlacementResult.Ok, simulation.TryPlaceUnit(Melee, new GridPos(1, 1), Direction.Right));
            Assert.AreEqual(0, simulation.World.Cost);
        }

        [Test]
        public void Place_Rejects_OccupiedCell()
        {
            var simulation = NewSimulation(startCost: 40);
            simulation.TryPlaceUnit(Ranged, new GridPos(1, 2), Direction.Right);
            Assert.AreEqual(PlacementResult.Occupied, simulation.CheckPlacement(Ranged, new GridPos(1, 2)));
        }

        [Test]
        public void Place_Rejects_SameUnitTwice()
        {
            var simulation = NewSimulation(startCost: 40);
            simulation.TryPlaceUnit(Melee, new GridPos(1, 1), Direction.Right);
            Assert.AreEqual(PlacementResult.AlreadyDeployed, simulation.TryPlaceUnit(Melee, new GridPos(0, 1), Direction.Right));
            Assert.AreEqual(30, simulation.World.Cost);
        }

        [Test]
        public void Place_TwoUnits_GetUniqueIds()
        {
            var simulation = NewSimulation(startCost: 40);
            simulation.TryPlaceUnit(Melee, new GridPos(1, 1), Direction.Right);
            simulation.TryPlaceUnit(Ranged, new GridPos(2, 2), Direction.Down);
            Assert.AreNotEqual(simulation.World.Units[0].Id, simulation.World.Units[1].Id);
        }

        [Test]
        public void CheckPlacement_DoesNotChangeState()
        {
            var simulation = NewSimulation();
            var before = simulation.World.NextEntityId;
            Assert.AreEqual(PlacementResult.Ok, simulation.CheckPlacement(Melee, new GridPos(1, 1)));
            Assert.AreEqual(20, simulation.World.Cost);
            Assert.AreEqual(before, simulation.World.NextEntityId);
            CollectionAssert.IsEmpty(simulation.World.Units);
        }

        [Test]
        public void Snapshot_RoundTrip_WithUnitsAndCost_ContinuesIdentically()
        {
            var original = NewSimulation(startCost: 30);
            Run(original, 75);
            original.TryPlaceUnit(Melee, new GridPos(0, 1), Direction.Left);
            original.TryPlaceUnit(Ranged, new GridPos(2, 2), Direction.Down);

            var restored = new Simulation(World.FromSnapshot(original.World.ToSnapshot()), original.Definition);
            AssertSameState(original.World, restored.World);
            Assert.AreEqual(15, restored.World.CostRegenTicks);

            Run(original, 200);
            Run(restored, 200);
            AssertSameState(original.World, restored.World);
        }

        [Test]
        public void Definition_RejectsDuplicateUnitAndBadCostRules()
        {
            var map = GridMap.Parse(Map);
            var units = new[] { new UnitDefinition(Melee, 10, UnitTerrain.Ground), new UnitDefinition(Melee, 12, UnitTerrain.Hill) };
            Assert.Throws<System.ArgumentException>(() => new BattleDefinition(
                map, 30, 10, new EnemyDefinition[0], new EnemyRoute[0], new SpawnEvent[0], new CostRules(20, 99, 60), units));

            Assert.Throws<System.ArgumentOutOfRangeException>(() => new CostRules(100, 99, 60));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new CostRules(20, 99, 0));
        }

        private static void AssertSameState(World a, World b)
        {
            Assert.AreEqual(a.Tick, b.Tick);
            Assert.AreEqual(a.Cost, b.Cost);
            Assert.AreEqual(a.CostRegenTicks, b.CostRegenTicks);
            Assert.AreEqual(a.NextEntityId, b.NextEntityId);
            CollectionAssert.AreEqual(a.Units.Select(u => u.Id).ToArray(), b.Units.Select(u => u.Id).ToArray());
            CollectionAssert.AreEqual(a.Units.Select(u => u.DefinitionId).ToArray(), b.Units.Select(u => u.DefinitionId).ToArray());
            CollectionAssert.AreEqual(a.Units.Select(u => u.Cell).ToArray(), b.Units.Select(u => u.Cell).ToArray());
            CollectionAssert.AreEqual(a.Units.Select(u => u.Direction).ToArray(), b.Units.Select(u => u.Direction).ToArray());
        }
    }
}
