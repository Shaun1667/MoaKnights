using System.Linq;
using MoaKnights.Core.Map;
using MoaKnights.Core.Sim;
using NUnit.Framework;

namespace MoaKnights.Tests
{
    public sealed class SimFoundationTests
    {
        private static ulong[] Draw(SeededRandom random, int count) =>
            Enumerable.Range(0, count).Select(_ => random.NextUInt64()).ToArray();

        private static readonly string[] SmallMap = { "#H.", "S.E" };

        private static World NewWorld(int seed, string stageId = "s") => World.Create(seed, stageId, 10, 20);

        [Test]
        public void SameSeed_GivesSameSequence()
        {
            var a = NewWorld(1234);
            var b = NewWorld(1234);
            CollectionAssert.AreEqual(Draw(a.Rng(RngStream.Combat), 16), Draw(b.Rng(RngStream.Combat), 16));
        }

        [Test]
        public void DifferentSeed_GivesDifferentSequence()
        {
            var a = NewWorld(1);
            var b = NewWorld(2);
            CollectionAssert.AreNotEqual(Draw(a.Rng(RngStream.Combat), 16), Draw(b.Rng(RngStream.Combat), 16));
        }

        [Test]
        public void Streams_AreIndependent()
        {
            var a = NewWorld(7);
            var b = NewWorld(7);
            Draw(a.Rng(RngStream.Visual), 100);
            CollectionAssert.AreEqual(Draw(a.Rng(RngStream.Combat), 16), Draw(b.Rng(RngStream.Combat), 16));
        }

        [Test]
        public void NextInt_StaysInRange()
        {
            var random = NewWorld(9).Rng(RngStream.Item);
            for (var i = 0; i < 1000; i++)
            {
                var value = random.NextInt(6);
                Assert.That(value, Is.InRange(0, 5));
            }
        }

        [Test]
        public void Snapshot_RoundTrip_ContinuesIdentically()
        {
            var map = GridMap.Parse(SmallMap);
            var definition = new BattleDefinition(map, 30, 10, new EnemyDefinition[0], new EnemyRoute[0], new SpawnEvent[0], new CostRules(20, 99, 60), new UnitDefinition[0], TestRules.Deploy, TestRules.TwoStarHpPercent);
            var original = NewWorld(42, "stage_x");
            var simulation = new Simulation(original, definition);
            for (var i = 0; i < 50; i++) simulation.Step();
            original.AllocateEntityId();
            original.Rng(RngStream.Combat).NextUInt64();

            var restored = World.FromSnapshot(original.ToSnapshot());

            Assert.AreEqual(50L, restored.Tick);
            Assert.AreEqual("stage_x", restored.StageId);
            Assert.AreEqual(42, restored.Seed);
            Assert.AreEqual(original.AllocateEntityId(), restored.AllocateEntityId());
            CollectionAssert.AreEqual(Draw(original.Rng(RngStream.Combat), 16), Draw(restored.Rng(RngStream.Combat), 16));
        }

        [Test]
        public void Snapshot_RejectsOtherVersion()
        {
            var snapshot = NewWorld(1).ToSnapshot();
            snapshot.Version = World.SnapshotVersion + 1;
            Assert.Throws<System.NotSupportedException>(() => World.FromSnapshot(snapshot));
        }

        [Test]
        public void EntityIds_AreUnique()
        {
            var world = NewWorld(1);
            Assert.AreNotEqual(world.AllocateEntityId(), world.AllocateEntityId());
        }

        [Test]
        public void Clock_OneSecond_GivesTicksPerSecond()
        {
            var clock = new TickClock(30);
            var ticks = 0;
            for (var i = 0; i < 60; i++) ticks += clock.Advance(1d / 60d);
            Assert.That(ticks, Is.InRange(29, 31));
        }

        [Test]
        public void Clock_DoubleSpeed_GivesTwiceTicks()
        {
            var clock = new TickClock(30);
            clock.SetSpeed(2);
            Assert.AreEqual(6, clock.Advance(0.1d));
        }

        /// <summary>BALANCE 2-2 검산: 카드를 맵 위로 끄는 동안 10% → 1배속 1초에 3틱, 2배속 6틱.</summary>
        [Test]
        public void Clock_Slow_ScalesTicks_AndCombinesWithSpeed()
        {
            var clock = new TickClock(30);
            clock.SetSlow(10);
            Assert.AreEqual(3, clock.Advance(1d));
            clock.SetSpeed(2);
            Assert.AreEqual(6, clock.Advance(1d));
        }

        [Test]
        public void Clock_ClearSlow_RestoresNormalSpeed()
        {
            var clock = new TickClock(30);
            clock.SetSlow(10);
            clock.ClearSlow();
            Assert.AreEqual(100, clock.SlowPercent);
            Assert.AreEqual(3, clock.Advance(0.1d));
        }

        [Test]
        public void Clock_SlowOutOfRange_Throws()
        {
            var clock = new TickClock(30);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => clock.SetSlow(0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => clock.SetSlow(101));
        }

        [Test]
        public void Clock_Paused_GivesNoTicks()
        {
            var clock = new TickClock(30) { Paused = true };
            Assert.AreEqual(0, clock.Advance(1d));
        }

        [Test]
        public void Clock_LongFrame_IsCapped()
        {
            var clock = new TickClock(30);
            Assert.That(clock.Advance(100d), Is.LessThan(100));
            Assert.AreEqual(0, clock.Advance(0d));
        }

        [Test]
        public void Map_TopRowIsHighestY()
        {
            var map = GridMap.Parse(SmallMap);
            Assert.AreEqual(3, map.Width);
            Assert.AreEqual(2, map.Height);
            Assert.AreEqual(CellType.Blocked, map[new GridPos(0, 1)]);
            Assert.AreEqual(CellType.Hill, map[new GridPos(1, 1)]);
            Assert.AreEqual(CellType.Entrance, map[new GridPos(0, 0)]);
            Assert.AreEqual(new GridPos(0, 0), map.Entrances[0]);
            Assert.AreEqual(new GridPos(2, 0), map.Exits[0]);
        }

        [TestCase("S.E", "S.")]     // 줄 길이가 다름
        [TestCase("S.E", "..X")]    // 모르는 글자
        [TestCase("...", "..E")]    // 입구 없음
        [TestCase("S..", "...")]    // 출구 없음
        public void Map_RejectsBadInput(string first, string second)
        {
            Assert.Throws<System.FormatException>(() => GridMap.Parse(new[] { first, second }));
        }
    }
}
