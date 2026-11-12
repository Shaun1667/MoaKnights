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

        public event Action<Unit> UnitStacked;

        /// <summary>직접 퇴각. 값은 돌려받은 코스트.</summary>
        public event Action<Unit, int> UnitRetreated;

        public event Action<Unit> SkillUsed;
        public event Action<Unit> SkillEnded;

        /// <summary>스킬로 회복했을 때. 값은 실제로 오른 HP.</summary>
        public event Action<Unit, int> UnitHealed;

        /// <summary>치유 특성의 적이 다른 적을 회복했을 때. 값은 실제로 오른 HP.</summary>
        public event Action<Enemy, int> EnemyHealed;

        /// <summary>분열로 새 적이 생겼을 때 (EnemySpawned와 따로 온다 — 등장 목록과 상관없음).</summary>
        public event Action<Enemy> EnemySplit;

        /// <summary>판이 끝났을 때 한 번 온다.</summary>
        public event Action<BattleOutcome> BattleEnded;

        public bool IsOver => World.Outcome != BattleOutcome.None;

        public Simulation(World world, BattleDefinition definition)
        {
            World = world;
            Definition = definition;
        }

        /// <summary>한 틱: 코스트 회복 → 스킬 시간 · 자연 SP → 능력자 공격(분열) → 적 행동(막힌 적의 반격 · 원거리 · 치유) → 적 이동(저지 · 누수) → 등장 → 승패.</summary>
        public void Step()
        {
            if (IsOver) return;

            World.RegenCost(Definition.Cost);
            UpdateSkills();
            UnitsAttack();
            EnemiesAttack();
            MoveEnemies();
            SpawnEnemies();
            CheckOutcome();
            World.AdvanceTick();
        }

        /// <summary>
        /// 패배: 디펜스 HP 0. 승리: 모든 적이 나왔고 판에 적이 없으며 HP가 남음 (결정 2026-11-10, 사용자).
        /// 등장 목록이 비어 있으면 승리로 치지 않는다 (시험용 판).
        /// </summary>
        private void CheckOutcome()
        {
            var outcome = BattleOutcome.None;
            if (World.DefenseHp <= 0) outcome = BattleOutcome.Defeat;
            else if (Definition.Spawns.Count > 0 && World.NextSpawnIndex >= Definition.Spawns.Count && World.Enemies.Count == 0)
                outcome = BattleOutcome.Victory;
            if (outcome == BattleOutcome.None) return;

            World.End(outcome);
            BattleEnded?.Invoke(outcome);
        }

        /// <summary>
        /// 개발자 디버그(바로 클리어): 판 위 적을 모두 쓰러뜨리고(EnemyKilled) 남은 등장을 건너뛴 뒤 승패를 판정한다.
        /// 디펜스 HP는 그대로라 별은 지금 HP로 정해진다. 시간은 진행하지 않는다. 끝난 판에서는 아무것도 하지 않는다.
        /// </summary>
        public void DebugClearAll()
        {
            if (IsOver) return;
            while (World.Enemies.Count > 0)
            {
                var enemy = World.Enemies[0];
                World.RemoveEnemy(enemy);
                EnemyKilled?.Invoke(enemy);
            }
            World.SkipRemainingSpawns(Definition.Spawns.Count);
            CheckOutcome();
        }

        /// <summary>별 0~3. 판이 끝나기 전이나 패배면 0. 새지 않으면 3, HP가 기준 이상이면 2, 아니면 1.</summary>
        public int Stars()
        {
            if (World.Outcome != BattleOutcome.Victory) return 0;
            if (World.DefenseHp >= Definition.DefenseHp) return 3;
            return World.DefenseHp * 100 >= Definition.DefenseHp * Definition.TwoStarHpPercent ? 2 : 1;
        }

        /// <summary>상태를 바꾸지 않고 배치 가능 여부만 본다. 미리보기와 TryPlaceUnit이 같은 판정을 쓴다.</summary>
        public PlacementResult CheckPlacement(string unitId, GridPos cell)
        {
            if (IsOver) return PlacementResult.BattleOver;
            if (!Definition.TryGetUnit(unitId, out var unit)) return PlacementResult.UnknownUnit;
            if (!Map.InBounds(cell)) return PlacementResult.OutOfBounds;
            if (!unit.CanStandOn(Map[cell])) return PlacementResult.WrongTerrain;
            if (World.UnitAt(cell) != null) return PlacementResult.Occupied;
            if (World.IsDeployed(unitId)) return PlacementResult.AlreadyDeployed;
            if (RedeployTicksLeft(unitId) > 0) return PlacementResult.OnCooldown;
            if (World.Cost < DeployCost(unitId)) return PlacementResult.NotEnoughCost;
            return PlacementResult.Ok;
        }

        public PlacementResult TryPlaceUnit(string unitId, GridPos cell, Direction direction)
        {
            var result = CheckPlacement(unitId, cell);
            if (result != PlacementResult.Ok) return result;

            Definition.TryGetUnit(unitId, out var unit);
            var cost = DeployCost(unitId);
            World.ChangeCost(-cost, Definition.Cost);
            var placed = World.PlaceUnit(unitId, cell, direction, Definition.Deploy.MaxHp(unit, 1), cost);
            UnitPlaced?.Invoke(placed);
            return result;
        }

        /// <summary>판 위의 같은 능력자에 겹칠 수 있는지만 본다. 방향 · 칸은 바뀌지 않는다.</summary>
        public PlacementResult CheckStack(string unitId)
        {
            if (IsOver) return PlacementResult.BattleOver;
            if (!Definition.TryGetUnit(unitId, out var unit)) return PlacementResult.UnknownUnit;
            var placed = World.UnitOf(unitId);
            if (placed == null) return PlacementResult.NotDeployed;
            if (placed.Stage >= Definition.Deploy.MaxStage) return PlacementResult.MaxStackReached;
            if (World.Cost < Definition.Deploy.StackCost(unit)) return PlacementResult.NotEnoughCost;
            return PlacementResult.Ok;
        }

        /// <summary>한 단계 겹친다. 오른 최대 HP만큼 지금 HP도 오른다.</summary>
        public PlacementResult TryStackUnit(string unitId)
        {
            var result = CheckStack(unitId);
            if (result != PlacementResult.Ok) return result;

            Definition.TryGetUnit(unitId, out var unit);
            var placed = World.UnitOf(unitId);
            var cost = Definition.Deploy.StackCost(unit);
            var before = Definition.Deploy.MaxHp(unit, placed.Stage);

            World.ChangeCost(-cost, Definition.Cost);
            placed.Stage++;
            placed.SpentCost += cost;
            placed.Hp += Definition.Deploy.MaxHp(unit, placed.Stage) - before;
            UnitStacked?.Invoke(placed);
            return result;
        }

        /// <summary>직접 퇴각. 들인 코스트의 일부를 돌려받고, 재배치 쿨타임이 시작된다.</summary>
        public PlacementResult TryRetreat(int unitEntityId)
        {
            if (IsOver) return PlacementResult.BattleOver;
            var unit = World.UnitById(unitEntityId);
            if (unit == null) return PlacementResult.NotDeployed;

            var refund = Definition.Deploy.Refund(unit.SpentCost);
            World.ChangeCost(refund, Definition.Cost);
            World.RemoveUnit(unit, World.Tick + Definition.Deploy.RedeployCooldownTicks);
            UnitRetreated?.Invoke(unit, refund);
            return PlacementResult.Ok;
        }

        /// <summary>이 능력자를 지금 놓는 코스트. 다시 놓을 때마다 오른다.</summary>
        public int DeployCost(string unitId)
        {
            if (!Definition.TryGetUnit(unitId, out var unit)) return 0;
            var slot = World.FindSlot(unitId);
            return Definition.Deploy.DeployCost(unit, slot?.Deployments ?? 0);
        }

        /// <summary>재배치까지 남은 틱. 0이면 바로 놓을 수 있다.</summary>
        public long RedeployTicksLeft(string unitId)
        {
            var slot = World.FindSlot(unitId);
            return slot == null ? 0 : Math.Max(0, slot.ReadyTick - World.Tick);
        }

        public int MaxHp(Unit unit)
        {
            Definition.TryGetUnit(unit.DefinitionId, out var definition);
            return Definition.Deploy.MaxHp(definition, unit.Stage);
        }

        /// <summary>지금 공격력. 단계 배율에 쓰고 있는 스킬의 추가 비율을 곱한다 (내림).</summary>
        public int AttackOf(Unit unit)
        {
            Definition.TryGetUnit(unit.DefinitionId, out var definition);
            var attack = Definition.Deploy.Attack(definition, unit.Stage);
            var bonus = unit.IsSkillActive ? definition.Skills.ForStage(unit.ActiveSkillStage).AttackBonusPercent : 0;
            return attack * (100 + bonus) / 100;
        }

        /// <summary>지금 저지 수. 단계 보너스에 쓰고 있는 스킬의 보너스를 더한다.</summary>
        public int BlockOf(Unit unit)
        {
            Definition.TryGetUnit(unit.DefinitionId, out var definition);
            var bonus = unit.IsSkillActive ? definition.Skills.ForStage(unit.ActiveSkillStage).BlockBonus : 0;
            return Definition.Deploy.BlockCount(definition, unit.Stage) + bonus;
        }

        public SkillSet SkillsOf(Unit unit)
        {
            Definition.TryGetUnit(unit.DefinitionId, out var definition);
            return definition.Skills;
        }

        /// <summary>스킬을 쓸 수 있는지만 본다.</summary>
        public PlacementResult CheckSkill(int unitEntityId)
        {
            if (IsOver) return PlacementResult.BattleOver;
            var unit = World.UnitById(unitEntityId);
            if (unit == null) return PlacementResult.NotDeployed;
            if (unit.IsSkillActive) return PlacementResult.SkillActive;
            if (unit.Sp < SkillsOf(unit).MaxSp) return PlacementResult.SpNotFull;
            return PlacementResult.Ok;
        }

        /// <summary>지금 단계의 스킬을 쓴다. SP는 0이 되고, 회복은 즉시, 나머지는 지속 시간 동안 걸린다.</summary>
        public PlacementResult TryUseSkill(int unitEntityId)
        {
            var result = CheckSkill(unitEntityId);
            if (result != PlacementResult.Ok) return result;

            var unit = World.UnitById(unitEntityId);
            var skill = SkillsOf(unit).ForStage(unit.Stage);
            unit.Sp = 0;
            unit.SpRegenTicks = 0;
            unit.SkillTicksLeft = skill.DurationTicks;
            unit.ActiveSkillStage = skill.DurationTicks > 0 ? unit.Stage : 0;

            var maxHp = MaxHp(unit);
            var healed = Math.Min(maxHp * skill.HealPercent / 100, maxHp - unit.Hp);
            unit.Hp += healed;

            SkillUsed?.Invoke(unit);
            if (healed > 0) UnitHealed?.Invoke(unit, healed);
            return result;
        }

        /// <summary>스킬 지속 시간을 줄이고 끝나면 효과를 거둔다. 스킬을 쓰지 않을 때만 자연 SP가 찬다.</summary>
        private void UpdateSkills()
        {
            foreach (var unit in World.Units)
            {
                if (unit.IsSkillActive)
                {
                    unit.SkillTicksLeft--;
                    if (unit.IsSkillActive) continue;

                    unit.ActiveSkillStage = 0;
                    ReleaseExcessBlocked(unit);
                    SkillEnded?.Invoke(unit);
                    continue;
                }

                var skills = SkillsOf(unit);
                if (skills.SpType != SpType.Natural || unit.Sp >= skills.MaxSp) continue;
                unit.SpRegenTicks++;
                if (unit.SpRegenTicks < skills.NaturalRegenTicks) continue;
                unit.SpRegenTicks = 0;
                unit.Sp++;
            }
        }

        /// <summary>타격 · 피격 SP. 스킬을 쓰는 동안과 가득 찼을 때는 오르지 않는다.</summary>
        private void GainSp(Unit unit, SpType trigger)
        {
            var skills = SkillsOf(unit);
            if (skills.SpType != trigger || unit.IsSkillActive || unit.Sp >= skills.MaxSp) return;
            unit.Sp++;
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

                if (definition.Role == UnitRole.Healer)
                {
                    HealAlly(unit, definition);
                    continue;
                }

                var target = FindTarget(unit, definition);
                if (target == null) continue;

                unit.AttackCooldown = definition.Stats.AttackIntervalTicks;
                DamageEnemy(target, AttackOf(unit));
                GainSp(unit, SpType.OnAttack);
            }
        }

        /// <summary>
        /// 힐러의 "공격": 범위 안에서 HP 비율이 가장 낮은 다친 아군을 공격력만큼 치유한다 (최대 HP까지).
        /// 다친 아군이 없으면 치유하지 않고 대기도 쓰지 않는다. 치유도 공격으로 쳐 타격 SP가 찬다.
        /// </summary>
        private void HealAlly(Unit healer, UnitDefinition definition)
        {
            Unit target = null;
            var targetMaxHp = 1;
            foreach (var ally in World.Units)
            {
                if (!definition.Range.Contains(healer.Cell, healer.Direction, ally.Cell)) continue;
                var maxHp = MaxHp(ally);
                if (ally.Hp >= maxHp) continue;

                // 비율 비교는 정수 곱으로 한다: ally.Hp / maxHp < target.Hp / targetMaxHp
                if (target == null || (long)ally.Hp * targetMaxHp < (long)target.Hp * maxHp)
                {
                    target = ally;
                    targetMaxHp = maxHp;
                }
            }
            if (target == null) return;

            healer.AttackCooldown = definition.Stats.AttackIntervalTicks;
            var healed = Math.Min(AttackOf(healer), targetMaxHp - target.Hp);
            target.Hp += healed;
            UnitHealed?.Invoke(target, healed);
            GainSp(healer, SpType.OnAttack);
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

        /// <summary>능력자가 적을 친다. 방어 특성만큼 덜 받는다 (최소 1). 쓰러지면 분열 특성대로 나뉜다.</summary>
        private void DamageEnemy(Enemy enemy, int damage)
        {
            var traits = Definition.Enemy(enemy.DefinitionId).Traits;
            damage = Math.Max(1, damage - traits.Armor);
            enemy.Hp -= damage;
            EnemyDamaged?.Invoke(enemy, damage);
            if (enemy.Hp > 0) return;

            World.RemoveEnemy(enemy);
            EnemyKilled?.Invoke(enemy);
            if (!traits.Splits) return;

            var child = Definition.Enemy(traits.SplitInto);
            for (var i = 0; i < traits.SplitCount; i++)
            {
                var split = World.SpawnSplit(child.Id, enemy.RouteIndex, enemy.Progress, child.Stats.MaxHp, child.Traits.HealIntervalTicks);
                EnemySplit?.Invoke(split);
            }
        }

        /// <summary>적 행동: 치유 특성은 간격마다 둘레의 다른 적을 회복하고, 막힌 적은 막은 능력자를, 막히지 않은 원거리 적은 가까운 능력자를 친다.</summary>
        private void EnemiesAttack()
        {
            var enemies = World.Enemies;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                var definition = Definition.Enemy(enemy.DefinitionId);
                if (definition.Traits.CanHeal) HealNearbyEnemies(enemy, definition.Traits);

                var stats = definition.Stats;
                if (stats.Attack <= 0) continue;
                if (enemy.AttackCooldown > 0) enemy.AttackCooldown--;
                if (enemy.AttackCooldown > 0) continue;

                var unit = enemy.IsBlocked ? World.UnitById(enemy.BlockedBy) : RangedTarget(enemy, definition.Traits);
                if (unit == null) continue;
                enemy.AttackCooldown = stats.AttackIntervalTicks;
                DamageUnit(unit, stats.Attack);
            }
        }

        /// <summary>원거리 특성의 과녁: 가로 + 세로 거리가 사거리 안인 능력자 중 가장 가까운 하나 (같으면 먼저 놓은 쪽).</summary>
        private Unit RangedTarget(Enemy enemy, EnemyTraits traits)
        {
            if (traits.RangedReach <= 0) return null;
            var cell = EnemyCell(enemy);
            Unit best = null;
            var bestDistance = int.MaxValue;
            foreach (var unit in World.Units)
            {
                var distance = Math.Abs(unit.Cell.X - cell.X) + Math.Abs(unit.Cell.Y - cell.Y);
                if (distance > traits.RangedReach || distance >= bestDistance) continue;
                best = unit;
                bestDistance = distance;
            }
            return best;
        }

        /// <summary>치유 특성: 간격마다 둘레 1칸(대각 포함, 같은 칸 포함)의 다친 다른 적을 회복한다. 최대 HP를 넘지 않는다.</summary>
        private void HealNearbyEnemies(Enemy healer, EnemyTraits traits)
        {
            if (healer.HealCooldown > 0) healer.HealCooldown--;
            if (healer.HealCooldown > 0) return;
            healer.HealCooldown = traits.HealIntervalTicks;

            var cell = EnemyCell(healer);
            foreach (var other in World.Enemies)
            {
                if (other == healer) continue;
                var otherCell = EnemyCell(other);
                if (Math.Abs(otherCell.X - cell.X) > 1 || Math.Abs(otherCell.Y - cell.Y) > 1) continue;
                var maxHp = Definition.Enemy(other.DefinitionId).Stats.MaxHp;
                var healed = Math.Min(traits.HealAmount, maxHp - other.Hp);
                if (healed <= 0) continue;
                other.Hp += healed;
                EnemyHealed?.Invoke(other, healed);
            }
        }

        /// <summary>이 능력자가 막고 있는 적의 무게 합 (무거움 특성은 무게가 크다).</summary>
        public int BlockedWeight(Unit unit)
        {
            var weight = 0;
            foreach (var enemy in World.Enemies)
                if (enemy.BlockedBy == unit.Id) weight += Definition.Enemy(enemy.DefinitionId).Traits.BlockWeight;
            return weight;
        }

        /// <summary>저지 수가 줄었을 때(스킬 끝) 무게 합이 넘치면 뒤에 막힌 적부터 푼다 (적 목록 순서 기준).</summary>
        private void ReleaseExcessBlocked(Unit unit)
        {
            var capacity = BlockOf(unit);
            var kept = 0;
            foreach (var enemy in World.Enemies)
            {
                if (enemy.BlockedBy != unit.Id) continue;
                var weight = Definition.Enemy(enemy.DefinitionId).Traits.BlockWeight;
                if (kept + weight <= capacity) kept += weight;
                else enemy.BlockedBy = 0;
            }
        }

        private void DamageUnit(Unit unit, int damage)
        {
            unit.Hp -= damage;
            UnitDamaged?.Invoke(unit, damage);
            if (unit.Hp > 0)
            {
                GainSp(unit, SpType.OnHit);
                return;
            }

            // 막혀 있던 적은 RemoveUnit에서 풀리고, 다음 이동 단계부터 다시 걷는다. 쓰러지면 환급은 없고 재배치 규칙은 퇴각과 같다.
            World.RemoveUnit(unit, World.Tick + Definition.Deploy.RedeployCooldownTicks);
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

        /// <summary>적이 들어선 칸의 능력자가 자리(저지 수 - 막은 무게)가 이 적의 무게만큼 남으면 막는다. 비행 적과 자리가 없으면 그냥 지나간다.</summary>
        private void TryBlock(Enemy enemy)
        {
            var traits = Definition.Enemy(enemy.DefinitionId).Traits;
            if (traits.Flying) return;
            var unit = World.UnitAt(EnemyCell(enemy));
            if (unit == null) return;
            if (BlockedWeight(unit) + traits.BlockWeight > BlockOf(unit)) return;

            enemy.BlockedBy = unit.Id;
            EnemyBlocked?.Invoke(enemy, unit);
        }

        private void SpawnEnemies()
        {
            var spawns = Definition.Spawns;
            while (World.NextSpawnIndex < spawns.Count && spawns[World.NextSpawnIndex].Tick <= World.Tick)
            {
                var spawn = spawns[World.NextSpawnIndex];
                var definition = Definition.Enemy(spawn.EnemyId);

                // ?.Invoke의 인자는 구독자가 없으면 계산되지 않으므로 상태 변경을 인자에 넣지 않는다.
                var enemy = World.SpawnEnemy(spawn.EnemyId, spawn.RouteIndex, definition.Stats.MaxHp, definition.Traits.HealIntervalTicks);
                EnemySpawned?.Invoke(enemy);
            }
        }
    }
}
