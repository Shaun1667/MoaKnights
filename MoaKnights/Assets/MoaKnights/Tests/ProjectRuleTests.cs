using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MoaKnights.Core.Map;
using MoaKnights.Core.Sim;
using MoaKnights.Game;
using MoaKnights.Localization;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MoaKnights.Tests
{
    /// <summary>CLAUDE.md 규칙을 코드로 지킨다.</summary>
    public sealed class ProjectRuleTests
    {
        private static string ScriptsRoot => Path.Combine(Application.dataPath, "MoaKnights", "Scripts");
        private static string TextCsvPath => Path.Combine(Application.dataPath, "MoaKnights", "Data", "Text", "text.csv");

        private static string[] GameScripts() =>
            Directory.GetFiles(ScriptsRoot, "*.cs", SearchOption.AllDirectories);

        private static string StripComments(string code)
        {
            code = Regex.Replace(code, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            return Regex.Replace(code, @"//.*", string.Empty);
        }

        [TestCase("Core/MoaKnights.Core.asmdef")]
        [TestCase("Localization/MoaKnights.Localization.asmdef")]
        public void RuleAssemblies_DoNotReferenceEngine(string relativePath)
        {
            var json = File.ReadAllText(Path.Combine(ScriptsRoot, relativePath));
            StringAssert.IsMatch("\"noEngineReferences\"\\s*:\\s*true", json);
        }

        [Test]
        public void TextTable_EveryKeyHasDefaultLanguage()
        {
            var table = TextTable.Parse(File.ReadAllText(TextCsvPath));
            var missing = table.Keys.Where(k => !table.TryGet(k, table.Languages[0], out _)).ToArray();
            CollectionAssert.IsEmpty(missing);
        }

        [Test]
        public void TextTable_ContainsEveryKeyUsedByCode()
        {
            var table = TextTable.Parse(File.ReadAllText(TextCsvPath));
            var used = GameScripts()
                .SelectMany(path => Regex.Matches(File.ReadAllText(path), "\"(ui|item|enemy|unit|skill|stage)\\.[a-z0-9_.]+\"")
                    .Cast<Match>())
                .Select(m => m.Value.Trim('"'))
                .Distinct();
            CollectionAssert.IsEmpty(used.Where(k => !table.Contains(k)).ToArray());
        }

        [Test]
        public void GameCode_HasNoKoreanStrings()
        {
            var offenders = GameScripts()
                .Where(path => Regex.IsMatch(StripComments(File.ReadAllText(path)), "[가-힣]"))
                .ToArray();
            CollectionAssert.IsEmpty(offenders);
        }

        [Test]
        public void BattleBalance_MatchesBalanceDoc()
        {
            var balance = AssetDatabase.LoadAssetAtPath<BattleBalanceAsset>("Assets/MoaKnights/Data/Balance/BattleBalance.asset");
            Assert.IsNotNull(balance);
            Assert.AreEqual(40, balance.StartCost);
            Assert.AreEqual(1f, balance.CostRegenSeconds);
            Assert.AreEqual(99, balance.MaxCost);
            Assert.AreEqual(30, balance.TicksPerSecond);
            Assert.AreEqual(10, balance.DefenseHp);

            var enemy = balance.Enemies.Single(e => e.Id == "dizes_01");
            Assert.AreEqual(1f, enemy.SpeedCellsPerSecond);
            Assert.AreEqual(1, enemy.LeakDamage);

            var melee = balance.Units.Single(u => u.Id == "melee_01");
            Assert.AreEqual(10, melee.Cost);
            Assert.AreEqual(UnitTerrain.Ground, melee.Terrain);
            var ranged = balance.Units.Single(u => u.Id == "ranged_01");
            Assert.AreEqual(12, ranged.Cost);
            Assert.AreEqual(UnitTerrain.Hill, ranged.Terrain);

            // BALANCE 3 · 5번 전투 수치.
            Assert.AreEqual(60, enemy.MaxHp);
            Assert.AreEqual(10, enemy.Attack);
            Assert.AreEqual(1.5f, enemy.AttackIntervalSeconds);
            Assert.AreEqual(100, melee.MaxHp);
            Assert.AreEqual(20, melee.Attack);
            Assert.AreEqual(1f, melee.AttackIntervalSeconds);
            Assert.AreEqual(2, melee.BlockCount);
            CollectionAssert.AreEqual(new[] { "@X" }, melee.Range);
            Assert.AreEqual(60, ranged.MaxHp);
            Assert.AreEqual(15, ranged.Attack);
            Assert.AreEqual(1.2f, ranged.AttackIntervalSeconds);
            Assert.AreEqual(0, ranged.BlockCount);
            CollectionAssert.AreEqual(new[] { ".XXX", "OXXX", ".XXX" }, ranged.Range);
        }

        /// <summary>BALANCE 5 · 7 · 9번: 능력자 6명 · 포지션 · 출전 4명 · 기본 편성, 그리고 편성대로 카드가 만들어지는지.</summary>
        [Test]
        public void Roster_AndDeck_MatchBalanceDoc()
        {
            var balance = AssetDatabase.LoadAssetAtPath<BattleBalanceAsset>("Assets/MoaKnights/Data/Balance/BattleBalance.asset");
            var stage = AssetDatabase.LoadAssetAtPath<StageAsset>("Assets/MoaKnights/Data/Stages/Stage01.asset");

            CollectionAssert.AreEqual(new[] { "melee_01", "ranged_01", "tank_01", "healer_01", "sniper_01", "vanguard_01" }, balance.Units.Select(u => u.Id).ToArray());
            CollectionAssert.AreEqual(
                new[] { UnitRole.Dealer, UnitRole.Dealer, UnitRole.Tank, UnitRole.Healer, UnitRole.Dealer, UnitRole.Dealer },
                balance.Units.Select(u => u.Role).ToArray());
            Assert.AreEqual(4, balance.DeckSize);
            CollectionAssert.AreEqual(new[] { "melee_01", "ranged_01", "tank_01", "healer_01" }, BattleDefinitionFactory.DefaultDeck(balance));

            var tank = balance.Units.Single(u => u.Id == "tank_01");
            Assert.AreEqual((14, 200, 10, 1.5f, 3), (tank.Cost, tank.MaxHp, tank.Attack, tank.AttackIntervalSeconds, tank.BlockCount));
            var healer = balance.Units.Single(u => u.Id == "healer_01");
            Assert.AreEqual((12, 50, 15, 1.5f, SpType.Natural, 15), (healer.Cost, healer.MaxHp, healer.Attack, healer.AttackIntervalSeconds, healer.SpType, healer.MaxSp));
            var sniper = balance.Units.Single(u => u.Id == "sniper_01");
            Assert.AreEqual((16, 40, 2.5f, 20), (sniper.Cost, sniper.Attack, sniper.AttackIntervalSeconds, sniper.MaxSp));
            CollectionAssert.AreEqual(new[] { "OXXXX" }, sniper.Range);
            var vanguard = balance.Units.Single(u => u.Id == "vanguard_01");
            Assert.AreEqual((6, 70, 12, 0.8f, 1), (vanguard.Cost, vanguard.MaxHp, vanguard.Attack, vanguard.AttackIntervalSeconds, vanguard.BlockCount));

            var custom = BattleDefinitionFactory.Create(stage, balance, new[] { "vanguard_01", "sniper_01" });
            CollectionAssert.AreEqual(new[] { "vanguard_01", "sniper_01" }, custom.Units.Select(u => u.Id).ToArray());
            custom.TryGetUnit("vanguard_01", out var vanguardDef);
            Assert.AreEqual(24, vanguardDef.Stats.AttackIntervalTicks);

            var fallback = BattleDefinitionFactory.Create(stage, balance, new[] { "nobody" });
            CollectionAssert.AreEqual(BattleDefinitionFactory.DefaultDeck(balance), fallback.Units.Select(u => u.Id).ToArray());
        }

        // 편성은 쉼표로 이은 문자열로 받는다 (NUnit은 배열 하나만 넘기면 인자 여러 개로 풀어 버린다).
        [TestCase("")]                                                      // 아무도 없음
        [TestCase("melee_01,melee_01")]                                     // 겹침
        [TestCase("melee_01,ranged_01,tank_01,healer_01,sniper_01")]        // 4명 초과
        [TestCase("nobody")]                                                // 목록에 없음
        public void Deck_RejectsInvalid(string ids)
        {
            var balance = AssetDatabase.LoadAssetAtPath<BattleBalanceAsset>("Assets/MoaKnights/Data/Balance/BattleBalance.asset");
            var deck = ids.Length == 0 ? new string[0] : ids.Split(',');
            Assert.IsFalse(BattleDefinitionFactory.IsValidDeck(balance, deck));
        }

        [Test]
        public void EveryEnemy_HasNameText()
        {
            var balance = AssetDatabase.LoadAssetAtPath<BattleBalanceAsset>("Assets/MoaKnights/Data/Balance/BattleBalance.asset");
            var table = TextTable.Parse(File.ReadAllText(TextCsvPath));
            foreach (var enemy in balance.Enemies)
                Assert.IsTrue(table.Contains("enemy." + enemy.Id + ".name"), enemy.Id);
        }

        /// <summary>BALANCE 4 · 8번: 스테이지 목록 순서, 스테이지 2 · 3 길, 새 적 수치, 별 기준, 스테이지 이름 글.</summary>
        [Test]
        public void StageCatalog_MatchesBalanceDoc_AndHasNames()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<StageCatalogAsset>("Assets/MoaKnights/Data/Stages/StageCatalog.asset");
            var balance = AssetDatabase.LoadAssetAtPath<BattleBalanceAsset>("Assets/MoaKnights/Data/Balance/BattleBalance.asset");
            Assert.IsNotNull(catalog);
            Assert.AreEqual(3, catalog.Count);
            CollectionAssert.AreEqual(new[] { "stage_01", "stage_02", "stage_03" }, Enumerable.Range(0, catalog.Count).Select(i => catalog[i].Id).ToArray());

            var table = TextTable.Parse(File.ReadAllText(TextCsvPath));
            for (var i = 0; i < catalog.Count; i++)
                Assert.IsTrue(table.Contains("stage." + catalog[i].Id + ".name"), catalog[i].Id);

            var stage2 = BattleDefinitionFactory.Create(catalog[1], balance);
            var stage3 = BattleDefinitionFactory.Create(catalog[2], balance);
            CollectionAssert.AreEqual(new[] { 12, 12 }, stage2.Routes.Select(r => r.Length).ToArray());
            CollectionAssert.AreEqual(new[] { 11, 11 }, stage3.Routes.Select(r => r.Length).ToArray());
            Assert.AreEqual(22, stage2.Spawns.Count);
            Assert.AreEqual(20, stage3.Spawns.Count);
            Assert.AreEqual(2, stage3.Map.Entrances.Count);

            var fast = balance.Enemies.Single(e => e.Id == "dizes_fast");
            Assert.AreEqual((2f, 1, 30, 5, 1f), (fast.SpeedCellsPerSecond, fast.LeakDamage, fast.MaxHp, fast.Attack, fast.AttackIntervalSeconds));
            var tank = balance.Enemies.Single(e => e.Id == "dizes_tank");
            Assert.AreEqual((0.6f, 2, 150, 15, 1.5f), (tank.SpeedCellsPerSecond, tank.LeakDamage, tank.MaxHp, tank.Attack, tank.AttackIntervalSeconds));
            Assert.AreEqual(60, stage2.Enemy("dizes_tank").SpeedPerTick);
            Assert.AreEqual(50, balance.TwoStarHpPercent);
        }

        /// <summary>BALANCE 7번: SP 타입 · 최대 SP · 단계별 스킬, 그리고 단계마다 스킬 이름 글이 표에 있는지.</summary>
        [Test]
        public void SkillData_MatchesBalanceDoc_AndHasNames()
        {
            var balance = AssetDatabase.LoadAssetAtPath<BattleBalanceAsset>("Assets/MoaKnights/Data/Balance/BattleBalance.asset");
            var melee = balance.Units.Single(u => u.Id == "melee_01");
            var ranged = balance.Units.Single(u => u.Id == "ranged_01");

            Assert.AreEqual(SpType.OnHit, melee.SpType);
            Assert.AreEqual(8, melee.MaxSp);
            CollectionAssert.AreEqual(new[] { 30, 40, 50 }, melee.Skills.Select(s => s.HealPercent).ToArray());
            CollectionAssert.AreEqual(new[] { 0, 0, 1 }, melee.Skills.Select(s => s.BlockBonus).ToArray());
            CollectionAssert.AreEqual(new[] { 0f, 0f, 8f }, melee.Skills.Select(s => s.DurationSeconds).ToArray());

            Assert.AreEqual(SpType.OnAttack, ranged.SpType);
            Assert.AreEqual(10, ranged.MaxSp);
            CollectionAssert.AreEqual(new[] { 50, 80, 120 }, ranged.Skills.Select(s => s.AttackBonusPercent).ToArray());
            CollectionAssert.AreEqual(new[] { 10f, 10f, 10f }, ranged.Skills.Select(s => s.DurationSeconds).ToArray());

            var table = TextTable.Parse(File.ReadAllText(TextCsvPath));
            foreach (var unit in balance.Units)
                for (var stage = 1; stage <= unit.Skills.Length; stage++)
                    Assert.IsTrue(table.Contains("skill." + unit.Id + "_" + stage + ".name"), unit.Id + " stage " + stage);
        }

        /// <summary>BALANCE 6번: 중첩 · 퇴각 · 재배치 값과, 그 값으로 계산한 근거리 단계별 수치.</summary>
        [Test]
        public void DeployRules_MatchBalanceDoc()
        {
            var balance = AssetDatabase.LoadAssetAtPath<BattleBalanceAsset>("Assets/MoaKnights/Data/Balance/BattleBalance.asset");
            CollectionAssert.AreEqual(new[] { 100, 130, 160 }, balance.StackHpPercents);
            CollectionAssert.AreEqual(new[] { 100, 130, 160 }, balance.StackAttackPercents);
            CollectionAssert.AreEqual(new[] { 0, 0, 1 }, balance.StackBlockBonus);
            Assert.AreEqual(100, balance.StackCostPercent);
            Assert.AreEqual(10f, balance.RedeployCooldownSeconds);
            Assert.AreEqual(5, balance.RedeployCostIncrease);
            Assert.AreEqual(50, balance.RetreatRefundPercent);

            var stage = AssetDatabase.LoadAssetAtPath<StageAsset>("Assets/MoaKnights/Data/Stages/Stage01.asset");
            var definition = BattleDefinitionFactory.Create(stage, balance);
            definition.TryGetUnit("melee_01", out var melee);
            var deploy = definition.Deploy;
            Assert.AreEqual(3, deploy.MaxStage);
            Assert.AreEqual(300, deploy.RedeployCooldownTicks);
            CollectionAssert.AreEqual(new[] { 100, 130, 160 }, new[] { 1, 2, 3 }.Select(s => deploy.MaxHp(melee, s)).ToArray());
            CollectionAssert.AreEqual(new[] { 20, 26, 32 }, new[] { 1, 2, 3 }.Select(s => deploy.Attack(melee, s)).ToArray());
            CollectionAssert.AreEqual(new[] { 2, 2, 3 }, new[] { 1, 2, 3 }.Select(s => deploy.BlockCount(melee, s)).ToArray());
            Assert.AreEqual(10, deploy.StackCost(melee));
            CollectionAssert.AreEqual(new[] { 10, 15, 20 }, new[] { 0, 1, 2 }.Select(n => deploy.DeployCost(melee, n)).ToArray());
        }

        /// <summary>BALANCE 5번 검산: 초 단위 공격 간격이 틱으로 맞게 바뀐다 (1초 30틱, 1.2초 36틱, 1.5초 45틱).</summary>
        [Test]
        public void AttackIntervals_ConvertToTicks()
        {
            var balance = AssetDatabase.LoadAssetAtPath<BattleBalanceAsset>("Assets/MoaKnights/Data/Balance/BattleBalance.asset");
            var stage = AssetDatabase.LoadAssetAtPath<StageAsset>("Assets/MoaKnights/Data/Stages/Stage01.asset");
            var definition = BattleDefinitionFactory.Create(stage, balance);
            definition.TryGetUnit("melee_01", out var melee);
            definition.TryGetUnit("ranged_01", out var ranged);
            Assert.AreEqual(30, melee.Stats.AttackIntervalTicks);
            Assert.AreEqual(36, ranged.Stats.AttackIntervalTicks);
            Assert.AreEqual(45, definition.Enemy("dizes_01").Stats.AttackIntervalTicks);
        }

        /// <summary>BALANCE 1번 검산: 2초에 1 회복 = 초당 30틱에서 60틱.</summary>
        [Test]
        public void CostRegen_IsTwoSecondsInTicks()
        {
            var balance = AssetDatabase.LoadAssetAtPath<BattleBalanceAsset>("Assets/MoaKnights/Data/Balance/BattleBalance.asset");
            var stage = AssetDatabase.LoadAssetAtPath<StageAsset>("Assets/MoaKnights/Data/Stages/Stage01.asset");
            var cost = BattleDefinitionFactory.Create(stage, balance).Cost;
            Assert.AreEqual(20, cost.Start);
            Assert.AreEqual(99, cost.Max);
            Assert.AreEqual(60, cost.RegenTicks);
        }

        [Test]
        public void EveryUnit_HasViewRowAndNameText()
        {
            var balance = AssetDatabase.LoadAssetAtPath<BattleBalanceAsset>("Assets/MoaKnights/Data/Balance/BattleBalance.asset");
            var view = AssetDatabase.LoadAssetAtPath<BattleViewAsset>("Assets/MoaKnights/Data/View/BattleView.asset");
            var table = TextTable.Parse(File.ReadAllText(TextCsvPath));
            CollectionAssert.IsNotEmpty(balance.Units);
            foreach (var unit in balance.Units)
            {
                Assert.IsTrue(view.TryGetUnit(unit.Id, out _), unit.Id);
                Assert.IsTrue(table.Contains("unit." + unit.Id + ".name"), unit.Id);
            }
        }

        [Test]
        public void BattleScene_HudAndCardBarHaveNoEmptyReferences()
        {
            var scene = File.ReadAllText(Path.Combine(Application.dataPath, "MoaKnights", "Scenes", "Battle.unity"));
            var block = Regex.Match(scene, @"MoaKnights\.Game\.BattleHud\s+((?:\s*_\w+: .*\n?)+)");
            Assert.IsTrue(block.Success, "BattleHud not found in Battle.unity");
            StringAssert.DoesNotMatch(@"\{fileID: 0\}", block.Groups[1].Value);
            StringAssert.Contains("MoaKnights.Game.CardBar", scene);
            StringAssert.Contains("InputSystemUIInputModule", scene);
        }

        [Test]
        public void EveryStage_BuildsBattleDefinition()
        {
            var balance = AssetDatabase.LoadAssetAtPath<BattleBalanceAsset>("Assets/MoaKnights/Data/Balance/BattleBalance.asset");
            foreach (var guid in AssetDatabase.FindAssets("t:StageAsset"))
            {
                var stage = AssetDatabase.LoadAssetAtPath<StageAsset>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.DoesNotThrow(() => BattleDefinitionFactory.Create(stage, balance), stage.name);
            }
        }

        /// <summary>BALANCE 4번 검산: 능력자가 없으면 10마리가 모두 새고, 마지막 한 마리가 28초에 HP를 0으로 만든다.</summary>
        [Test]
        public void Stage01_WithoutUnits_DefenseFallsToZeroAt28Seconds()
        {
            var balance = AssetDatabase.LoadAssetAtPath<BattleBalanceAsset>("Assets/MoaKnights/Data/Balance/BattleBalance.asset");
            var stage = AssetDatabase.LoadAssetAtPath<StageAsset>("Assets/MoaKnights/Data/Stages/Stage01.asset");
            var definition = BattleDefinitionFactory.Create(stage, balance);
            var simulation = new Simulation(World.Create(1, stage.Id, definition.DefenseHp, definition.Cost.Start), definition);

            Assert.AreEqual(10, definition.Routes[0].Length);
            Assert.AreEqual(10, definition.Spawns.Count);

            while (simulation.World.Tick < 28 * balance.TicksPerSecond) simulation.Step();
            Assert.AreEqual(1, simulation.World.DefenseHp);

            simulation.Step();
            Assert.AreEqual(0, simulation.World.DefenseHp);
            CollectionAssert.IsEmpty(simulation.World.Enemies);
        }

        [Test]
        public void BattleView_HasRowForEveryEnemy()
        {
            var balance = AssetDatabase.LoadAssetAtPath<BattleBalanceAsset>("Assets/MoaKnights/Data/Balance/BattleBalance.asset");
            var view = AssetDatabase.LoadAssetAtPath<BattleViewAsset>("Assets/MoaKnights/Data/View/BattleView.asset");
            foreach (var enemy in balance.Enemies)
                Assert.IsTrue(view.TryGetEnemy(enemy.Id, out _), enemy.Id);
        }

        [Test]
        public void EveryStage_HasValidMapAndUniqueId()
        {
            var stages = AssetDatabase.FindAssets("t:StageAsset")
                .Select(guid => AssetDatabase.LoadAssetAtPath<StageAsset>(AssetDatabase.GUIDToAssetPath(guid)))
                .ToArray();

            CollectionAssert.IsNotEmpty(stages);
            CollectionAssert.AllItemsAreUnique(stages.Select(s => s.Id).ToArray());
            foreach (var stage in stages)
                Assert.DoesNotThrow(() => GridMap.Parse(stage.MapRows), stage.name);
        }

        [Test]
        public void BattleScene_GameRootHasNoEmptyReferences()
        {
            var scene = File.ReadAllText(Path.Combine(Application.dataPath, "MoaKnights", "Scenes", "Battle.unity"));
            var block = Regex.Match(scene, @"MoaKnights\.Game\.GameRoot\s+((?:\s*_\w+: .*\n?)+)");
            Assert.IsTrue(block.Success, "GameRoot not found in Battle.unity");
            StringAssert.DoesNotMatch(@"\{fileID: 0\}", block.Groups[1].Value);
        }

        [Test]
        public void BattleView_HasRowForEveryCellType()
        {
            var view = AssetDatabase.LoadAssetAtPath<BattleViewAsset>("Assets/MoaKnights/Data/View/BattleView.asset");
            Assert.IsNotNull(view);
            foreach (CellType type in System.Enum.GetValues(typeof(CellType)))
                Assert.IsTrue(view.TryGetCell(type, out _), type.ToString());
        }
    }
}
