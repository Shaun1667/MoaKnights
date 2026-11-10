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
        public CombatStats Stats { get; }

        public EnemyDefinition(string id, int speedPerTick, int leakDamage, CombatStats stats)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Enemy id is empty.");
            if (speedPerTick <= 0) throw new ArgumentOutOfRangeException(nameof(speedPerTick), "Enemy " + id);
            if (leakDamage < 0) throw new ArgumentOutOfRangeException(nameof(leakDamage), "Enemy " + id);
            Id = id;
            SpeedPerTick = speedPerTick;
            LeakDamage = leakDamage;
            Stats = stats ?? throw new ArgumentNullException(nameof(stats), "Enemy " + id);
        }
    }

    /// <summary>싸우는 쪽의 공통 수치. 피해 = 공격력 (방어는 아직 없음). 공격 간격은 틱.</summary>
    public sealed class CombatStats
    {
        public int MaxHp { get; }
        public int Attack { get; }
        public int AttackIntervalTicks { get; }

        public CombatStats(int maxHp, int attack, int attackIntervalTicks)
        {
            if (maxHp <= 0) throw new ArgumentOutOfRangeException(nameof(maxHp));
            if (attack < 0) throw new ArgumentOutOfRangeException(nameof(attack));
            if (attackIntervalTicks <= 0) throw new ArgumentOutOfRangeException(nameof(attackIntervalTicks));
            MaxHp = maxHp;
            Attack = attack;
            AttackIntervalTicks = attackIntervalTicks;
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

    /// <summary>코스트 규칙. 회복은 틱 단위 정수다.</summary>
    public sealed class CostRules
    {
        public int Start { get; }
        public int Max { get; }

        /// <summary>코스트 1이 차는 데 걸리는 틱.</summary>
        public int RegenTicks { get; }

        public CostRules(int start, int max, int regenTicks)
        {
            if (max <= 0) throw new ArgumentOutOfRangeException(nameof(max));
            if (start < 0 || start > max) throw new ArgumentOutOfRangeException(nameof(start));
            if (regenTicks <= 0) throw new ArgumentOutOfRangeException(nameof(regenTicks));
            Start = start;
            Max = max;
            RegenTicks = regenTicks;
        }
    }

    /// <summary>한 판 동안 변하지 않는 규칙 입력. 변하는 것은 World에 둔다.</summary>
    public sealed class BattleDefinition
    {
        private const int UnitsPerCellPerTicksPerSecond = 100;

        private readonly Dictionary<string, EnemyDefinition> _enemies = new Dictionary<string, EnemyDefinition>();
        private readonly Dictionary<string, UnitDefinition> _units = new Dictionary<string, UnitDefinition>();
        private readonly List<UnitDefinition> _unitOrder = new List<UnitDefinition>();

        public GridMap Map { get; }
        public int TicksPerSecond { get; }
        public int DefenseHp { get; }
        public CostRules Cost { get; }
        public DeployRules Deploy { get; }
        public IReadOnlyList<EnemyRoute> Routes { get; }

        /// <summary>이 판에 나오는 능력자. 카드 순서와 같다.</summary>
        public IReadOnlyList<UnitDefinition> Units => _unitOrder;

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
            IEnumerable<SpawnEvent> spawns,
            CostRules cost,
            IEnumerable<UnitDefinition> units,
            DeployRules deploy)
        {
            if (ticksPerSecond <= 0) throw new ArgumentOutOfRangeException(nameof(ticksPerSecond));
            if (defenseHp <= 0) throw new ArgumentOutOfRangeException(nameof(defenseHp));

            Map = map;
            TicksPerSecond = ticksPerSecond;
            DefenseHp = defenseHp;
            Cost = cost ?? throw new ArgumentNullException(nameof(cost));
            Deploy = deploy ?? throw new ArgumentNullException(nameof(deploy));
            Routes = routes;

            foreach (var unit in units)
            {
                if (_units.ContainsKey(unit.Id)) throw new ArgumentException("Duplicate unit id: " + unit.Id);
                if (unit.Skills.Skills.Count != deploy.MaxStage)
                    throw new ArgumentException("Unit " + unit.Id + " needs one skill per stage (" + deploy.MaxStage + ").");
                _units.Add(unit.Id, unit);
                _unitOrder.Add(unit);
            }

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

        public bool TryGetUnit(string id, out UnitDefinition unit) => _units.TryGetValue(id ?? string.Empty, out unit);
    }
}
