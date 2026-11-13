using System;
using System.Collections.Generic;
using System.Linq;
using MoaKnights.Core.Map;
using MoaKnights.Core.Sim;

// 에셋 행 (BattleBalanceAsset · StageAsset과 같은 칸)
internal sealed class EnemyRow
{
    public string Id; public float Speed; public int Leak, Hp, Attack; public float Interval;
    public int Armor; public bool Flying; public int Reach, Heal; public float HealSeconds; public int Weight; public string Split; public int SplitCount;
    public EnemyRow(string id, float speed, int leak, int hp, int attack, float interval, int armor, bool flying, int reach, int heal, float healSeconds, int weight, string split, int splitCount)
    { Id = id; Speed = speed; Leak = leak; Hp = hp; Attack = attack; Interval = interval; Armor = armor; Flying = flying; Reach = reach; Heal = heal; HealSeconds = healSeconds; Weight = weight; Split = split; SplitCount = splitCount; }
}

internal sealed class SkillRow
{
    public int Heal, Attack, Block; public float Duration;
    public SkillRow(int heal, int attack, int block, float duration) { Heal = heal; Attack = attack; Block = block; Duration = duration; }
}

internal sealed class UnitRow
{
    public string Id; public int Cost, Terrain, Hp, Attack; public float Interval; public int Block; public string[] Range; public int Role, SpType, MaxSp; public float Natural; public SkillRow[] Skills; public int Rarity;
    public UnitRow(string id, int cost, int terrain, int hp, int attack, float interval, int block, string[] range, int role, int spType, int maxSp, float natural, SkillRow[] skills, int rarity)
    { Id = id; Cost = cost; Terrain = terrain; Hp = hp; Attack = attack; Interval = interval; Block = block; Range = range; Role = role; SpType = spType; MaxSp = maxSp; Natural = natural; Skills = skills; Rarity = rarity; }
}

internal sealed class SpawnRow
{
    public string Enemy; public int Route; public float Start, Interval; public int Count;
    public SpawnRow(string enemy, int route, float start, float interval, int count) { Enemy = enemy; Route = route; Start = start; Interval = interval; Count = count; }
}

internal sealed class StageRow
{
    public string Id; public string[] Map, Routes; public SpawnRow[] Spawns;
    public StageRow(string id, string[] map, string[] routes, SpawnRow[] spawns) { Id = id; Map = map; Routes = routes; Spawns = spawns; }
}

/// <summary>BattleDefinitionFactory.Create와 같은 변환 (Mathf.RoundToInt = 가장 가까운 짝수 반올림).</summary>
internal static class Factory
{
    private static int R(float x) => (int)Math.Round(x, MidpointRounding.ToEven);
    private static int Ticks(float seconds) => R(seconds * Data.Tps);

    public static BattleDefinition Create(StageRow stage, IEnumerable<string> deck, SpawnRow[] spawnsOverride = null)
    {
        var map = GridMap.Parse(stage.Map);
        var enemies = Data.Enemies.Select(e => new EnemyDefinition(e.Id, R(e.Speed * 100f), e.Leak, new CombatStats(e.Hp, e.Attack, Ticks(e.Interval)),
            new EnemyTraits(e.Armor, e.Flying, e.Reach, e.Heal, Ticks(e.HealSeconds), e.Weight, e.Split, e.SplitCount))).ToList();
        var routes = stage.Routes.Select(r => EnemyRoute.Parse(map, r)).ToList();
        var spawns = new List<SpawnEvent>();
        foreach (var g in spawnsOverride ?? stage.Spawns)
            for (var i = 0; i < g.Count; i++) spawns.Add(new SpawnEvent(R((Data.FirstDelay + g.Start + g.Interval * i) * Data.Tps), g.Enemy, g.Route));
        var cost = new CostRules(Data.StartCost, Data.MaxCost, R(Data.CostRegenSeconds * Data.Tps));
        var units = deck.Select(id =>
        {
            var row = Data.Units.First(u => u.Id == id);
            var skills = row.Skills.Select(s => new SkillDefinition(s.Heal, s.Attack, s.Block, Ticks(s.Duration))).ToList();
            var natural = row.SpType == (int)SpType.Natural ? Ticks(row.Natural) : 0;
            return new UnitDefinition(row.Id, row.Cost, (UnitTerrain)row.Terrain, new CombatStats(row.Hp, row.Attack, Ticks(row.Interval)), row.Block,
                AttackRange.Parse(row.Range), new SkillSet((SpType)row.SpType, row.MaxSp, natural, skills), (UnitRole)row.Role);
        }).ToList();
        var deploy = new DeployRules(Data.StackHp, Data.StackAtk, Data.StackBlock, Data.StackCostPercent, Ticks(Data.RedeploySeconds), Data.RedeployIncrease, Data.RefundPercent);
        return new BattleDefinition(map, Data.Tps, Data.DefenseHp, enemies, routes, spawns, cost, units, deploy, Data.TwoStar);
    }
}
