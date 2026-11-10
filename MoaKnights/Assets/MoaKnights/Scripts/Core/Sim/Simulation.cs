using System;
using MoaKnights.Core.Map;

namespace MoaKnights.Core.Sim
{
    /// <summary>
    /// 규칙 진행의 입구. 시간은 Step으로만 앞으로 가고, 시스템은 Step에 순서대로 더한다.
    /// 플레이어 명령(배치 등)은 시간을 진행하지 않으므로 Try로 시작하는 메서드로 바로 처리한다 — 일시정지 중에도 받는다.
    /// </summary>
    public sealed class Simulation
    {
        public World World { get; }
        public BattleDefinition Definition { get; }
        public GridMap Map => Definition.Map;

        public event Action<Enemy> EnemySpawned;
        public event Action<Enemy> EnemyLeaked;

        /// <summary>디펜스 HP가 실제로 깎였을 때만 온다. 값은 깎인 양이다.</summary>
        public event Action<int> DefenseDamaged;

        public event Action<Unit> UnitPlaced;

        public Simulation(World world, BattleDefinition definition)
        {
            World = world;
            Definition = definition;
        }

        public void Step()
        {
            World.RegenCost(Definition.Cost);
            MoveEnemies();
            SpawnEnemies();
            World.AdvanceTick();
        }

        /// <summary>상태를 바꾸지 않고 배치 가능 여부만 본다. 미리보기와 TryPlaceUnit이 같은 판정을 쓴다.</summary>
        public PlacementResult CheckPlacement(string unitId, GridPos cell)
        {
            if (!Definition.TryGetUnit(unitId, out var unit)) return PlacementResult.UnknownUnit;
            if (!Map.InBounds(cell)) return PlacementResult.OutOfBounds;
            if (!unit.CanStandOn(Map[cell])) return PlacementResult.WrongTerrain;
            if (World.UnitAt(cell) != null) return PlacementResult.Occupied;
            if (World.IsDeployed(unitId)) return PlacementResult.AlreadyDeployed;
            if (World.Cost < unit.Cost) return PlacementResult.NotEnoughCost;
            return PlacementResult.Ok;
        }

        public PlacementResult TryPlaceUnit(string unitId, GridPos cell, Direction direction)
        {
            var result = CheckPlacement(unitId, cell);
            if (result != PlacementResult.Ok) return result;

            Definition.TryGetUnit(unitId, out var unit);
            World.ChangeCost(-unit.Cost, Definition.Cost);
            var placed = World.PlaceUnit(unitId, cell, direction);
            UnitPlaced?.Invoke(placed);
            return result;
        }

        private void MoveEnemies()
        {
            var enemies = World.Enemies;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                var definition = Definition.Enemy(enemy.DefinitionId);
                enemy.Progress += definition.SpeedPerTick;

                if (enemy.Progress < Definition.Routes[enemy.RouteIndex].Length * Definition.UnitsPerCell) continue;

                World.RemoveEnemyAt(i);
                i--;
                EnemyLeaked?.Invoke(enemy);

                var damage = World.DamageDefense(definition.LeakDamage);
                if (damage > 0) DefenseDamaged?.Invoke(damage);
            }
        }

        private void SpawnEnemies()
        {
            var spawns = Definition.Spawns;
            while (World.NextSpawnIndex < spawns.Count && spawns[World.NextSpawnIndex].Tick <= World.Tick)
            {
                var spawn = spawns[World.NextSpawnIndex];

                // ?.Invoke의 인자는 구독자가 없으면 계산되지 않으므로 상태 변경을 인자에 넣지 않는다.
                var enemy = World.SpawnEnemy(spawn.EnemyId, spawn.RouteIndex);
                EnemySpawned?.Invoke(enemy);
            }
        }
    }
}
