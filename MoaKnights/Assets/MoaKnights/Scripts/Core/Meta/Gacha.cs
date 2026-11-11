using System;
using System.Collections.Generic;
using System.Linq;
using MoaKnights.Core.Sim;

namespace MoaKnights.Core.Meta
{
    /// <summary>뽑기 대상 능력자 하나.</summary>
    public readonly struct GachaEntry
    {
        public string UnitId { get; }
        public int Rarity { get; }

        public GachaEntry(string unitId, int rarity)
        {
            UnitId = unitId;
            Rarity = rarity;
        }
    }

    /// <summary>뽑기 · 재화 수치 (BALANCE 10번). 화면 계층이 에셋에서 만든다.</summary>
    public sealed class GachaRules
    {
        public int SingleCost { get; }
        public int MultiCount { get; }
        public int MultiCost { get; }

        /// <summary>이미 가진 능력자가 나오면 돌려주는 재화.</summary>
        public int DuplicateRefund { get; }

        /// <summary>등급별 확률(만분율이 아니라 가중치). 키 = 등급.</summary>
        public IReadOnlyDictionary<int, int> RarityWeights { get; }

        public IReadOnlyList<GachaEntry> Pool { get; }

        /// <summary>승리하면 별 하나에 받는 재화.</summary>
        public int CurrencyPerStar { get; }

        public GachaRules(int singleCost, int multiCount, int multiCost, int duplicateRefund, IReadOnlyDictionary<int, int> rarityWeights, IEnumerable<GachaEntry> pool,
            int currencyPerStar)
        {
            if (singleCost < 0 || multiCost < 0 || duplicateRefund < 0 || currencyPerStar < 0) throw new ArgumentOutOfRangeException(nameof(singleCost));
            if (multiCount <= 0) throw new ArgumentOutOfRangeException(nameof(multiCount));
            if (rarityWeights == null || rarityWeights.Count == 0 || rarityWeights.Values.Any(w => w < 0) || rarityWeights.Values.Sum() <= 0)
                throw new ArgumentException("rarity weights must be non-negative with a positive sum", nameof(rarityWeights));
            var entries = pool?.ToList() ?? throw new ArgumentNullException(nameof(pool));
            if (entries.Count == 0) throw new ArgumentException("empty pool", nameof(pool));

            SingleCost = singleCost;
            MultiCount = multiCount;
            MultiCost = multiCost;
            DuplicateRefund = duplicateRefund;
            RarityWeights = new Dictionary<int, int>(rarityWeights.ToDictionary(p => p.Key, p => p.Value));
            Pool = entries;
            CurrencyPerStar = currencyPerStar;
        }

        public int CostOf(int count) => count == MultiCount ? MultiCost : SingleCost * count;

        /// <summary>판 결과로 받는 재화: 이기면 별 수 x 별 하나 재화, 지면 0.</summary>
        public int RewardFor(bool victory, int stars) => victory ? Math.Max(0, stars) * CurrencyPerStar : 0;
    }

    /// <summary>한 번 뽑은 결과.</summary>
    public readonly struct GachaPull
    {
        public string UnitId { get; }
        public int Rarity { get; }

        /// <summary>처음 얻은 능력자인지. 아니면 Refund만큼 재화로 돌려받았다.</summary>
        public bool IsNew { get; }
        public int Refund { get; }

        public GachaPull(string unitId, int rarity, bool isNew, int refund)
        {
            UnitId = unitId;
            Rarity = rarity;
            IsNew = isNew;
            Refund = refund;
        }
    }

    public enum GachaResult
    {
        Ok = 0,
        NotEnoughCurrency = 1,
        InvalidCount = 2,
    }

    /// <summary>
    /// 뽑기 계산. 등급을 확률로 고르고(그 등급 능력자가 없으면 아래 등급, 그래도 없으면 위 등급), 등급 안에서는 고르게 고른다.
    /// 난수는 프로필의 뽑기 난수 상태로만 — 같은 프로필 = 같은 결과.
    /// </summary>
    public static class Gacha
    {
        public static GachaResult TryRoll(PlayerProfile profile, GachaRules rules, int count, out IReadOnlyList<GachaPull> pulls)
        {
            pulls = Array.Empty<GachaPull>();
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (count != 1 && count != rules.MultiCount) return GachaResult.InvalidCount;
            if (!profile.TrySpend(rules.CostOf(count))) return GachaResult.NotEnoughCurrency;

            var random = new SeededRandom(profile.GachaRngState);
            var results = new List<GachaPull>(count);
            for (var i = 0; i < count; i++)
            {
                var rarity = PickRarity(rules, random);
                var candidates = rules.Pool.Where(e => e.Rarity == rarity).ToList();
                var entry = candidates[random.NextInt(candidates.Count)];

                var isNew = profile.AddUnit(entry.UnitId);
                var refund = isNew ? 0 : rules.DuplicateRefund;
                if (refund > 0) profile.AddCurrency(refund);
                results.Add(new GachaPull(entry.UnitId, entry.Rarity, isNew, refund));
            }
            profile.GachaRngState = random.State;
            pulls = results;
            return GachaResult.Ok;
        }

        /// <summary>뽑기 결과 중 가장 높은 등급. 연출이 빛 색을 정할 때 쓴다.</summary>
        public static int HighestRarity(IReadOnlyList<GachaPull> pulls) => pulls.Count == 0 ? 0 : pulls.Max(p => p.Rarity);

        private static int PickRarity(GachaRules rules, SeededRandom random)
        {
            var weights = rules.RarityWeights.OrderBy(p => p.Key).ToList();
            var roll = random.NextInt(weights.Sum(p => p.Value));
            var rolled = weights[weights.Count - 1].Key;
            foreach (var pair in weights)
            {
                if (roll < pair.Value)
                {
                    rolled = pair.Key;
                    break;
                }
                roll -= pair.Value;
            }
            return AvailableRarity(rules, rolled);
        }

        /// <summary>그 등급 능력자가 없으면 가장 가까운 아래 등급, 아래에도 없으면 가장 가까운 위 등급.</summary>
        public static int AvailableRarity(GachaRules rules, int rarity)
        {
            var available = rules.Pool.Select(e => e.Rarity).Distinct().ToList();
            if (available.Contains(rarity)) return rarity;
            var lower = available.Where(r => r < rarity).ToList();
            return lower.Count > 0 ? lower.Max() : available.Where(r => r > rarity).Min();
        }
    }
}
