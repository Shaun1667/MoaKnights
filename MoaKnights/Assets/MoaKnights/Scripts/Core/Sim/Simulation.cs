using MoaKnights.Core.Map;

namespace MoaKnights.Core.Sim
{
    /// <summary>규칙 진행의 입구. 게임은 Step으로만 앞으로 간다. 시스템은 여기에 순서대로 더한다.</summary>
    public sealed class Simulation
    {
        public World World { get; }
        public GridMap Map { get; }

        public Simulation(World world, GridMap map)
        {
            World = world;
            Map = map;
        }

        public void Step()
        {
            World.AdvanceTick();
        }
    }
}
