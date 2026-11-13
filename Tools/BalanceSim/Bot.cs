using System;
using System.Collections.Generic;
using System.Linq;
using MoaKnights.Core.Map;
using MoaKnights.Core.Sim;

/// <summary>
/// 자동 배치 봇 (4-1 방식에 칸 고르기를 더함): 편성 순서대로 코스트가 되면 놓고, 다 놓으면 겹치고, SP가 차면 바로 스킬.
/// 지상: 길 칸 중 길이 많이 지나고 출구에 가까운 칸 + 범위가 덮는 길. 언덕: 범위가 덮는 길(출구 쪽 가중). 힐러: 아군을 많이 덮는 칸.
/// </summary>
internal static class Bot
{
    public sealed class Result { public BattleOutcome Outcome; public int Stars, Defense, Seconds, Leaked, UnitsLost; public Dictionary<string, int> LeakBy = new Dictionary<string, int>(); public List<string> Lost = new List<string>(); }

    private static readonly Direction[] Dirs = { Direction.Right, Direction.Up, Direction.Left, Direction.Down };

    private static Dictionary<GridPos, (int routes, float toExit)> RouteInfo(BattleDefinition d)
    {
        var info = new Dictionary<GridPos, (int, float)>();
        foreach (var r in d.Routes)
            for (var i = 1; i < r.Cells.Count - 1; i++)
            {
                var c = r.Cells[i];
                var toExit = 1f - (float)i / (r.Cells.Count - 1);
                info[c] = info.TryGetValue(c, out var v) ? (v.Item1 + 1, Math.Min(v.Item2, toExit)) : (1, toExit);
            }
        return info;
    }

    private static (GridPos cell, Direction dir, float score) BestSpot(Simulation s, UnitDefinition u, Dictionary<GridPos, (int routes, float toExit)> info)
    {
        var map = s.Map;
        var best = (cell: new GridPos(-1, -1), dir: Direction.Right, score: float.MinValue);
        for (var x = 0; x < map.Width; x++)
            for (var y = 0; y < map.Height; y++)
            {
                var cell = new GridPos(x, y);
                if (s.CheckPlacement(u.Id, cell) != PlacementResult.Ok) continue;
                foreach (var dir in Dirs)
                {
                    float score = 0;
                    if (u.Role == UnitRole.Healer)
                    {
                        foreach (var ally in s.World.Units) if (u.Range.Contains(cell, dir, ally.Cell)) score += 3;
                        foreach (var kv in info) if (u.Range.Contains(cell, dir, kv.Key)) score += 0.2f;
                    }
                    else
                    {
                        foreach (var kv in info) if (u.Range.Contains(cell, dir, kv.Key)) score += kv.Value.routes * (1.5f - kv.Value.toExit);
                        if (info.TryGetValue(cell, out var here)) score += here.routes * 4 + (1f - here.toExit) * 3 + (u.BlockCount > 0 ? 2 : 0);
                    }
                    if (score > best.score) best = (cell, dir, score);
                }
            }
        return best;
    }

    public static Result Play(BattleDefinition d, bool useSkills = true, int maxSeconds = 600)
    {
        var s = new Simulation(World.Create(1, "bot", d.DefenseHp, d.Cost.Start), d);
        var info = RouteInfo(d);
        var leaked = 0;
        var lost = 0;
        var result = new Result();
        s.EnemyLeaked += e => { leaked++; result.LeakBy[e.DefinitionId] = result.LeakBy.TryGetValue(e.DefinitionId, out var c) ? c + 1 : 1; };
        s.UnitDefeated += u => { lost++; result.Lost.Add(u.DefinitionId + "@" + s.World.Tick / d.TicksPerSecond); };
        var order = d.Units.ToList();
        var ticks = 0;
        while (!s.IsOver && ticks < maxSeconds * d.TicksPerSecond)
        {
            var next = order.FirstOrDefault(u => !s.World.IsDeployed(u.Id) && s.RedeployTicksLeft(u.Id) == 0);
            if (next != null)
            {
                if (s.World.Cost >= s.DeployCost(next.Id))
                {
                    var spot = BestSpot(s, next, info);
                    if (spot.score <= float.MinValue || s.TryPlaceUnit(next.Id, spot.cell, spot.dir) != PlacementResult.Ok) order.Remove(next);
                }
            }
            else
            {
                var stack = s.World.Units.Where(u => s.CheckStack(u.DefinitionId) == PlacementResult.Ok)
                    .OrderByDescending(u => { d.TryGetUnit(u.DefinitionId, out var def); return def.Role == UnitRole.Dealer ? def.Stats.Attack * 30f / def.Stats.AttackIntervalTicks : def.Role == UnitRole.Tank ? 5f : 1f; })
                    .FirstOrDefault();
                if (stack != null) s.TryStackUnit(stack.DefinitionId);
            }
            if (useSkills) foreach (var u in s.World.Units.ToArray()) if (s.CheckSkill(u.Id) == PlacementResult.Ok) s.TryUseSkill(u.Id);
            s.Step();
            ticks++;
        }
        result.Outcome = s.World.Outcome; result.Stars = s.Stars(); result.Defense = s.World.DefenseHp; result.Seconds = ticks / d.TicksPerSecond; result.Leaked = leaked; result.UnitsLost = lost;
        return result;
    }

    public static string Short(Result r) =>
        (r.Outcome == BattleOutcome.Victory ? "W" + r.Stars : r.Outcome == BattleOutcome.Defeat ? "L " : "- ") + string.Format(" hp{0,2} {1,3}s", r.Defense, r.Seconds);
}

internal static class Program
{
    public static readonly Dictionary<string, string[]> Decks = new Dictionary<string, string[]>
    {
        { "start4", new[] { "tank_01", "melee_01", "ranged_01", "healer_01" } },
        { "3star6", new[] { "tank_01", "melee_01", "ranged_01", "sniper_01", "vanguard_01", "healer_01" } },
        { "mix8", new[] { "tank_01", "melee_01", "blade_01", "ranged_01", "sniper_01", "archer_01", "guardian_01", "healer_01" } },
        { "four10", new[] { "guardian_01", "blade_01", "archer_01", "lancer_01", "cleric_01", "warden_01", "gunner_01", "mage_01", "bulwark_01", "medic_01" } },
        { "rare12", new[] { "guardian_01", "blade_01", "archer_01", "lancer_01", "cleric_01", "swordmaster_01", "starshot_01", "paladin_01", "mage_01", "mano_01", "irene_01", "bulwark_01" } },
    };

    public static void Main(string[] args)
    {
        var useProposed = args.Contains("proposed");
        Console.WriteLine(useProposed ? "== proposed spawn tables ==" : "== asset spawn tables ==");
        Console.WriteLine("stage     " + string.Join("  ", Decks.Keys.Select(k => k.PadRight(14))) + "  enemies");
        for (var i = 0; i < Data.Stages.Length; i++)
        {
            var stage = Data.Stages[i];
            var spawns = useProposed && Plans.Tables.TryGetValue(i + 1, out var p) ? p : null;
            var line = stage.Id.PadRight(10);
            var count = 0;
            foreach (var kv in Decks)
            {
                var d = Factory.Create(stage, kv.Value, spawns);
                count = d.Spawns.Count;
                line += Bot.Short(Bot.Play(d)).PadRight(16);
            }
            Console.WriteLine(line + count);
            if (args.Contains("detail" + (i + 1)))
                foreach (var kv in Decks)
                {
                    var r = Bot.Play(Factory.Create(stage, kv.Value, spawns));
                    Console.WriteLine("   " + kv.Key + " leaks: " + string.Join(", ", r.LeakBy.Select(x => x.Key + "x" + x.Value)) + " | lost: " + string.Join(", ", r.Lost));
                }
        }
    }
}
