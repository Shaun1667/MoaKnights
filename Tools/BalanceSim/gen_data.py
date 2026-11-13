"""데이터 에셋(BattleBalance · Stage01~10)을 읽어 Data.cs를 만든다. 변환은 BattleDefinitionFactory와 같게 Data.cs 안의 C#이 한다."""
import re
import struct
import uyaml

import os
import sys

D = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'MoaKnights', 'Assets', 'MoaKnights', 'Data') + '/'


def raw_hex_ints(path, key):
    m = re.search(r'^\s*' + key + r':\s*([0-9a-fA-F]*)\s*$', open(path, encoding='utf-8').read(), re.M)
    h = m.group(1)
    return [struct.unpack('<i', bytes.fromhex(h[i:i + 8]))[0] for i in range(0, len(h), 8)]


def cs_str(s):
    return '"' + str(s).replace('\\', '\\\\').replace('"', '\\"') + '"'


def cs_f(x):
    return repr(float(x)) + 'f'


bp = D + 'Balance/BattleBalance.asset'
b = uyaml.parse(bp)
hp = raw_hex_ints(bp, '_stackHpPercents')
atk = raw_hex_ints(bp, '_stackAttackPercents')
blk = raw_hex_ints(bp, '_stackBlockBonus')

out = []
out.append('using System; using System.Collections.Generic; using MoaKnights.Core.Sim; using MoaKnights.Core.Map;')
out.append('internal static class Data {')
out.append(f'  public const int Tps = {b["_ticksPerSecond"]}; public const int DefenseHp = {b["_defenseHp"]}; public const int TwoStar = {b["_twoStarHpPercent"]};')
out.append(f'  public const int StartCost = {b["_startCost"]}; public const int MaxCost = {b["_maxCost"]}; public const float CostRegenSeconds = {cs_f(b["_costRegenSeconds"])};')
out.append(f'  public const float FirstDelay = {cs_f(b["_firstSpawnDelaySeconds"])};')
out.append(f'  public static readonly int[] StackHp = {{ {", ".join(map(str, hp))} }}; public static readonly int[] StackAtk = {{ {", ".join(map(str, atk))} }}; public static readonly int[] StackBlock = {{ {", ".join(map(str, blk))} }};')
out.append(f'  public const int StackCostPercent = {b["_stackCostPercent"]}; public const float RedeploySeconds = {cs_f(b["_redeployCooldownSeconds"])}; public const int RedeployIncrease = {b["_redeployCostIncrease"]}; public const int RefundPercent = {b["_retreatRefundPercent"]};')

out.append('  public static readonly EnemyRow[] Enemies = {')
for e in b['_enemies']:
    out.append(f'    new EnemyRow({cs_str(e["Id"])}, {cs_f(e["SpeedCellsPerSecond"])}, {e["LeakDamage"]}, {e["MaxHp"]}, {e["Attack"]}, {cs_f(e["AttackIntervalSeconds"])}, '
               f'{e.get("Armor", 0)}, {"true" if e.get("Flying", 0) else "false"}, {e.get("RangedReach", 0)}, {e.get("HealAmount", 0)}, {cs_f(e.get("HealIntervalSeconds", 0) or 0)}, '
               f'{e.get("BlockWeight", 0)}, {cs_str(e.get("SplitInto", "") or "")}, {e.get("SplitCount", 0)}),')
out.append('  };')

out.append('  public static readonly UnitRow[] Units = {')
for u in b['_units']:
    rng = ', '.join(cs_str(r) for r in u['Range'])
    sk = ', '.join(f'new SkillRow({s["HealPercent"]}, {s["AttackBonusPercent"]}, {s["BlockBonus"]}, {cs_f(s["DurationSeconds"])})' for s in u['Skills'])
    out.append(f'    new UnitRow({cs_str(u["Id"])}, {u["Cost"]}, {u["Terrain"]}, {u["MaxHp"]}, {u["Attack"]}, {cs_f(u["AttackIntervalSeconds"])}, {u["BlockCount"]}, new[] {{ {rng} }}, {u["Role"]}, {u["SpType"]}, {u["MaxSp"]}, {cs_f(u["NaturalSpSeconds"])}, new[] {{ {sk} }}, {u["Rarity"]}),')
out.append('  };')

out.append('  public static readonly StageRow[] Stages = {')
for n in range(1, 11):
    s = uyaml.parse(D + f'Stages/Stage{n:02d}.asset')
    rows = ', '.join(cs_str(r) for r in s['_mapRows'])
    routes = ', '.join(cs_str(r) for r in s['_routes'])
    groups = ', '.join(f'new SpawnRow({cs_str(g["EnemyId"])}, {g["RouteIndex"]}, {cs_f(g["StartSeconds"])}, {cs_f(g["IntervalSeconds"])}, {g["Count"]})' for g in s['_spawnGroups'])
    out.append(f'    new StageRow({cs_str(s["_id"])}, new[] {{ {rows} }}, new[] {{ {routes} }}, new[] {{ {groups} }}),')
out.append('  };')
out.append('}')
open(sys.argv[1] if len(sys.argv) > 1 else 'Data.cs', 'w', encoding='utf-8').write('\n'.join(out) + '\n')
print('Data.cs', len(b['_enemies']), 'enemies', len(b['_units']), 'units')
