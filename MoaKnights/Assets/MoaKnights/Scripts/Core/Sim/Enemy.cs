namespace MoaKnights.Core.Sim
{
    /// <summary>판 위의 적 한 마리. 값은 World가 들고 Simulation만 바꾼다.</summary>
    public sealed class Enemy
    {
        public int Id { get; }
        public string DefinitionId { get; }
        public int RouteIndex { get; }

        /// <summary>길을 따라 나아간 정도. 단위는 BattleDefinition.UnitsPerCell.</summary>
        public int Progress { get; internal set; }

        public int Hp { get; internal set; }

        /// <summary>다음 공격까지 남은 틱. 0이면 막히는 즉시 친다.</summary>
        public int AttackCooldown { get; internal set; }

        /// <summary>이 적을 막고 있는 능력자 ID. 0이면 막히지 않았다.</summary>
        public int BlockedBy { get; internal set; }

        public bool IsBlocked => BlockedBy != 0;

        public Enemy(int id, string definitionId, int routeIndex, int progress, int hp, int attackCooldown, int blockedBy)
        {
            Id = id;
            DefinitionId = definitionId;
            RouteIndex = routeIndex;
            Progress = progress;
            Hp = hp;
            AttackCooldown = attackCooldown;
            BlockedBy = blockedBy;
        }
    }
}
