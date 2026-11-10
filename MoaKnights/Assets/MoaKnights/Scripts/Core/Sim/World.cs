using System;
using System.Collections.Generic;
using MoaKnights.Core.Map;

namespace MoaKnights.Core.Sim
{
    [Serializable]
    public sealed class EnemySnapshot
    {
        public int Id;
        public string DefinitionId;
        public int RouteIndex;
        public int Progress;
        public int Hp;
        public int AttackCooldown;
        public int BlockedBy;
    }

    [Serializable]
    public sealed class UnitSnapshot
    {
        public int Id;
        public string DefinitionId;
        public int CellX;
        public int CellY;
        public int Direction;
        public int Hp;
        public int AttackCooldown;
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
        public int Cost;
        public int CostRegenTicks;
        public UnitSnapshot[] Units;
        public string[] UsedUnitIds;
    }

    /// <summary>한 판의 상태. 시스템은 상태를 갖지 않고 여기만 읽고 쓴다.</summary>
    public sealed class World
    {
        public const int SnapshotVersion = 4;

        private readonly SeededRandom[] _streams;
        private readonly List<Enemy> _enemies = new List<Enemy>();
        private readonly List<Unit> _units = new List<Unit>();
        private readonly List<string> _usedUnitIds = new List<string>();

        public int Seed { get; }
        public string StageId { get; }
        public long Tick { get; private set; }
        public int NextEntityId { get; private set; }
        public int DefenseHp { get; private set; }
        public int NextSpawnIndex { get; private set; }
        public int Cost { get; private set; }

        /// <summary>다음 코스트 1까지 쌓인 틱.</summary>
        public int CostRegenTicks { get; private set; }

        /// <summary>살아 있는 적. 나온 순서를 유지한다.</summary>
        public IReadOnlyList<Enemy> Enemies => _enemies;

        /// <summary>배치된 능력자. 놓은 순서를 유지한다.</summary>
        public IReadOnlyList<Unit> Units => _units;

        /// <summary>이 판에서 한 번이라도 배치한 능력자 ID. 쓰러져도 남는다 (재배치는 2-2).</summary>
        public IReadOnlyList<string> UsedUnitIds => _usedUnitIds;

        private World(int seed, string stageId, long tick, int nextEntityId, ulong[] rngStates, int defenseHp, int nextSpawnIndex, int cost, int costRegenTicks)
        {
            Seed = seed;
            StageId = stageId;
            Tick = tick;
            NextEntityId = nextEntityId;
            DefenseHp = defenseHp;
            NextSpawnIndex = nextSpawnIndex;
            Cost = cost;
            CostRegenTicks = costRegenTicks;
            _streams = new SeededRandom[rngStates.Length];
            for (var i = 0; i < rngStates.Length; i++) _streams[i] = new SeededRandom(rngStates[i]);
        }

        public static World Create(int seed, string stageId, int defenseHp, int startCost)
        {
            var streams = (RngStream[])Enum.GetValues(typeof(RngStream));
            var states = new ulong[streams.Length];
            foreach (var stream in streams) states[(int)stream] = SeededRandom.StreamState(seed, stream);
            return new World(seed, stageId, 0L, 1, states, defenseHp, 0, startCost, 0);
        }

        public static World FromSnapshot(WorldSnapshot snapshot)
        {
            if (snapshot.Version != SnapshotVersion)
                throw new NotSupportedException("Unsupported snapshot version: " + snapshot.Version);

            var world = new World(snapshot.Seed, snapshot.StageId, snapshot.Tick, snapshot.NextEntityId, snapshot.RngStates,
                snapshot.DefenseHp, snapshot.NextSpawnIndex, snapshot.Cost, snapshot.CostRegenTicks);
            foreach (var enemy in snapshot.Enemies)
                world._enemies.Add(new Enemy(enemy.Id, enemy.DefinitionId, enemy.RouteIndex, enemy.Progress, enemy.Hp, enemy.AttackCooldown, enemy.BlockedBy));
            foreach (var unit in snapshot.Units)
                world._units.Add(new Unit(unit.Id, unit.DefinitionId, new GridPos(unit.CellX, unit.CellY), (Direction)unit.Direction, unit.Hp, unit.AttackCooldown));
            world._usedUnitIds.AddRange(snapshot.UsedUnitIds);
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
                    Hp = enemy.Hp,
                    AttackCooldown = enemy.AttackCooldown,
                    BlockedBy = enemy.BlockedBy,
                };
            }

            var units = new UnitSnapshot[_units.Count];
            for (var i = 0; i < _units.Count; i++)
            {
                var unit = _units[i];
                units[i] = new UnitSnapshot
                {
                    Id = unit.Id,
                    DefinitionId = unit.DefinitionId,
                    CellX = unit.Cell.X,
                    CellY = unit.Cell.Y,
                    Direction = (int)unit.Direction,
                    Hp = unit.Hp,
                    AttackCooldown = unit.AttackCooldown,
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
                Cost = Cost,
                CostRegenTicks = CostRegenTicks,
                Units = units,
                UsedUnitIds = _usedUnitIds.ToArray(),
            };
        }

        public SeededRandom Rng(RngStream stream) => _streams[(int)stream];

        public int AllocateEntityId() => NextEntityId++;

        public void AdvanceTick() => Tick++;

        public Enemy SpawnEnemy(string definitionId, int routeIndex, int hp)
        {
            NextSpawnIndex++;
            var enemy = new Enemy(AllocateEntityId(), definitionId, routeIndex, 0, hp, 0, 0);
            _enemies.Add(enemy);
            return enemy;
        }

        public void RemoveEnemyAt(int index) => _enemies.RemoveAt(index);

        public void RemoveEnemy(Enemy enemy) => _enemies.Remove(enemy);

        /// <summary>실제로 깎인 양을 돌려준다. HP는 0 아래로 내려가지 않는다.</summary>
        public int DamageDefense(int amount)
        {
            var applied = Math.Min(amount, DefenseHp);
            DefenseHp -= applied;
            return applied;
        }

        /// <summary>코스트 회복 한 틱. 최대치면 쌓지 않는다. 코스트가 오르면 true.</summary>
        public bool RegenCost(CostRules rules)
        {
            if (Cost >= rules.Max)
            {
                CostRegenTicks = 0;
                return false;
            }

            CostRegenTicks++;
            if (CostRegenTicks < rules.RegenTicks) return false;

            CostRegenTicks = 0;
            Cost++;
            return true;
        }

        /// <summary>코스트를 더하거나 뺀다. 0과 최대치 사이로 자른다.</summary>
        public void ChangeCost(int amount, CostRules rules)
        {
            Cost = Math.Max(0, Math.Min(rules.Max, Cost + amount));
        }

        public Unit PlaceUnit(string definitionId, GridPos cell, Direction direction, int hp)
        {
            var unit = new Unit(AllocateEntityId(), definitionId, cell, direction, hp, 0);
            _units.Add(unit);
            if (!_usedUnitIds.Contains(definitionId)) _usedUnitIds.Add(definitionId);
            return unit;
        }

        /// <summary>능력자를 판에서 뺀다. 이 능력자가 막고 있던 적은 풀린다.</summary>
        public void RemoveUnit(Unit unit)
        {
            _units.Remove(unit);
            foreach (var enemy in _enemies)
                if (enemy.BlockedBy == unit.Id) enemy.BlockedBy = 0;
        }

        public Unit UnitById(int id)
        {
            foreach (var unit in _units)
                if (unit.Id == id) return unit;
            return null;
        }

        public int BlockedCount(int unitId)
        {
            var count = 0;
            foreach (var enemy in _enemies)
                if (enemy.BlockedBy == unitId) count++;
            return count;
        }

        public Unit UnitAt(GridPos cell)
        {
            foreach (var unit in _units)
                if (unit.Cell.Equals(cell)) return unit;
            return null;
        }

        /// <summary>이 판에서 이미 쓴 능력자인지. 쓰러져도 true다 (재배치는 2-2).</summary>
        public bool IsDeployed(string definitionId) => _usedUnitIds.Contains(definitionId);
    }
}
