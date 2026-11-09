namespace MoaKnights.Core
{
    /// <summary>한 판의 시작 조건. 같은 시드 = 같은 결과의 출발점.</summary>
    public sealed class RunContext
    {
        public int Seed { get; }

        public RunContext(int seed)
        {
            Seed = seed;
        }
    }
}
