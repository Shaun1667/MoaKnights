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

    /// <summary>포지션 (기획 4-10). 힐러는 적을 치지 않고 범위 안 아군을 치유한다. enum은 끝에만 추가한다.</summary>
    public enum UnitRole
    {
        Tank = 0,
        Dealer = 1,
        Healer = 2,
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
        OnCooldown = 7,
        MaxStackReached = 8,
        NotDeployed = 9,
        SpNotFull = 10,
        SkillActive = 11,
        BattleOver = 12,
    }

    /// <summary>판의 결과. enum은 끝에만 추가한다.</summary>
    public enum BattleOutcome
    {
        None = 0,
        Victory = 1,
        Defeat = 2,
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

        public SkillSet Skills { get; }
        public UnitRole Role { get; }

        public UnitDefinition(string id, int cost, UnitTerrain terrain, CombatStats stats, int blockCount, AttackRange range, SkillSet skills, UnitRole role)
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
            Skills = skills ?? throw new ArgumentNullException(nameof(skills), "Unit " + id);
            Role = role;
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

        /// <summary>중첩 단계. 1부터 DeployRules.MaxStage까지.</summary>
        public int Stage { get; internal set; }

        /// <summary>배치 · 중첩에 들인 코스트 합. 퇴각 환급의 기준이다.</summary>
        public int SpentCost { get; internal set; }

        public int Sp { get; internal set; }

        /// <summary>자연 회복 SP의 다음 1까지 쌓인 틱.</summary>
        public int SpRegenTicks { get; internal set; }

        /// <summary>쓰고 있는 스킬의 남은 틱. 0이면 스킬을 쓰고 있지 않다.</summary>
        public int SkillTicksLeft { get; internal set; }

        /// <summary>쓰고 있는 스킬의 단계. 쓰는 중에 겹쳐도 처음 쓴 단계의 효과가 이어진다. 0이면 없음.</summary>
        public int ActiveSkillStage { get; internal set; }

        public bool IsSkillActive => SkillTicksLeft > 0;

        public Unit(int id, string definitionId, GridPos cell, Direction direction, int hp, int attackCooldown, int stage, int spentCost)
        {
            Id = id;
            DefinitionId = definitionId;
            Cell = cell;
            Direction = direction;
            Hp = hp;
            AttackCooldown = attackCooldown;
            Stage = stage;
            SpentCost = spentCost;
        }
    }

    /// <summary>능력자 카드 하나의 판 기록. 몇 번 놓았는지와 언제 다시 놓을 수 있는지.</summary>
    public sealed class UnitSlot
    {
        public string DefinitionId { get; }

        /// <summary>지금까지 놓은 횟수. 재배치 코스트 인상에 쓴다.</summary>
        public int Deployments { get; internal set; }

        /// <summary>이 틱부터 다시 놓을 수 있다.</summary>
        public long ReadyTick { get; internal set; }

        public UnitSlot(string definitionId, int deployments, long readyTick)
        {
            DefinitionId = definitionId;
            Deployments = deployments;
            ReadyTick = readyTick;
        }
    }
}
