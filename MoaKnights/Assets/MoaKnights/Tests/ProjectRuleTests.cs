using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MoaKnights.Core.Map;
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
