using System.Collections.Generic;
using System.IO;
using System.Linq;
using MoaKnights.Core.Meta;
using MoaKnights.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MoaKnights.Tests
{
    /// <summary>판 밖 상태: 프로필(가진 능력자 · 재화 · 저장)과 뽑기.</summary>
    public sealed class MetaTests
    {
        private static readonly GachaEntry[] ThreeStarPool = { new GachaEntry("a", 3), new GachaEntry("b", 3), new GachaEntry("c", 3) };

        private static GachaRules Rules(IEnumerable<GachaEntry> pool = null, Dictionary<int, int> weights = null) => new GachaRules(
            10, 10, 90, 2,
            weights ?? new Dictionary<int, int> { { 3, 90 }, { 4, 8 }, { 5, 2 } },
            pool ?? ThreeStarPool,
            10);

        [Test]
        public void NewProfile_HasStartingUnitsAndCurrency()
        {
            var profile = PlayerProfile.CreateNew(new[] { "a", "b" }, 100, 1);
            CollectionAssert.AreEqual(new[] { "a", "b" }, profile.Owned);
            Assert.AreEqual(100, profile.Currency);
            Assert.IsTrue(profile.Owns("a"));
            Assert.IsFalse(profile.Owns("c"));
            Assert.IsFalse(profile.AddUnit("a"));
        }

        [Test]
        public void TrySpend_FailsWithoutChangingCurrency()
        {
            var profile = PlayerProfile.CreateNew(new[] { "a" }, 5, 1);
            Assert.IsFalse(profile.TrySpend(6));
            Assert.AreEqual(5, profile.Currency);
            Assert.IsTrue(profile.TrySpend(5));
            Assert.AreEqual(0, profile.Currency);
        }

        [Test]
        public void Snapshot_RoundTrips_ThroughJson()
        {
            var profile = PlayerProfile.CreateNew(new[] { "a", "b" }, 37, 12345);
            Gacha.TryRoll(profile, Rules(), 1, out _);

            var json = JsonUtility.ToJson(profile.ToSnapshot());
            var loaded = PlayerProfile.FromSnapshot(JsonUtility.FromJson<ProfileSnapshot>(json));

            CollectionAssert.AreEqual(profile.Owned, loaded.Owned);
            Assert.AreEqual(profile.Currency, loaded.Currency);
            Assert.AreEqual(profile.GachaRngState, loaded.GachaRngState);
        }

        [Test]
        public void Snapshot_WrongVersion_Throws()
        {
            var snapshot = PlayerProfile.CreateNew(new[] { "a" }, 0, 1).ToSnapshot();
            snapshot.Version = PlayerProfile.SnapshotVersion + 1;
            Assert.Throws<System.InvalidOperationException>(() => PlayerProfile.FromSnapshot(snapshot));
        }

        [Test]
        public void Roll_SameProfile_SameResult()
        {
            var first = PlayerProfile.CreateNew(new[] { "a" }, 200, 7);
            var second = PlayerProfile.FromSnapshot(first.ToSnapshot());

            Gacha.TryRoll(first, Rules(), 10, out var a);
            Gacha.TryRoll(second, Rules(), 10, out var b);

            CollectionAssert.AreEqual(a.Select(p => p.UnitId), b.Select(p => p.UnitId));
            Assert.AreEqual(first.GachaRngState, second.GachaRngState);
            Assert.AreEqual(first.Currency, second.Currency);
        }

        [Test]
        public void Roll_AdvancesRng_SoNextRollDiffers()
        {
            var profile = PlayerProfile.CreateNew(new[] { "a" }, 1000, 7);
            var before = profile.GachaRngState;
            Gacha.TryRoll(profile, Rules(), 10, out _);
            Assert.AreNotEqual(before, profile.GachaRngState);
        }

        [Test]
        public void Roll_SpendsCost_AndRefundsDuplicates()
        {
            var profile = PlayerProfile.CreateNew(new[] { "a" }, 90, 3);
            Assert.AreEqual(GachaResult.Ok, Gacha.TryRoll(profile, Rules(), 10, out var pulls));
            Assert.AreEqual(10, pulls.Count);

            // 처음 나온 능력자만 새것, 나머지는 2씩 돌려받는다.
            var duplicates = pulls.Count(p => !p.IsNew);
            Assert.AreEqual(pulls.Select(p => p.UnitId).Distinct().Count(p => p != "a"), pulls.Count(p => p.IsNew));
            Assert.IsTrue(pulls.Where(p => p.IsNew).All(p => p.Refund == 0));
            Assert.IsTrue(pulls.Where(p => !p.IsNew).All(p => p.Refund == 2));
            Assert.AreEqual(duplicates * 2, profile.Currency);
            Assert.IsTrue(pulls.All(p => profile.Owns(p.UnitId)));
        }

        [Test]
        public void Roll_NotEnoughCurrency_ChangesNothing()
        {
            var profile = PlayerProfile.CreateNew(new[] { "a" }, 89, 3);
            var state = profile.GachaRngState;
            Assert.AreEqual(GachaResult.NotEnoughCurrency, Gacha.TryRoll(profile, Rules(), 10, out var pulls));
            Assert.AreEqual(0, pulls.Count);
            Assert.AreEqual(89, profile.Currency);
            Assert.AreEqual(state, profile.GachaRngState);
            CollectionAssert.AreEqual(new[] { "a" }, profile.Owned);
        }

        [Test]
        public void Roll_OnlyOneOrMultiCount()
        {
            var profile = PlayerProfile.CreateNew(new[] { "a" }, 1000, 3);
            Assert.AreEqual(GachaResult.InvalidCount, Gacha.TryRoll(profile, Rules(), 3, out _));
            Assert.AreEqual(1000, profile.Currency);
            Assert.AreEqual(10, Rules().CostOf(1));
            Assert.AreEqual(90, Rules().CostOf(10));
        }

        [Test]
        public void MissingRarity_FallsBackDownThenUp()
        {
            var rules = Rules(new[] { new GachaEntry("a", 3), new GachaEntry("e", 5) });
            Assert.AreEqual(3, Gacha.AvailableRarity(rules, 4));
            Assert.AreEqual(5, Gacha.AvailableRarity(rules, 5));
            Assert.AreEqual(3, Gacha.AvailableRarity(rules, 3));

            var onlyFive = Rules(new[] { new GachaEntry("e", 5) });
            Assert.AreEqual(5, Gacha.AvailableRarity(onlyFive, 3));
        }

        [Test]
        public void ThreeStarOnlyPool_AlwaysGivesThreeStar()
        {
            var profile = PlayerProfile.CreateNew(new[] { "a" }, 10000, 99);
            for (var i = 0; i < 10; i++)
            {
                Gacha.TryRoll(profile, Rules(), 10, out var pulls);
                Assert.IsTrue(pulls.All(p => p.Rarity == 3));
            }
        }

        [Test]
        public void RarityWeights_AreRespected()
        {
            // 4성만 가중치가 있으면 4성만 나온다.
            var pool = new[] { new GachaEntry("a", 3), new GachaEntry("d", 4), new GachaEntry("e", 5) };
            var rules = Rules(pool, new Dictionary<int, int> { { 3, 0 }, { 4, 1 }, { 5, 0 } });
            var profile = PlayerProfile.CreateNew(new[] { "a" }, 1000, 5);
            Gacha.TryRoll(profile, rules, 10, out var pulls);
            Assert.IsTrue(pulls.All(p => p.UnitId == "d"));
            Assert.AreEqual(4, Gacha.HighestRarity(pulls));
        }

        [Test]
        public void VictoryReward_IsStarsTimesPerStar()
        {
            var rules = Rules();
            Assert.AreEqual(30, rules.RewardFor(true, 3));
            Assert.AreEqual(10, rules.RewardFor(true, 1));
            Assert.AreEqual(0, rules.RewardFor(false, 0));
        }

        [Test]
        public void ProfileStore_SavesAndLoads_AndDropsUnknownUnits()
        {
            var balance = AssetDatabase.LoadAssetAtPath<BattleBalanceAsset>("Assets/MoaKnights/Data/Balance/BattleBalance.asset");
            var path = Path.Combine(Path.GetTempPath(), "moaknights_profile_test_" + System.Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var created = ProfileStore.Load(balance, path, 1);
                CollectionAssert.AreEqual(balance.StartingUnits, created.Owned);
                Assert.AreEqual(balance.StartingCurrency, created.Currency);

                created.AddUnit("removed_unit");
                created.AddCurrency(5);
                ProfileStore.Save(created, path);

                var loaded = ProfileStore.Load(balance, path, 2);
                CollectionAssert.AreEqual(balance.StartingUnits, loaded.Owned);
                Assert.AreEqual(balance.StartingCurrency + 5, loaded.Currency);
                Assert.AreEqual(created.GachaRngState, loaded.GachaRngState);

                File.WriteAllText(path, "not json");
                var broken = ProfileStore.Load(balance, path, 3);
                Assert.AreEqual(balance.StartingCurrency, broken.Currency);
            }
            finally
            {
                ProfileStore.Delete(path);
            }
        }
    }
}
