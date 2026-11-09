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
            Assert.AreEqual(20, balance.StartCost);
            Assert.AreEqual(2f, balance.CostRegenSeconds);
            Assert.AreEqual(99, balance.MaxCost);
            Assert.AreEqual(30, balance.TicksPerSecond);
            Assert.AreEqual(10, balance.DefenseHp);

            var enemy = balance.Enemies.Single(e => e.Id == "dizes_01");
            Assert.AreEqual(1f, enemy.SpeedCellsPerSecond);
            Assert.AreEqual(1, enemy.LeakDamage);
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
            var simulation = new Simulation(World.Create(1, stage.Id, definition.DefenseHp), definition);

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
