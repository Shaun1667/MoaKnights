using System.Collections.Generic;
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
            Assert.AreEqual(10, balance.DragSlowPercent);
            Assert.AreEqual(20f, balance.FirstSpawnDelaySeconds);
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

        /// <summary>BALANCE 5 · 7 · 9번: 능력자 6명 · 포지션 · 3성 · 출전 12명 · 기본 편성, 그리고 편성대로 카드가 만들어지는지.</summary>
        [Test]
        public void GachaView_HasRarityColorsAndTimes()
        {
            var view = AssetDatabase.LoadAssetAtPath<BattleViewAsset>("Assets/MoaKnights/Data/View/BattleView.asset");
            // 등급마다 다른 빛 색 (3성 파랑 · 4성 보라 · 5성 금색), 연출 시간은 모두 0보다 크다.
            Assert.AreNotEqual(view.GachaRarityColor(3), view.GachaRarityColor(4));
            Assert.AreNotEqual(view.GachaRarityColor(4), view.GachaRarityColor(5));
            Assert.Greater(view.GachaRarityColor(3).a, 0f);
            Assert.Greater(view.GachaLightSeconds, 0f);
            Assert.Greater(view.GachaSpreadSeconds, 0f);
            Assert.Greater(view.GachaFlipSeconds, 0f);
            Assert.GreaterOrEqual(view.GachaFlipIntervalSeconds, 0f);
        }

        [Test]
        public void GachaAndCurrency_MatchBalanceDoc()
        {
            var balance = AssetDatabase.LoadAssetAtPath<BattleBalanceAsset>("Assets/MoaKnights/Data/Balance/BattleBalance.asset");
            Assert.AreEqual((10, 10, 90, 2), (balance.GachaSingleCost, balance.GachaMultiCount, balance.GachaMultiCost, balance.GachaDuplicateRefund));
            CollectionAssert.AreEqual(new[] { (3, 90), (4, 8), (5, 2) }, balance.GachaRarityWeights.Select(w => (w.Rarity, w.Weight)).ToArray());
            Assert.AreEqual(10, balance.CurrencyPerStar);
            Assert.AreEqual(100, balance.StartingCurrency);
            CollectionAssert.AreEqual(new[] { "melee_01", "ranged_01", "tank_01", "healer_01" }, balance.StartingUnits);

            // 가진 능력자만 출전한다.
            var owned = balance.StartingUnits;
            CollectionAssert.AreEqual(owned, BattleDefinitionFactory.DefaultDeck(balance, owned));
            Assert.IsFalse(BattleDefinitionFactory.IsValidDeck(balance, new[] { "melee_01", "sniper_01" }, owned));
            Assert.IsTrue(BattleDefinitionFactory.IsValidDeck(balance, new[] { "melee_01", "sniper_01" }, owned.Concat(new[] { "sniper_01" }).ToArray()));

            // 뽑기 대상은 능력자 목록 전부, 등급은 행의 값.
            var rules = BattleDefinitionFactory.CreateGachaRules(balance);
            CollectionAssert.AreEqual(balance.Units.Select(u => u.Id), rules.Pool.Select(e => e.UnitId));
        }

        [Test]
        public void Roster_AndDeck_MatchBalanceDoc()
        {
            var balance = AssetDatabase.LoadAssetAtPath<BattleBalanceAsset>("Assets/MoaKnights/Data/Balance/BattleBalance.asset");
            var stage = AssetDatabase.LoadAssetAtPath<StageAsset>("Assets/MoaKnights/Data/Stages/Stage01.asset");

            var ids = new[]
            {
                "melee_01", "ranged_01", "tank_01", "healer_01", "sniper_01", "vanguard_01", "blade_01", "irene_01",
                "guardian_01", "bulwark_01", "warden_01", "lancer_01", "archer_01", "gunner_01", "mage_01", "cleric_01", "medic_01",
                "mano_01", "paladin_01", "swordmaster_01", "starshot_01",
            };
            CollectionAssert.AreEqual(ids, balance.Units.Select(u => u.Id).ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    UnitRole.Dealer, UnitRole.Dealer, UnitRole.Tank, UnitRole.Healer, UnitRole.Dealer, UnitRole.Dealer, UnitRole.Dealer, UnitRole.Healer,
                    UnitRole.Tank, UnitRole.Tank, UnitRole.Tank, UnitRole.Dealer, UnitRole.Dealer, UnitRole.Dealer, UnitRole.Dealer, UnitRole.Healer, UnitRole.Healer,
                    UnitRole.Dealer, UnitRole.Tank, UnitRole.Dealer, UnitRole.Dealer,
                },
                balance.Units.Select(u => u.Role).ToArray());
            CollectionAssert.AreEqual(
                new[] { 3, 3, 3, 3, 3, 3, 4, 5, 4, 4, 4, 4, 4, 4, 4, 4, 4, 5, 5, 5, 5 },
                balance.Units.Select(u => u.Rarity).ToArray());
            Assert.AreEqual(10, balance.Units.Count(u => u.Rarity == 4));
            Assert.AreEqual(5, balance.Units.Count(u => u.Rarity == 5));
            Assert.AreEqual(12, balance.DeckSize);
            CollectionAssert.AreEqual(ids.Take(12).ToArray(), BattleDefinitionFactory.DefaultDeck(balance));   // 목록 앞 12명

            // G-4 첫 4 · 5성 (BALANCE 5-13 · 5-14).
            var blade = balance.Units.Single(u => u.Id == "blade_01");
            Assert.AreEqual((12, 130, 26, 0.9f, 2, SpType.OnAttack, 10), (blade.Cost, blade.MaxHp, blade.Attack, blade.AttackIntervalSeconds, blade.BlockCount, blade.SpType, blade.MaxSp));
            var irene = balance.Units.Single(u => u.Id == "irene_01");
            Assert.AreEqual((22, 90, 30, 1.4f, 0, SpType.Natural, 18), (irene.Cost, irene.MaxHp, irene.Attack, irene.AttackIntervalSeconds, irene.BlockCount, irene.SpType, irene.MaxSp));
            Assert.AreEqual(5, irene.Range.Length);

            // G-5 나머지 4성 9명 · 5성 4명 (BALANCE 5-15 ~ 5-27, 7-12 ~ 7-24): 코스트 · HP · 공격 · 간격 · 저지 · SP.
            var rare = new (string Id, int Cost, int MaxHp, int Attack, float Interval, int Block, SpType Sp, int MaxSp)[]
            {
                ("guardian_01", 18, 260, 13, 1.5f, 3, SpType.OnHit, 12),
                ("bulwark_01", 20, 300, 15, 2f, 3, SpType.OnHit, 12),
                ("warden_01", 16, 230, 18, 1.2f, 2, SpType.OnHit, 10),
                ("lancer_01", 14, 140, 24, 1.1f, 2, SpType.OnAttack, 12),
                ("archer_01", 14, 75, 20, 1.1f, 0, SpType.Natural, 12),
                ("gunner_01", 15, 70, 14, 0.6f, 0, SpType.OnAttack, 14),
                ("mage_01", 18, 60, 34, 1.6f, 0, SpType.Natural, 15),
                ("cleric_01", 14, 65, 20, 1.5f, 0, SpType.Natural, 14),
                ("medic_01", 15, 70, 16, 1.2f, 0, SpType.Natural, 14),
                ("mano_01", 24, 85, 48, 1.8f, 0, SpType.Natural, 20),
                ("paladin_01", 26, 420, 22, 1.4f, 4, SpType.OnHit, 14),
                ("swordmaster_01", 20, 200, 42, 0.8f, 2, SpType.OnAttack, 12),
                ("starshot_01", 24, 80, 70, 2.2f, 0, SpType.Natural, 20),
            };
            foreach (var expected in rare)
            {
                var unit = balance.Units.Single(u => u.Id == expected.Id);
                Assert.AreEqual(
                    (expected.Cost, expected.MaxHp, expected.Attack, expected.Interval, expected.Block, expected.Sp, expected.MaxSp),
                    (unit.Cost, unit.MaxHp, unit.Attack, unit.AttackIntervalSeconds, unit.BlockCount, unit.SpType, unit.MaxSp), expected.Id);
                Assert.AreEqual(3, unit.Skills.Length, expected.Id);
                Assert.DoesNotThrow(() => AttackRange.Parse(unit.Range), expected.Id);
            }
            var paladin = balance.Units.Single(u => u.Id == "paladin_01");
            Assert.AreEqual((70, 120, 2, 12f), (paladin.Skills[2].HealPercent, paladin.Skills[2].AttackBonusPercent, paladin.Skills[2].BlockBonus, paladin.Skills[2].DurationSeconds));
            CollectionAssert.AreEqual(new[] { "OXXXXX" }, balance.Units.Single(u => u.Id == "starshot_01").Range);

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
        // 출전 12명이 되며 "인원 초과"는 지금 능력자 6명으로 만들 수 없어 뺐다 (2026-11-11).
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
            Assert.AreEqual(10, catalog.Count);
            CollectionAssert.AreEqual(Enumerable.Range(1, 10).Select(n => "stage_" + n.ToString("00")).ToArray(), Enumerable.Range(0, catalog.Count).Select(i => catalog[i].Id).ToArray());

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

            // 6-1 · 6-4 스테이지 4 ~ 10 (BALANCE 4-7 ~ 4-13): 맵 크기 · 입구 수 · 길 길이 · 등장 수. 길은 팩토리가 지상 칸만 지나는지 검사한다.
            var later = new (int Width, int Height, int Entrances, int[] Routes, int Spawns)[]
            {
                (9, 7, 1, new[] { 22 }, 24),
                (9, 7, 1, new[] { 12, 8, 12 }, 29),
                (9, 7, 3, new[] { 8, 7, 7 }, 39),
                (11, 7, 2, new[] { 13, 13 }, 44),
                (11, 9, 1, new[] { 43 }, 69),
                (11, 7, 2, new[] { 11, 11 }, 56),
                (11, 9, 3, new[] { 18, 18, 13 }, 63),
            };
            for (var i = 0; i < later.Length; i++)
            {
                var expected = later[i];
                var definition = BattleDefinitionFactory.Create(catalog[3 + i], balance);
                var label = catalog[3 + i].Id;
                Assert.AreEqual((expected.Width, expected.Height), (definition.Map.Width, definition.Map.Height), label);
                Assert.AreEqual(expected.Entrances, definition.Map.Entrances.Count, label);
                CollectionAssert.AreEqual(expected.Routes, definition.Routes.Select(r => r.Length).ToArray(), label);
                Assert.AreEqual(expected.Spawns, definition.Spawns.Count, label);
            }
            var firstStage = new Dictionary<string, int> { { "dizes_shell", 4 }, { "dizes_wing", 5 }, { "dizes_spitter", 6 }, { "dizes_mender", 7 }, { "dizes_brute", 7 }, { "dizes_splitter", 8 }, { "dizes_lord", 10 } };
            foreach (var pair in firstStage)
            {
                var first = Enumerable.Range(0, catalog.Count).First(i => catalog[i].SpawnGroups.Any(g => g.EnemyId == pair.Key)) + 1;
                Assert.AreEqual(pair.Value, first, pair.Key);
            }
            Assert.AreEqual(1, BattleDefinitionFactory.Create(catalog[9], balance).Spawns.Count(e => e.EnemyId == "dizes_lord"));

            var fast = balance.Enemies.Single(e => e.Id == "dizes_fast");
            Assert.AreEqual((2f, 1, 30, 5, 1f), (fast.SpeedCellsPerSecond, fast.LeakDamage, fast.MaxHp, fast.Attack, fast.AttackIntervalSeconds));
            var tank = balance.Enemies.Single(e => e.Id == "dizes_tank");
            Assert.AreEqual((0.6f, 2, 150, 15, 1.5f), (tank.SpeedCellsPerSecond, tank.LeakDamage, tank.MaxHp, tank.Attack, tank.AttackIntervalSeconds));
            Assert.AreEqual(60, stage2.Enemy("dizes_tank").SpeedPerTick);

            // 6-2 새 적 7종 (BALANCE 3-7 ~ 3-13): 속도 · 누수 · HP · 공격 · 간격 · 방어 · 비행 · 사거리 · 치유 · 무게 · 분열, 이름 글.
            var traitRows = new (string Id, float Speed, int Leak, int Hp, int Attack, float Interval, int Armor, bool Flying, int Reach, int Heal, float HealSeconds, int Weight, string Split, int SplitCount)[]
            {
                ("dizes_shell", 0.8f, 1, 100, 10, 1.5f, 8, false, 0, 0, 0f, 1, "", 0),
                ("dizes_wing", 1.4f, 1, 45, 0, 1f, 0, true, 0, 0, 0f, 1, "", 0),
                ("dizes_spitter", 0.9f, 1, 70, 12, 2f, 0, false, 2, 0, 0f, 1, "", 0),
                ("dizes_mender", 0.9f, 1, 90, 6, 1.5f, 0, false, 0, 15, 3f, 1, "", 0),
                ("dizes_brute", 0.5f, 3, 450, 35, 2f, 0, false, 0, 0, 0f, 2, "", 0),
                ("dizes_splitter", 0.8f, 1, 140, 12, 1.5f, 0, false, 0, 0, 0f, 1, "dizes_fast", 2),
                ("dizes_lord", 0.4f, 10, 2500, 60, 2f, 10, false, 0, 0, 0f, 3, "", 0),
            };
            foreach (var expected in traitRows)
            {
                var row = balance.Enemies.Single(e => e.Id == expected.Id);
                Assert.AreEqual((expected.Speed, expected.Leak, expected.Hp, expected.Attack, expected.Interval), (row.SpeedCellsPerSecond, row.LeakDamage, row.MaxHp, row.Attack, row.AttackIntervalSeconds), expected.Id);
                Assert.AreEqual((expected.Armor, expected.Flying, expected.Reach, expected.Heal, expected.HealSeconds, expected.Weight, expected.Split ?? "", expected.SplitCount),
                    (row.Armor, row.Flying, row.RangedReach, row.HealAmount, row.HealIntervalSeconds, row.BlockWeight, row.SplitInto ?? "", row.SplitCount), expected.Id);
                Assert.IsTrue(table.Contains("enemy." + expected.Id + ".name"), expected.Id);
            }
            var lord = stage2.Enemy("dizes_lord").Traits;
            Assert.AreEqual((10, 3), (lord.Armor, lord.BlockWeight));
            Assert.AreEqual(90, stage2.Enemy("dizes_mender").Traits.HealIntervalTicks);
            foreach (var key in new[] { "armor", "flying", "ranged", "heal", "heavy", "split", "joiner" })
                Assert.IsTrue(table.Contains("enemy.trait." + key), key);
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

        /// <summary>BALANCE 1번 검산: 1초에 1 회복 = 초당 30틱에서 30틱 (2026-11-11 재미 검증용 임시값).</summary>
        [Test]
        public void CostRegen_IsOneSecondInTicks()
        {
            var balance = AssetDatabase.LoadAssetAtPath<BattleBalanceAsset>("Assets/MoaKnights/Data/Balance/BattleBalance.asset");
            var stage = AssetDatabase.LoadAssetAtPath<StageAsset>("Assets/MoaKnights/Data/Stages/Stage01.asset");
            var cost = BattleDefinitionFactory.Create(stage, balance).Cost;
            Assert.AreEqual(40, cost.Start);
            Assert.AreEqual(99, cost.Max);
            Assert.AreEqual(30, cost.RegenTicks);
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

        /// <summary>모델이 있는 능력자는 카드 얼굴도 있다 (Setup이 Art/Units/&lt;id&gt;/&lt;id&gt;_portrait.png를 넣는다).</summary>
        [Test]
        public void EveryUnitWithModel_HasCardPortrait()
        {
            var balance = AssetDatabase.LoadAssetAtPath<BattleBalanceAsset>("Assets/MoaKnights/Data/Balance/BattleBalance.asset");
            var view = AssetDatabase.LoadAssetAtPath<BattleViewAsset>("Assets/MoaKnights/Data/View/BattleView.asset");
            foreach (var unit in balance.Units)
            {
                Assert.IsTrue(view.TryGetUnit(unit.Id, out var visual), unit.Id);
                if (visual.Prefab == null) continue;
                Assert.IsNotNull(visual.Portrait, unit.Id);
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

        /// <summary>BALANCE 4번 검산: 능력자가 없으면 10마리가 모두 새고, 마지막 한 마리가 준비 20초 + 28초 = 48초에 HP를 0으로 만든다.</summary>
        [Test]
        public void Stage01_WithoutUnits_DefenseFallsToZeroAt48Seconds()
        {
            var balance = AssetDatabase.LoadAssetAtPath<BattleBalanceAsset>("Assets/MoaKnights/Data/Balance/BattleBalance.asset");
            var stage = AssetDatabase.LoadAssetAtPath<StageAsset>("Assets/MoaKnights/Data/Stages/Stage01.asset");
            var definition = BattleDefinitionFactory.Create(stage, balance);
            var simulation = new Simulation(World.Create(1, stage.Id, definition.DefenseHp, definition.Cost.Start), definition);

            Assert.AreEqual(10, definition.Routes[0].Length);
            Assert.AreEqual(10, definition.Spawns.Count);

            while (simulation.World.Tick < 48 * balance.TicksPerSecond) simulation.Step();
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
