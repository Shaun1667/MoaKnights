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

        public Enemy(int id, string definitionId, int routeIndex, int progress)
        {
            Id = id;
            DefinitionId = definitionId;
            RouteIndex = routeIndex;
            Progress = progress;
        }
    }
}
