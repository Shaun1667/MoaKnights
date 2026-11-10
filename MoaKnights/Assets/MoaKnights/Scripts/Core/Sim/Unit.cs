using System;
using MoaKnights.Core.Map;

namespace MoaKnights.Core.Sim
{
    /// <summary>능력자를 놓을 수 있는 칸. enum은 끝에만 추가한다.</summary>
    public enum UnitTerrain
    {
        Ground = 0,
        Hill = 1,
    }

    /// <summary>배치 방향. 격자 기준이며 Right가 공격 범위 정의의 기준 방향이다. enum은 끝에만 추가한다.</summary>
    public enum Direction
    {
        Right = 0,
        Up = 1,
        Left = 2,
        Down = 3,
    }

    /// <summary>배치를 시도한 결과. 글은 화면 계층이 표에서 만든다. enum은 끝에만 추가한다.</summary>
    public enum PlacementResult
    {
        Ok = 0,
        UnknownUnit = 1,
        OutOfBounds = 2,
        WrongTerrain = 3,
        Occupied = 4,
        AlreadyDeployed = 5,
        NotEnoughCost = 6,
    }

    /// <summary>능력자 한 종류의 규칙 값.</summary>
    public sealed class UnitDefinition
    {
        public string Id { get; }
        public int Cost { get; }
        public UnitTerrain Terrain { get; }
        public CombatStats Stats { get; }

        /// <summary>동시에 막을 수 있는 적 수. 0이면 막지 않는다.</summary>
        public int BlockCount { get; }

        public AttackRange Range { get; }

        public UnitDefinition(string id, int cost, UnitTerrain terrain, CombatStats stats, int blockCount, AttackRange range)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Unit id is empty.");
            if (cost < 0) throw new ArgumentOutOfRangeException(nameof(cost), "Unit " + id);
            if (blockCount < 0) throw new ArgumentOutOfRangeException(nameof(blockCount), "Unit " + id);
            Id = id;
            Cost = cost;
            Terrain = terrain;
            Stats = stats ?? throw new ArgumentNullException(nameof(stats), "Unit " + id);
            BlockCount = blockCount;
            Range = range ?? throw new ArgumentNullException(nameof(range), "Unit " + id);
        }

        public bool CanStandOn(CellType cell)
        {
            switch (Terrain)
            {
                case UnitTerrain.Ground: return cell == CellType.Ground;
                case UnitTerrain.Hill: return cell == CellType.Hill;
                default: return false;
            }
        }
    }

    /// <summary>판 위에 배치된 능력자 하나. 값은 World가 들고 Simulation만 바꾼다.</summary>
    public sealed class Unit
    {
        public int Id { get; }
        public string DefinitionId { get; }
        public GridPos Cell { get; }
        public Direction Direction { get; }
        public int Hp { get; internal set; }

        /// <summary>다음 공격까지 남은 틱. 0이면 대상이 생기는 즉시 친다.</summary>
        public int AttackCooldown { get; internal set; }

        public Unit(int id, string definitionId, GridPos cell, Direction direction, int hp, int attackCooldown)
        {
            Id = id;
            DefinitionId = definitionId;
            Cell = cell;
            Direction = direction;
            Hp = hp;
            AttackCooldown = attackCooldown;
        }
    }
}
