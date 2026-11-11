using System;
using System.Collections.Generic;
using MoaKnights.Core.Sim;

namespace MoaKnights.Core.Meta
{
    /// <summary>
    /// 판 밖에서 남는 플레이어 상태: 가진 능력자, 뽑기 재화, 뽑기 난수 상태.
    /// 저장은 화면 계층이 스냅샷으로 바꿔 파일에 쓴다. 규칙은 파일을 모른다.
    /// </summary>
    public sealed class PlayerProfile
    {
        /// <summary>스냅샷 형식이 바뀌면 올린다.</summary>
        public const int SnapshotVersion = 1;

        private readonly List<string> _owned = new List<string>();

        /// <summary>가진 능력자 ID. 얻은 순서.</summary>
        public IReadOnlyList<string> Owned => _owned;

        /// <summary>뽑기 재화.</summary>
        public int Currency { get; private set; }

        /// <summary>뽑기 난수 상태 (SplitMix64). 같은 상태 = 같은 뽑기 결과.</summary>
        public ulong GachaRngState { get; internal set; }

        private PlayerProfile()
        {
        }

        /// <summary>처음 시작하는 프로필: 기본 능력자와 시작 재화.</summary>
        public static PlayerProfile CreateNew(IEnumerable<string> startingUnits, int startingCurrency, int seed)
        {
            if (startingCurrency < 0) throw new ArgumentOutOfRangeException(nameof(startingCurrency));
            var profile = new PlayerProfile { Currency = startingCurrency, GachaRngState = SeededRandom.StreamState(seed, RngStream.Gacha) };
            foreach (var id in startingUnits) profile.AddUnit(id);
            return profile;
        }

        public bool Owns(string unitId) => _owned.Contains(unitId);

        /// <summary>능력자를 얻는다. 이미 있으면 false.</summary>
        public bool AddUnit(string unitId)
        {
            if (string.IsNullOrEmpty(unitId)) throw new ArgumentException("empty unit id", nameof(unitId));
            if (_owned.Contains(unitId)) return false;
            _owned.Add(unitId);
            return true;
        }

        /// <summary>조건에 맞지 않는 능력자를 지운다 (능력자 목록에서 빠진 ID를 저장 파일에서 정리할 때). 지운 수를 돌려준다.</summary>
        public int RemoveUnitsWhere(Predicate<string> remove) => _owned.RemoveAll(remove);

        public void AddCurrency(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Currency += amount;
        }

        /// <summary>재화를 쓴다. 모자라면 아무것도 바꾸지 않고 false.</summary>
        public bool TrySpend(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (Currency < amount) return false;
            Currency -= amount;
            return true;
        }

        public ProfileSnapshot ToSnapshot() => new ProfileSnapshot
        {
            Version = SnapshotVersion,
            Owned = _owned.ToArray(),
            Currency = Currency,
            GachaRngState = GachaRngState,
        };

        public static PlayerProfile FromSnapshot(ProfileSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.Version != SnapshotVersion) throw new InvalidOperationException("profile snapshot version " + snapshot.Version);
            var profile = new PlayerProfile { Currency = snapshot.Currency, GachaRngState = snapshot.GachaRngState };
            foreach (var id in snapshot.Owned ?? new string[0]) profile.AddUnit(id);
            return profile;
        }
    }

    /// <summary>프로필 저장 형식. 필드만 있고, 화면 계층이 JSON으로 쓴다.</summary>
    [Serializable]
    public sealed class ProfileSnapshot
    {
        public int Version;
        public string[] Owned;
        public int Currency;
        public ulong GachaRngState;
    }
}
