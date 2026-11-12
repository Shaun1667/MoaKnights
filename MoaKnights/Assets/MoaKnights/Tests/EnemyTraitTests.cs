using System;
using System.Collections.Generic;
using System.Linq;
using MoaKnights.Core.Map;
using MoaKnights.Core.Sim;
using NUnit.Framework;

namespace MoaKnights.Tests
{
    /// <summary>6-2 적 특성: 방어 · 비행 · 원거리 · 치유 · 무거움 · 분열 (BALANCE 3-7 ~ 3-13).</summary>
    public sealed class EnemyTraitTests
    {
        private const int Tps = 30;
        private const int OneCellPerSecond = 100;
        private const string Melee = "melee";
        private const string Watcher = "watcher";

        // 일직선 길: 입구 (0,0) → 출구 (7,0). 위 줄(y = 1)은 언덕.
        private static readonly string[] Map =
        {
            "HHHHHHHH",
            "S......E",
        };

        private const string Route = "0,0 7,0";

        private static EnemyDefinition Enemy(string id, int hp, int attack = 0, int interval = 30, EnemyTraits traits = null, int speed = OneCellPerSecond) =>
            new EnemyDefinition(id, speed, 1, new CombatStats(hp, attack, interval), traits);

        /// <summary>근거리(지상, 범위 @X)와 감시자(언덕, 범위가 맵 밖이라 치지 않음) 둘을 쓸 수 있는 판.</summary>
        private static Simulation Build(IEnumerable<EnemyDefinition> enemies, IEnumerable<SpawnEvent> spawns, int meleeBlock = 2, int meleeAttack = 20)
        {
            var map = GridMap.Parse(Map);
            var definition = new BattleDefinition(
                map, Tps, 10, enemies,
                new[] { EnemyRoute.Parse(map, Route) },
                spawns,
                new CostRules(99, 99, 60),
                new[]
                {
                    new UnitDefinition(Melee, 10, UnitTerrain.Ground, new CombatStats(1000, meleeAttack, 30), meleeBlock, AttackRange.Parse(new[] { "@X" }), TestRules.NoSkills(), UnitRole.Dealer),
                    new UnitDefinition(Watcher, 10, UnitTerrain.Hill, new CombatStats(100, 1, 30), 0, AttackRange.Parse(new[] { "OX" }), TestRules.NoSkills(), UnitRole.Dealer),
                },
                TestRules.Deploy, TestRules.TwoStarHpPercent);
            return new Simulation(World.Create(1, "s", 10, 99), definition);
        }

        private static void Run(Simulation simulation, int steps)
        {
            for (var i = 0; i < steps; i++) simulation.Step();
        }

        // ---- 방어 ----

        [TestCase(20, 12)]
        [TestCase(5, 1)]
        public void Armor_ReducesDamage_ButAtLeastOne(int meleeAttack, int expected)
        {
            var s = Build(new[] { Enemy("shell", 100, traits: new EnemyTraits(armor: 8)) }, new[] { new SpawnEvent(0, "shell", 0) }, meleeAttack: meleeAttack);
            Assert.AreEqual(PlacementResult.Ok, s.TryPlaceUnit(Melee, new GridPos(3, 0), Direction.Right));
            var damages = new List<int>();
            s.EnemyDamaged += (enemy, damage) => damages.Add(damage);

            Run(s, 200);

            Assert.IsNotEmpty(damages);
            Assert.IsTrue(damages.All(d => d == expected));
            Assert.AreEqual(100 - expected * damages.Count, s.World.Enemies.Single().Hp);
        }

        // ---- 비행 ----

        [Test]
        public void Flying_IsNotBlocked_ButStillGetsHit_AndLeaks()
        {
            var s = Build(new[] { Enemy("wing", 1000, traits: new EnemyTraits(flying: true)) }, new[] { new SpawnEvent(0, "wing", 0) });
            s.TryPlaceUnit(Melee, new GridPos(3, 0), Direction.Right);
            var blocked = 0;
            var hits = 0;
            var leaked = 0;
            s.EnemyBlocked += (e, u) => blocked++;
            s.EnemyDamaged += (e, d) => hits++;
            s.EnemyLeaked += e => leaked++;

            Run(s, 300);

            Assert.AreEqual(0, blocked);
            Assert.Greater(hits, 0);
            Assert.AreEqual(1, leaked);
            Assert.AreEqual(9, s.World.DefenseHp);
        }

        // ---- 원거리 ----

        [Test]
        public void Ranged_HitsNearestUnitWithinReach_WhileWalking()
        {
            var s = Build(new[] { Enemy("spitter", 1000, attack: 12, interval: 60, traits: new EnemyTraits(rangedReach: 2)) }, new[] { new SpawnEvent(0, "spitter", 0) });
            s.TryPlaceUnit(Watcher, new GridPos(3, 1), Direction.Up);
            var watcher = s.World.Units.Single();
            var hitCells = new List<GridPos>();
            s.UnitDamaged += (unit, damage) =>
            {
                Assert.AreEqual(12, damage);
                hitCells.Add(s.EnemyCell(s.World.Enemies.Single()));
            };

            Run(s, 300);

            Assert.IsNotEmpty(hitCells);
            // 처음 친 곳은 감시자와 거리 2가 된 칸 (2,0). 거리 3인 (1,0)에서는 치지 않는다.
            Assert.AreEqual(new GridPos(2, 0), hitCells[0]);
            Assert.IsTrue(hitCells.All(c => Math.Abs(c.X - 3) + Math.Abs(c.Y - 1) <= 2));
            Assert.AreEqual(100 - 12 * hitCells.Count, watcher.Hp);
            Assert.IsFalse(s.World.Enemies.Single().IsBlocked);
        }

        [Test]
        public void NoAttack_EnemyNeverHitsUnits()
        {
            var s = Build(new[] { Enemy("wing", 1000, attack: 0, traits: new EnemyTraits(flying: true, rangedReach: 3)) }, new[] { new SpawnEvent(0, "wing", 0) });
            s.TryPlaceUnit(Watcher, new GridPos(3, 1), Direction.Up);
            var hits = 0;
            s.UnitDamaged += (u, d) => hits++;

            Run(s, 300);

            Assert.AreEqual(0, hits);
        }

        // ---- 치유 ----

        [Test]
        public void Heal_RestoresNearbyOtherEnemies_UpToMax_NotSelf_AndSurvivesSnapshot()
        {
            var enemies = new[] { Enemy("mender", 90, traits: new EnemyTraits(healAmount: 15, healIntervalTicks: 90)), Enemy("e", 100) };
            var spawns = new[] { new SpawnEvent(0, "mender", 0), new SpawnEvent(0, "e", 0), new SpawnEvent(0, "e", 0) };
            var s = Build(enemies, spawns);
            Run(s, 1);
            Assert.AreEqual(90, s.World.Enemies[0].HealCooldown, "나온 직후에는 간격만큼 기다린다");

            // 스냅샷으로 HP와 치유 대기를 바꿔 다음 틱에 치유가 일어나게 한다.
            var snapshot = s.World.ToSnapshot();
            snapshot.Enemies[0].Hp = 10;
            snapshot.Enemies[0].HealCooldown = 1;
            snapshot.Enemies[1].Hp = 50;
            snapshot.Enemies[2].Hp = 95;
            var restored = new Simulation(World.FromSnapshot(snapshot), s.Definition);
            var healed = new List<int>();
            restored.EnemyHealed += (e, amount) => healed.Add(amount);

            restored.Step();

            Assert.AreEqual(10, restored.World.Enemies[0].Hp, "자기 자신은 치유하지 않는다");
            Assert.AreEqual(65, restored.World.Enemies[1].Hp);
            Assert.AreEqual(100, restored.World.Enemies[2].Hp, "최대 HP를 넘지 않는다");
            CollectionAssert.AreEqual(new[] { 15, 5 }, healed);
            Assert.AreEqual(90, restored.World.Enemies[0].HealCooldown);
            Assert.AreEqual(90, World.FromSnapshot(restored.World.ToSnapshot()).Enemies[0].HealCooldown);
        }

        [Test]
        public void Heal_DoesNotReachEnemiesTwoCellsAway()
        {
            var enemies = new[] { Enemy("mender", 90, traits: new EnemyTraits(healAmount: 15, healIntervalTicks: 90)), Enemy("e", 100) };
            var s = Build(enemies, new[] { new SpawnEvent(0, "e", 0), new SpawnEvent(60, "mender", 0) });
            Run(s, 61);
            var snapshot = s.World.ToSnapshot();
            snapshot.Enemies[0].Hp = 50;          // e는 두 칸 앞에 있다
            snapshot.Enemies[1].HealCooldown = 1;
            var restored = new Simulation(World.FromSnapshot(snapshot), s.Definition);
            Assert.AreEqual(2, restored.EnemyCell(restored.World.Enemies[0]).X - restored.EnemyCell(restored.World.Enemies[1]).X);

            restored.Step();

            Assert.AreEqual(50, restored.World.Enemies[0].Hp);
        }

        // ---- 무거움 ----

        [Test]
        public void Heavy_UsesBlockCapacity_SoNextEnemyPasses()
        {
            var enemies = new[] { Enemy("brute", 100000, traits: new EnemyTraits(blockWeight: 2)), Enemy("e", 100000) };
            var s = Build(enemies, new[] { new SpawnEvent(0, "brute", 0), new SpawnEvent(30, "e", 0) }, meleeBlock: 2);
            s.TryPlaceUnit(Melee, new GridPos(3, 0), Direction.Right);
            var melee = s.World.Units.Single();
            var leaked = new List<string>();
            s.EnemyLeaked += e => leaked.Add(e.DefinitionId);

            Run(s, 400);

            var brute = s.World.Enemies.Single(e => e.DefinitionId == "brute");
            Assert.AreEqual(melee.Id, brute.BlockedBy);
            Assert.AreEqual(2, s.BlockedWeight(melee));
            CollectionAssert.AreEqual(new[] { "e" }, leaked);
        }

        [TestCase(2, false)]
        [TestCase(3, true)]
        public void Heavy_NeedsEnoughBlock(int meleeBlock, bool expectBlocked)
        {
            var s = Build(new[] { Enemy("lord", 100000, traits: new EnemyTraits(armor: 10, blockWeight: 3)) }, new[] { new SpawnEvent(0, "lord", 0) }, meleeBlock: meleeBlock);
            s.TryPlaceUnit(Melee, new GridPos(3, 0), Direction.Right);
            var blocked = 0;
            s.EnemyBlocked += (e, u) => blocked++;

            Run(s, 300);

            Assert.AreEqual(expectBlocked ? 1 : 0, blocked);
        }

        // ---- 분열 ----

        [Test]
        public void Split_SpawnsChildrenWhereParentDied_AndVictoryWaitsForThem()
        {
            var enemies = new[] { Enemy("splitter", 30, traits: new EnemyTraits(splitInto: "e", splitCount: 2)), Enemy("e", 60) };
            var s = Build(enemies, new[] { new SpawnEvent(0, "splitter", 0) }, meleeAttack: 50);
            s.TryPlaceUnit(Melee, new GridPos(3, 0), Direction.Right);
            var killedProgress = -1;
            var splits = new List<Enemy>();
            s.EnemyKilled += e => { if (e.DefinitionId == "splitter") killedProgress = e.Progress; };
            s.EnemySplit += e => splits.Add(e);

            while (killedProgress < 0 && s.World.Tick < 400) s.Step();

            Assert.GreaterOrEqual(killedProgress, 0);
            Assert.AreEqual(2, splits.Count);
            Assert.IsTrue(splits.All(e => e.DefinitionId == "e" && e.Progress == killedProgress && e.RouteIndex == 0 && e.Hp == 60));
            Assert.AreNotEqual(splits[0].Id, splits[1].Id);
            CollectionAssert.AreEquivalent(splits, s.World.Enemies);
            Assert.AreEqual(BattleOutcome.None, s.World.Outcome, "나뉜 적이 남아 있으면 아직 이기지 않는다");

            Run(s, 600);

            Assert.AreEqual(BattleOutcome.Victory, s.World.Outcome);
            Assert.AreEqual(10, s.World.DefenseHp);
        }

        [Test]
        public void Split_IntoUnknownEnemy_IsRejected()
        {
            Assert.Throws<ArgumentException>(() => Build(new[] { Enemy("splitter", 30, traits: new EnemyTraits(splitInto: "nope", splitCount: 2)) }, new SpawnEvent[0]));
        }

        [Test]
        public void NoTraits_BehaveAsBefore()
        {
            var traits = new EnemyDefinition("plain", 100, 1, new CombatStats(10, 1, 30)).Traits;
            Assert.AreSame(EnemyTraits.None, traits);
            Assert.AreEqual((0, false, 0, 1, false, false), (traits.Armor, traits.Flying, traits.RangedReach, traits.BlockWeight, traits.CanHeal, traits.Splits));
        }
    }
}
