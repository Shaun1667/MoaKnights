using System;

namespace MoaKnights.Core.Sim
{
    /// <summary>저장할 상태 전부. 새 상태 = 필드 + 버전 증가 + 왕복 테스트.</summary>
    [Serializable]
    public sealed class WorldSnapshot
    {
        public int Version;
        public int Seed;
        public string StageId;
        public long Tick;
        public int NextEntityId;
        public ulong[] RngStates;
    }

    /// <summary>한 판의 상태. 시스템은 상태를 갖지 않고 여기만 읽고 쓴다.</summary>
    public sealed class World
    {
        public const int SnapshotVersion = 1;

        private readonly SeededRandom[] _streams;

        public int Seed { get; }
        public string StageId { get; }
        public long Tick { get; private set; }
        public int NextEntityId { get; private set; }

        private World(int seed, string stageId, long tick, int nextEntityId, ulong[] rngStates)
        {
            Seed = seed;
            StageId = stageId;
            Tick = tick;
            NextEntityId = nextEntityId;
            _streams = new SeededRandom[rngStates.Length];
            for (var i = 0; i < rngStates.Length; i++) _streams[i] = new SeededRandom(rngStates[i]);
        }

        public static World Create(int seed, string stageId)
        {
            var streams = (RngStream[])Enum.GetValues(typeof(RngStream));
            var states = new ulong[streams.Length];
            foreach (var stream in streams) states[(int)stream] = SeededRandom.StreamState(seed, stream);
            return new World(seed, stageId, 0L, 1, states);
        }

        public static World FromSnapshot(WorldSnapshot snapshot)
        {
            if (snapshot.Version != SnapshotVersion)
                throw new NotSupportedException("Unsupported snapshot version: " + snapshot.Version);
            return new World(snapshot.Seed, snapshot.StageId, snapshot.Tick, snapshot.NextEntityId, snapshot.RngStates);
        }

        public WorldSnapshot ToSnapshot()
        {
            var states = new ulong[_streams.Length];
            for (var i = 0; i < _streams.Length; i++) states[i] = _streams[i].State;
            return new WorldSnapshot
            {
                Version = SnapshotVersion,
                Seed = Seed,
                StageId = StageId,
                Tick = Tick,
                NextEntityId = NextEntityId,
                RngStates = states,
            };
        }

        public SeededRandom Rng(RngStream stream) => _streams[(int)stream];

        public int AllocateEntityId() => NextEntityId++;

        public void AdvanceTick() => Tick++;
    }
}
