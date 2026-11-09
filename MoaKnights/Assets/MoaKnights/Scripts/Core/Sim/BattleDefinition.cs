using System;
using System.Collections.Generic;
using System.Linq;
using MoaKnights.Core.Map;

namespace MoaKnights.Core.Sim
{
    /// <summary>적 한 종류의 규칙 값.</summary>
    public sealed class EnemyDefinition
    {
        public string Id { get; }

        /// <summary>틱마다 늘어나는 진행도. 값 100이 초당 1칸이다 (BattleDefinition.UnitsPerCell 참고).</summary>
        public int SpeedPerTick { get; }

        public int LeakDamage { get; }

        public EnemyDefinition(string id, int speedPerTick, int leakDamage)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Enemy id is empty.");
            if (speedPerTick <= 0) throw new ArgumentOutOfRangeException(nameof(speedPerTick), "Enemy " + id);
            if (leakDamage < 0) throw new ArgumentOutOfRangeException(nameof(leakDamage), "Enemy " + id);
            Id = id;
            SpeedPerTick = speedPerTick;
            LeakDamage = leakDamage;
        }
    }

    /// <summary>적 한 마리가 나오는 시각과 길.</summary>
    public readonly struct SpawnEvent
    {
        public readonly long Tick;
        public readonly string EnemyId;
        public readonly int RouteIndex;

        public SpawnEvent(long tick, string enemyId, int routeIndex)
        {
            Tick = tick;
            EnemyId = enemyId;
            RouteIndex = routeIndex;
        }
    }

    /// <summary>한 판 동안 변하지 않는 규칙 입력. 변하는 것은 World에 둔다.</summary>
    public sealed class BattleDefinition
    {
        private const int UnitsPerCellPerTicksPerSecond = 100;

        private readonly Dictionary<string, EnemyDefinition> _enemies = new Dictionary<string, EnemyDefinition>();

        public GridMap Map { get; }
        public int TicksPerSecond { get; }
        public int DefenseHp { get; }
        public IReadOnlyList<EnemyRoute> Routes { get; }

        /// <summary>시각 순서로 정렬된 등장 목록. 같은 시각이면 받은 순서를 지킨다.</summary>
        public IReadOnlyList<SpawnEvent> Spawns { get; }

        /// <summary>한 칸의 진행도. 실수 없이 계산하려고 초당 틱 x 100을 쓴다 (속도 1.00칸/초 = 틱당 100).</summary>
        public int UnitsPerCell => TicksPerSecond * UnitsPerCellPerTicksPerSecond;

        public BattleDefinition(
            GridMap map,
            int ticksPerSecond,
            int defenseHp,
            IEnumerable<EnemyDefinition> enemies,
            IReadOnlyList<EnemyRoute> routes,
            IEnumerable<SpawnEvent> spawns)
        {
            if (ticksPerSecond <= 0) throw new ArgumentOutOfRangeException(nameof(ticksPerSecond));
            if (defenseHp <= 0) throw new ArgumentOutOfRangeException(nameof(defenseHp));

            Map = map;
            TicksPerSecond = ticksPerSecond;
            DefenseHp = defenseHp;
            Routes = routes;

            foreach (var enemy in enemies)
            {
                if (_enemies.ContainsKey(enemy.Id)) throw new ArgumentException("Duplicate enemy id: " + enemy.Id);
                _enemies.Add(enemy.Id, enemy);
            }

            var sorted = spawns.OrderBy(s => s.Tick).ToArray();
            foreach (var spawn in sorted)
            {
                if (spawn.Tick < 0) throw new ArgumentException("Spawn tick is negative: " + spawn.Tick);
                if (!_enemies.ContainsKey(spawn.EnemyId)) throw new ArgumentException("Spawn uses unknown enemy: " + spawn.EnemyId);
                if (spawn.RouteIndex < 0 || spawn.RouteIndex >= routes.Count)
                    throw new ArgumentException("Spawn uses unknown route: " + spawn.RouteIndex);
            }
            Spawns = sorted;
        }

        public EnemyDefinition Enemy(string id) => _enemies[id];
    }
}
