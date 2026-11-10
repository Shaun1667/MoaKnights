using System.Collections.Generic;
using System.Linq;
using MoaKnights.Core.Map;
using MoaKnights.Core.Sim;
using NUnit.Framework;

namespace MoaKnights.Tests
{
    public sealed class SkillTests
    {
        private const int Tps = 30;
        private const string Melee = "melee";
        private const string Ranged = "ranged";
        private const string EnemyId = "e";

        // y=1: H H H H H
        // y=0: S . . . E   (길 0,0 → 4,0)
        private static readonly string[] Map = { "HHHHH", "S...E" };

        private sealed class Setup
        {
            public SkillSet MeleeSkills = Skills(SpType.OnHit, 3, 0, Heal(30), Heal(40), Combo(50, 0, 1, 60));
            public SkillSet RangedSkills = Skills(SpType.OnAttack, 4, 0, Buff(50, 45), Buff(80, 45), Buff(120, 45));
            public int MeleeHp = 100;
            public int MeleeAttack = 20;
            public int MeleeBlock = 1;
            public int EnemyHp = 1000;
            public int EnemyAttack = 10;
            public long[] Spawns = new long[0];
        }

        private static SkillSet Skills(SpType type, int maxSp, int naturalTicks, params SkillDefinition[] stages) => new SkillSet(type, maxSp, naturalTicks, stages);
        private static SkillDefinition Heal(int percent) => new SkillDefinition(percent, 0, 0, 0);
        private static SkillDefinition Buff(int attackPercent, int ticks) => new SkillDefinition(0, attackPercent, 0, ticks);
        private static SkillDefinition Combo(int heal, int attack, int block, int ticks) => new SkillDefinition(heal, attack, block, ticks);

        private static Simulation Build(Setup s)
        {
            var map = GridMap.Parse(Map);
            var definition = new BattleDefinition(
                map, Tps, 10,
                new[] { new EnemyDefinition(EnemyId, 100, 1, new CombatStats(s.EnemyHp, s.EnemyAttack, 30)) },
                new[] { EnemyRoute.Parse(map, "0,0 4,0") },
                s.Spawns.Select(t => new SpawnEvent(t, EnemyId, 0)),
                new CostRules(99, 99, 1000000),
                new[]
                {
                    new UnitDefinition(Melee, 10, UnitTerrain.Ground, new CombatStats(s.MeleeHp, s.MeleeAttack, 30), s.MeleeBlock, AttackRange.Parse(new[] { "@X" }), s.MeleeSkills, UnitRole.Dealer),
                    new UnitDefinition(Ranged, 12, UnitTerrain.Hill, new CombatStats(60, 15, 30), 0, AttackRange.Parse(new[] { "OX" }), s.RangedSkills, UnitRole.Dealer),
                },
                TestRules.Deploy, TestRules.TwoStarHpPercent);
            return new Simulation(World.Create(1, "s", 10, 99), definition);
        }

        private static void Run(Simulation simulation, int steps)
        {
            for (var i = 0; i < steps; i++) simulation.Step();
        }

        private static Unit Of(Simulation s, string id) => s.World.UnitOf(id);

        /// <summary>근거리가 (3,0)에서 적을 막고, 원거리가 바로 위 언덕 (3,1)에서 그 칸을 친다.</summary>
        private static Setup RangedSetup(long[] spawns = null) =>
            new Setup { MeleeAttack = 0, EnemyAttack = 0, Spawns = spawns ?? new long[] { 0 } };

        private static void PlaceRangedWithBlocker(Simulation s)
        {
            s.TryPlaceUnit(Melee, new GridPos(3, 0), Direction.Left);
            s.TryPlaceUnit(Ranged, new GridPos(3, 1), Direction.Down);
        }

        // ---- SP 3타입 ----

        [Test]
        public void OnAttack_GainsOnePerAttack_UpToMax()
        {
            var s = Build(RangedSetup());
            PlaceRangedWithBlocker(s);
            var attacks = 0;
            s.EnemyDamaged += (e, d) => { if (d > 0) attacks++; };

            Run(s, 200);
            Assert.That(attacks, Is.GreaterThanOrEqualTo(4));
            Assert.AreEqual(4, Of(s, Ranged).Sp);
        }

        [Test]
        public void OnHit_GainsOnePerHitTaken()
        {
            var s = Build(new Setup { MeleeAttack = 0, Spawns = new long[] { 0 } });
            s.TryPlaceUnit(Melee, new GridPos(2, 0), Direction.Right);
            var hits = 0;
            s.UnitDamaged += (u, d) => hits++;

            Run(s, 100);
            Assert.That(hits, Is.InRange(1, 2));
            Assert.AreEqual(hits, Of(s, Melee).Sp);
        }

        [Test]
        public void OnHit_DoesNotGainFromOwnAttacks()
        {
            // 저지 0이라 적이 막히지 않고 지나가며 근거리에게 맞기만 한다.
            var s = Build(new Setup { EnemyAttack = 0, MeleeBlock = 0, Spawns = new long[] { 0 } });
            s.TryPlaceUnit(Melee, new GridPos(2, 0), Direction.Left);
            var hits = 0;
            s.EnemyDamaged += (e, d) => hits++;
            Run(s, 200);
            Assert.Greater(hits, 0);
            Assert.AreEqual(0, Of(s, Melee).Sp);
        }

        [Test]
        public void Natural_GainsOnePerRegenTicks_UpToMax()
        {
            var s = Build(new Setup { MeleeSkills = Skills(SpType.Natural, 3, 30, Heal(10), Heal(10), Heal(10)) });
            s.TryPlaceUnit(Melee, new GridPos(2, 0), Direction.Left);

            Run(s, 29);
            Assert.AreEqual(0, Of(s, Melee).Sp);
            Run(s, 1);
            Assert.AreEqual(1, Of(s, Melee).Sp);
            Run(s, 300);
            Assert.AreEqual(3, Of(s, Melee).Sp);
        }

        // ---- 쓰기 ----

        [Test]
        public void CheckSkill_RejectsUntilFull()
        {
            var s = Build(new Setup { MeleeSkills = Skills(SpType.Natural, 2, 10, Heal(10), Heal(10), Heal(10)) });
            Assert.AreEqual(PlacementResult.NotDeployed, s.CheckSkill(999));
            s.TryPlaceUnit(Melee, new GridPos(2, 0), Direction.Left);
            var id = Of(s, Melee).Id;
            Assert.AreEqual(PlacementResult.SpNotFull, s.TryUseSkill(id));
            Run(s, 20);
            Assert.AreEqual(PlacementResult.Ok, s.CheckSkill(id));
        }

        [Test]
        public void Heal_RestoresPercentOfMax_CappedAtMax_AndResetsSp()
        {
            var s = Build(new Setup { MeleeAttack = 0, EnemyAttack = 20, Spawns = new long[] { 0 } });
            s.TryPlaceUnit(Melee, new GridPos(2, 0), Direction.Right);
            var healed = new List<int>();
            s.UnitHealed += (u, h) => healed.Add(h);

            Run(s, 130);   // 3대 맞아 HP 40, SP 3
            var unit = Of(s, Melee);
            Assert.AreEqual(40, unit.Hp);
            Assert.AreEqual(3, unit.Sp);

            Assert.AreEqual(PlacementResult.Ok, s.TryUseSkill(unit.Id));
            Assert.AreEqual(70, unit.Hp);          // 최대 100의 30%
            Assert.AreEqual(0, unit.Sp);
            CollectionAssert.AreEqual(new[] { 30 }, healed);
        }

        [Test]
        public void Heal_DoesNotOverheal()
        {
            var s = Build(new Setup { MeleeSkills = Skills(SpType.Natural, 1, 1, Heal(30), Heal(30), Heal(30)) });
            s.TryPlaceUnit(Melee, new GridPos(2, 0), Direction.Left);
            Run(s, 1);
            var healed = 0;
            s.UnitHealed += (u, h) => healed += h;
            s.TryUseSkill(Of(s, Melee).Id);
            Assert.AreEqual(100, Of(s, Melee).Hp);
            Assert.AreEqual(0, healed);
        }

        [Test]
        public void AttackBuff_RaisesDamage_ForDuration_ThenEnds()
        {
            var s = Build(RangedSetup());
            PlaceRangedWithBlocker(s);
            Run(s, 300);   // SP 4 가득
            var unit = Of(s, Ranged);
            var damage = new List<int>();
            var ended = 0;
            s.EnemyDamaged += (e, d) => damage.Add(d);
            s.SkillEnded += u => ended++;

            Assert.AreEqual(PlacementResult.Ok, s.TryUseSkill(unit.Id));
            Assert.AreEqual(22, s.AttackOf(unit));     // 15 x 150% = 22 (내림)
            Assert.AreEqual(PlacementResult.SkillActive, s.CheckSkill(unit.Id));

            Run(s, 44);
            Assert.IsTrue(unit.IsSkillActive);
            Run(s, 1);
            Assert.IsFalse(unit.IsSkillActive);
            Assert.AreEqual(1, ended);
            Assert.AreEqual(15, s.AttackOf(unit));
            Assert.IsTrue(damage.Contains(22));
        }

        [Test]
        public void NoSpGain_WhileSkillActive()
        {
            var s = Build(RangedSetup());
            PlaceRangedWithBlocker(s);
            Run(s, 300);
            var unit = Of(s, Ranged);
            s.TryUseSkill(unit.Id);
            Run(s, 44);
            Assert.AreEqual(0, unit.Sp);
        }

        // ---- 단계별 스킬 (중첩마다 바뀜) ----

        [Test]
        public void SkillDependsOnStage()
        {
            var s = Build(RangedSetup());
            PlaceRangedWithBlocker(s);
            s.TryStackUnit(Ranged);
            s.TryStackUnit(Ranged);
            Run(s, 300);
            var unit = Of(s, Ranged);
            s.TryUseSkill(unit.Id);
            Assert.AreEqual(3, unit.ActiveSkillStage);
            Assert.AreEqual(52, s.AttackOf(unit));     // 15 x 160% = 24, x 220% = 52 (내림)
        }

        [Test]
        public void StackingDuringSkill_KeepsActiveSkillStage()
        {
            var s = Build(RangedSetup());
            PlaceRangedWithBlocker(s);
            Run(s, 300);
            var unit = Of(s, Ranged);
            s.TryUseSkill(unit.Id);
            s.TryStackUnit(Ranged);
            Assert.AreEqual(2, unit.Stage);
            Assert.AreEqual(1, unit.ActiveSkillStage);
            Assert.AreEqual(28, s.AttackOf(unit));     // 2단계 공격 15 x 130% = 19 (내림), 1단계 스킬 +50% → 19 x 150% = 28 (내림)
        }

        [Test]
        public void BlockBonus_BlocksMore_ThenReleasesExtraOnEnd()
        {
            // 3단계 근거리 스킬: 저지 +1, 60틱. 기본 저지 1 + 3단계 보너스 1 = 2, 스킬 중 3.
            var s = Build(new Setup
            {
                MeleeAttack = 0,
                EnemyAttack = 0,
                MeleeSkills = Skills(SpType.Natural, 1, 1, Heal(0), Heal(0), Combo(0, 0, 1, 60)),
                Spawns = new long[] { 0, 5, 10, 15 },
            });
            s.TryPlaceUnit(Melee, new GridPos(2, 0), Direction.Right);
            s.TryStackUnit(Melee);
            s.TryStackUnit(Melee);
            Run(s, 1);
            var unit = Of(s, Melee);
            s.TryUseSkill(unit.Id);
            Assert.AreEqual(3, s.BlockOf(unit));

            Run(s, 59);
            Assert.AreEqual(3, s.World.BlockedCount(unit.Id));

            Run(s, 1);
            Assert.IsFalse(unit.IsSkillActive);
            Assert.AreEqual(2, s.BlockOf(unit));
            Assert.AreEqual(2, s.World.BlockedCount(unit.Id));
        }

        // ---- 퇴각 · 스냅샷 · 배속 ----

        [Test]
        public void Redeploy_StartsWithEmptySp()
        {
            var s = Build(new Setup { MeleeSkills = Skills(SpType.Natural, 3, 1, Heal(10), Heal(10), Heal(10)) });
            s.TryPlaceUnit(Melee, new GridPos(2, 0), Direction.Left);
            Run(s, 5);
            s.TryRetreat(Of(s, Melee).Id);
            Run(s, TestRules.Deploy.RedeployCooldownTicks);
            s.TryPlaceUnit(Melee, new GridPos(2, 0), Direction.Left);
            Assert.AreEqual(0, Of(s, Melee).Sp);
        }

        [Test]
        public void Snapshot_WithActiveSkill_ContinuesIdentically()
        {
            var original = Build(RangedSetup(new long[] { 0, 20 }));
            PlaceRangedWithBlocker(original);
            Run(original, 300);
            original.TryUseSkill(Of(original, Ranged).Id);
            Run(original, 10);

            var restored = new Simulation(World.FromSnapshot(original.World.ToSnapshot()), original.Definition);
            var a = Of(original, Ranged);
            var b = Of(restored, Ranged);
            Assert.AreEqual((a.Sp, a.SkillTicksLeft, a.ActiveSkillStage), (b.Sp, b.SkillTicksLeft, b.ActiveSkillStage));

            Run(original, 100);
            Run(restored, 100);
            Assert.AreEqual((a.Sp, a.SkillTicksLeft, a.ActiveSkillStage, a.SpRegenTicks), (b.Sp, b.SkillTicksLeft, b.ActiveSkillStage, b.SpRegenTicks));
            CollectionAssert.AreEqual(original.World.Enemies.Select(e => (e.Id, e.Hp, e.Progress)).ToArray(),
                restored.World.Enemies.Select(e => (e.Id, e.Hp, e.Progress)).ToArray());
        }

        [Test]
        public void DoubleSpeed_DoublesCostRegenAndEnemyProgress_PerRealSecond()
        {
            // 기획 4-9: 2배속은 적 진행과 코스트 회복 모두 2배. 배속은 틱 수만 바꾼다.
            var normalCost = RunOneRealSecond(1, out var normalProgress);
            var fastCost = RunOneRealSecond(2, out var fastProgress);
            Assert.That(fastCost, Is.InRange(normalCost * 2 - 1, normalCost * 2 + 1));
            Assert.That(fastProgress, Is.InRange(normalProgress * 2 - 200, normalProgress * 2 + 200));
        }

        /// <summary>실제 1초(60프레임)를 TickClock으로 돌려 코스트와 첫 적의 진행도를 돌려준다.</summary>
        private static int RunOneRealSecond(int speed, out int progress)
        {
            var map = GridMap.Parse(Map);
            var definition = new BattleDefinition(map, Tps, 10,
                new[] { new EnemyDefinition(EnemyId, 100, 1, new CombatStats(1000, 0, 30)) },
                new[] { EnemyRoute.Parse(map, "0,0 4,0") },
                new[] { new SpawnEvent(0, EnemyId, 0) },
                new CostRules(0, 99, 15), new UnitDefinition[0], TestRules.Deploy, TestRules.TwoStarHpPercent);
            var simulation = new Simulation(World.Create(1, "s", 10, 0), definition);

            var clock = new TickClock(Tps);
            clock.SetSpeed(speed);
            for (var frame = 0; frame < 60; frame++)
            {
                var ticks = clock.Advance(1d / 60d);
                for (var t = 0; t < ticks; t++) simulation.Step();
            }

            progress = simulation.World.Enemies[0].Progress;
            return simulation.World.Cost;
        }

        [Test]
        public void Definition_RequiresOneSkillPerStage()
        {
            var map = GridMap.Parse(Map);
            var twoStages = Skills(SpType.OnHit, 3, 0, Heal(10), Heal(10));
            Assert.Throws<System.ArgumentException>(() => new BattleDefinition(map, Tps, 10, new EnemyDefinition[0], new EnemyRoute[0], new SpawnEvent[0],
                new CostRules(10, 99, 60),
                new[] { new UnitDefinition(Melee, 10, UnitTerrain.Ground, new CombatStats(10, 1, 30), 1, AttackRange.Parse(new[] { "@" }), twoStages, UnitRole.Dealer) },
                TestRules.Deploy, TestRules.TwoStarHpPercent));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => Skills(SpType.Natural, 3, 0, Heal(10)));
        }
    }
}
