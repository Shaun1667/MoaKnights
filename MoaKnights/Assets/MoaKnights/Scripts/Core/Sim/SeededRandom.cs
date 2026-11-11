using System;

namespace MoaKnights.Core.Sim
{
    /// <summary>용도별 난수 스트림. enum은 끝에만 추가한다 — 스냅샷이 순서를 쓴다.</summary>
    public enum RngStream
    {
        Map = 0,
        Combat = 1,
        Item = 2,
        Visual = 3,
        Gacha = 4,   // 판 밖 뽑기 (프로필에 상태 저장)
    }

    /// <summary>SplitMix64 시드 난수. 상태가 64비트 하나라 스냅샷에 그대로 담을 수 있다.</summary>
    public sealed class SeededRandom
    {
        private const ulong Gamma = 0x9E3779B97F4A7C15UL;

        public ulong State { get; private set; }

        public SeededRandom(ulong state)
        {
            State = state;
        }

        /// <summary>시드와 스트림 번호로 서로 독립인 시작 상태를 만든다.</summary>
        public static ulong StreamState(int seed, RngStream stream)
        {
            var mixedSeed = Mix((ulong)(uint)seed + Gamma);
            return Mix(mixedSeed + ((ulong)stream + 1UL) * Gamma);
        }

        public ulong NextUInt64()
        {
            State += Gamma;
            return Mix(State);
        }

        /// <summary>0 이상 maxExclusive 미만의 정수.</summary>
        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            return (int)(((NextUInt64() >> 32) * (ulong)maxExclusive) >> 32);
        }

        private static ulong Mix(ulong z)
        {
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
