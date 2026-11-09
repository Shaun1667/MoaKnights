using System;
using MoaKnights.Core.Map;

namespace MoaKnights.Core.Sim
{
    /// <summary>규칙 진행의 입구. 게임은 Step으로만 앞으로 간다. 시스템은 여기에 순서대로 더한다.</summary>
    public sealed class Simulation
    {
        public World World { get; }
        public BattleDefinition Definition { get; }
        public GridMap Map => Definition.Map;

        public event Action<Enemy> EnemySpawned;
        public event Action<Enemy> EnemyLeaked;

        /// <summary>디펜스 HP가 실제로 깎였을 때만 온다. 값은 깎인 양이다.</summary>
        public event Action<int> DefenseDamaged;

        public Simulation(World world, BattleDefinition definition)
        {
            World = world;
            Definition = definition;
        }

        public void Step()
        {
            MoveEnemies();
            SpawnEnemies();
            World.AdvanceTick();
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
                EnemySpawned?.Invoke(World.SpawnEnemy(spawn.EnemyId, spawn.RouteIndex));
            }
        }
    }
}
