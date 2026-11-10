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

        /// <summary>적이 맞았을 때. 값은 피해량. HP가 0이 되면 이 다음에 EnemyKilled가 온다.</summary>
        public event Action<Enemy, int> EnemyDamaged;
        public event Action<Enemy> EnemyKilled;

        /// <summary>능력자가 맞았을 때. 값은 피해량. HP가 0이 되면 이 다음에 UnitDefeated가 온다.</summary>
        public event Action<Unit, int> UnitDamaged;
        public event Action<Unit> UnitDefeated;

        public event Action<Enemy, Unit> EnemyBlocked;

        public Simulation(World world, BattleDefinition definition)
        {
            World = world;
            Definition = definition;
        }

        /// <summary>한 틱: 코스트 회복 → 능력자 공격 → 막힌 적의 반격 → 적 이동(저지 · 누수) → 등장.</summary>
        public void Step()
        {
            World.RegenCost(Definition.Cost);
            UnitsAttack();
            EnemiesAttack();
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
            var placed = World.PlaceUnit(unitId, cell, direction, unit.Stats.MaxHp);
            UnitPlaced?.Invoke(placed);
            return result;
        }

        /// <summary>적이 지금 서 있는 칸. 진행도를 반올림한 칸이라 칸 경계를 넘으면 다음 칸이 된다.</summary>
        public GridPos EnemyCell(Enemy enemy)
        {
            var route = Definition.Routes[enemy.RouteIndex];
            var units = Definition.UnitsPerCell;
            var index = Math.Min((enemy.Progress + units / 2) / units, route.Length);
            return route.Cells[index];
        }

        private void UnitsAttack()
        {
            // 공격 중에는 능력자가 빠지지 않으므로 목록을 그대로 돈다. 적은 죽으면 바로 빠진다.
            foreach (var unit in World.Units)
            {
                Definition.TryGetUnit(unit.DefinitionId, out var definition);
                if (unit.AttackCooldown > 0) unit.AttackCooldown--;
                if (unit.AttackCooldown > 0) continue;

                var target = FindTarget(unit, definition);
                if (target == null) continue;

                unit.AttackCooldown = definition.Stats.AttackIntervalTicks;
                DamageEnemy(target, definition.Stats.Attack);
            }
        }

        /// <summary>범위 안에서 출구에 가장 가까운 적. 같으면 먼저 나온 적.</summary>
        private Enemy FindTarget(Unit unit, UnitDefinition definition)
        {
            Enemy best = null;
            foreach (var enemy in World.Enemies)
            {
                if (!definition.Range.Contains(unit.Cell, unit.Direction, EnemyCell(enemy))) continue;
                if (best == null || enemy.Progress > best.Progress) best = enemy;
            }
            return best;
        }

        private void DamageEnemy(Enemy enemy, int damage)
        {
            enemy.Hp -= damage;
            EnemyDamaged?.Invoke(enemy, damage);
            if (enemy.Hp > 0) return;

            World.RemoveEnemy(enemy);
            EnemyKilled?.Invoke(enemy);
        }

        private void EnemiesAttack()
        {
            var enemies = World.Enemies;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (!enemy.IsBlocked) continue;

                var stats = Definition.Enemy(enemy.DefinitionId).Stats;
                if (enemy.AttackCooldown > 0) enemy.AttackCooldown--;
                if (enemy.AttackCooldown > 0) continue;

                var unit = World.UnitById(enemy.BlockedBy);
                enemy.AttackCooldown = stats.AttackIntervalTicks;
                DamageUnit(unit, stats.Attack);
            }
        }

        private void DamageUnit(Unit unit, int damage)
        {
            unit.Hp -= damage;
            UnitDamaged?.Invoke(unit, damage);
            if (unit.Hp > 0) return;

            // 막혀 있던 적은 RemoveUnit에서 풀리고, 다음 이동 단계부터 다시 걷는다.
            World.RemoveUnit(unit);
            UnitDefeated?.Invoke(unit);
        }

        private void MoveEnemies()
        {
            var enemies = World.Enemies;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy.IsBlocked) continue;

                var definition = Definition.Enemy(enemy.DefinitionId);
                enemy.Progress += definition.SpeedPerTick;

                if (enemy.Progress < Definition.Routes[enemy.RouteIndex].Length * Definition.UnitsPerCell)
                {
                    TryBlock(enemy);
                    continue;
                }

                World.RemoveEnemyAt(i);
                i--;
                EnemyLeaked?.Invoke(enemy);

                var damage = World.DamageDefense(definition.LeakDamage);
                if (damage > 0) DefenseDamaged?.Invoke(damage);
            }
        }

        /// <summary>적이 들어선 칸의 능력자가 자리가 남으면 막는다. 자리가 없으면 그냥 지나간다.</summary>
        private void TryBlock(Enemy enemy)
        {
            var unit = World.UnitAt(EnemyCell(enemy));
            if (unit == null) return;

            Definition.TryGetUnit(unit.DefinitionId, out var definition);
            if (World.BlockedCount(unit.Id) >= definition.BlockCount) return;

            enemy.BlockedBy = unit.Id;
            EnemyBlocked?.Invoke(enemy, unit);
        }

        private void SpawnEnemies()
        {
            var spawns = Definition.Spawns;
            while (World.NextSpawnIndex < spawns.Count && spawns[World.NextSpawnIndex].Tick <= World.Tick)
            {
                var spawn = spawns[World.NextSpawnIndex];
                var hp = Definition.Enemy(spawn.EnemyId).Stats.MaxHp;

                // ?.Invoke의 인자는 구독자가 없으면 계산되지 않으므로 상태 변경을 인자에 넣지 않는다.
                var enemy = World.SpawnEnemy(spawn.EnemyId, spawn.RouteIndex, hp);
                EnemySpawned?.Invoke(enemy);
            }
        }
    }
}
