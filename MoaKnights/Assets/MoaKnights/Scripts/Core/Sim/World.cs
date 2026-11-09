using System;
using System.Collections.Generic;

namespace MoaKnights.Core.Sim
{
    [Serializable]
    public sealed class EnemySnapshot
    {
        public int Id;
        public string DefinitionId;
        public int RouteIndex;
        public int Progress;
    }

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
        public int DefenseHp;
        public int NextSpawnIndex;
        public EnemySnapshot[] Enemies;
    }

    /// <summary>한 판의 상태. 시스템은 상태를 갖지 않고 여기만 읽고 쓴다.</summary>
    public sealed class World
    {
        public const int SnapshotVersion = 2;

        private readonly SeededRandom[] _streams;
        private readonly List<Enemy> _enemies = new List<Enemy>();

        public int Seed { get; }
        public string StageId { get; }
        public long Tick { get; private set; }
        public int NextEntityId { get; private set; }
        public int DefenseHp { get; private set; }
        public int NextSpawnIndex { get; private set; }

        /// <summary>살아 있는 적. 나온 순서를 유지한다.</summary>
        public IReadOnlyList<Enemy> Enemies => _enemies;

        private World(int seed, string stageId, long tick, int nextEntityId, ulong[] rngStates, int defenseHp, int nextSpawnIndex)
        {
            Seed = seed;
            StageId = stageId;
            Tick = tick;
            NextEntityId = nextEntityId;
            DefenseHp = defenseHp;
            NextSpawnIndex = nextSpawnIndex;
            _streams = new SeededRandom[rngStates.Length];
            for (var i = 0; i < rngStates.Length; i++) _streams[i] = new SeededRandom(rngStates[i]);
        }

        public static World Create(int seed, string stageId, int defenseHp)
        {
            var streams = (RngStream[])Enum.GetValues(typeof(RngStream));
            var states = new ulong[streams.Length];
            foreach (var stream in streams) states[(int)stream] = SeededRandom.StreamState(seed, stream);
            return new World(seed, stageId, 0L, 1, states, defenseHp, 0);
        }

        public static World FromSnapshot(WorldSnapshot snapshot)
        {
            if (snapshot.Version != SnapshotVersion)
                throw new NotSupportedException("Unsupported snapshot version: " + snapshot.Version);

            var world = new World(snapshot.Seed, snapshot.StageId, snapshot.Tick, snapshot.NextEntityId, snapshot.RngStates, snapshot.DefenseHp, snapshot.NextSpawnIndex);
            foreach (var enemy in snapshot.Enemies)
                world._enemies.Add(new Enemy(enemy.Id, enemy.DefinitionId, enemy.RouteIndex, enemy.Progress));
            return world;
        }

        public WorldSnapshot ToSnapshot()
        {
            var states = new ulong[_streams.Length];
            for (var i = 0; i < _streams.Length; i++) states[i] = _streams[i].State;

            var enemies = new EnemySnapshot[_enemies.Count];
            for (var i = 0; i < _enemies.Count; i++)
            {
                var enemy = _enemies[i];
                enemies[i] = new EnemySnapshot
                {
                    Id = enemy.Id,
                    DefinitionId = enemy.DefinitionId,
                    RouteIndex = enemy.RouteIndex,
                    Progress = enemy.Progress,
                };
            }

            return new WorldSnapshot
            {
                Version = SnapshotVersion,
                Seed = Seed,
                StageId = StageId,
                Tick = Tick,
                NextEntityId = NextEntityId,
                RngStates = states,
                DefenseHp = DefenseHp,
                NextSpawnIndex = NextSpawnIndex,
                Enemies = enemies,
            };
        }

        public SeededRandom Rng(RngStream stream) => _streams[(int)stream];

        public int AllocateEntityId() => NextEntityId++;

        public void AdvanceTick() => Tick++;

        public Enemy SpawnEnemy(string definitionId, int routeIndex)
        {
            NextSpawnIndex++;
            var enemy = new Enemy(AllocateEntityId(), definitionId, routeIndex, 0);
            _enemies.Add(enemy);
            return enemy;
        }

        public void RemoveEnemyAt(int index) => _enemies.RemoveAt(index);

        /// <summary>실제로 깎인 양을 돌려준다. HP는 0 아래로 내려가지 않는다.</summary>
        public int DamageDefense(int amount)
        {
            var applied = Math.Min(amount, DefenseHp);
            DefenseHp -= applied;
            return applied;
        }
    }
}
