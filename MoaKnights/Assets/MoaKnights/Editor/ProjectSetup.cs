using MoaKnights.Core.Map;
using MoaKnights.Core.Sim;
using MoaKnights.Game;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MoaKnights.EditorTools
{
    /// <summary>
    /// 에셋 · 씬 생성 한 곳. 몇 번 실행해도 같은 결과가 나온다.
    /// 데이터 에셋은 없을 때만 만들고, 이미 있는 값은 덮어쓰지 않는다. 씬은 매번 다시 만든다.
    /// </summary>
    public static class ProjectSetup
    {
        private const string Root = "Assets/MoaKnights";
        private const string BalancePath = Root + "/Data/Balance/BattleBalance.asset";
        private const string Stage01Path = Root + "/Data/Stages/Stage01.asset";
        private const string Stage02Path = Root + "/Data/Stages/Stage02.asset";
        private const string Stage03Path = Root + "/Data/Stages/Stage03.asset";
        private const string CatalogPath = Root + "/Data/Stages/StageCatalog.asset";
        private const string ViewPath = Root + "/Data/View/BattleView.asset";
        private const string GreyboxMaterialPath = Root + "/Art/Greybox/Greybox.mat";
        private const string UnitArtFolder = Root + "/Art/Units";
        private const string EnemyArtFolder = Root + "/Art/Enemies";
        private static readonly string[] ModelParts = { "body", "outfit" };
        private const string TextTablePath = Root + "/Data/Text/text.csv";
        private const string FontPath = Root + "/Fonts/NotoSansKR-VF.ttf";
        private const string ScenePath = Root + "/Scenes/Battle.unity";

        // 원본 기획의 게임 설명 그림을 따른 첫 맵. 입구는 오른쪽, 출구는 왼쪽, 경로는 돌아간다.
        private static readonly string[] Stage01Rows =
        {
            "#HHHHH#",
            "HHH..HH",
            "HHH..HH",
            "E.H...S",
            "H.H.HHH",
            "H...HHH",
            "#HHHHH#",
        };

        // 입구에서 아래로 내려갔다가 왼쪽으로 돌아 올라와 출구로 간다.
        private const string Stage01Route = "6,3 3,3 3,1 1,1 1,3 0,3";
        private const string FirstEnemyId = "dizes_01";
        private const string FastEnemyId = "dizes_fast";
        private const string TankEnemyId = "dizes_tank";

        // 스테이지 2: 입구 하나, 길 두 갈래가 출구 앞에서 만난다. 스테이지 3: 입구 둘이 가운데에서 합친다 (결정 2026-11-10, 사용자).
        private static readonly string[] Stage02Rows =
        {
            "#HHHHHHH#",
            "H.......H",
            "H.HHHHH.H",
            "S.HHHHH.E",
            "H.HHHHH.H",
            "H.......H",
            "#HHHHHHH#",
        };

        private static readonly string[] Stage02Routes = { "0,3 1,3 1,5 7,5 7,3 8,3", "0,3 1,3 1,1 7,1 7,3 8,3" };

        private static readonly string[] Stage03Rows =
        {
            "S...HHHH#",
            "HHH.HHHH#",
            "HHH.HHHHH",
            "HHH.....E",
            "HHH.HHHHH",
            "HHH.HHHH#",
            "S...HHHH#",
        };

        private static readonly string[] Stage03Routes = { "0,6 3,6 3,3 8,3", "0,0 3,0 3,3 8,3" };
        private const string MeleeUnitId = "melee_01";
        private const string RangedUnitId = "ranged_01";
        private const string TankUnitId = "tank_01";
        private const string HealerUnitId = "healer_01";
        private const string SniperUnitId = "sniper_01";
        private const string VanguardUnitId = "vanguard_01";
        private const string BladeUnitId = "blade_01";
        private const string IreneUnitId = "irene_01";

        // 공격 범위 (오른쪽이 앞). 근거리: 자기 칸 + 앞 1칸. 원거리: 앞 3x3.
        private static readonly string[] MeleeRange = { "@X" };
        private static readonly string[] RangedRange = { ".XXX", "OXXX", ".XXX" };

        [MenuItem("MoaKnights/Setup/Run All")]
        public static void RunAll()
        {
            if (AssetDatabase.FindAssets("t:TMP_Settings").Length == 0)
            {
                TMP_PackageResourceImporter.ImportResources(true, false, false);
                Debug.Log("[Setup] Importing TMP Essentials. Run this menu again when the import finishes.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning("[Setup] Cancelled: the open scene has unsaved changes.");
                return;
            }

            EnsureBalance();
            EnsureStages();
            EnsureView();
            AssetDatabase.SaveAssets();
            BuildBattleScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[Setup] Done: " + ScenePath);
        }

        private static BattleBalanceAsset EnsureBalance()
        {
            var asset = LoadOrCreate<BattleBalanceAsset>(BalancePath);
            var so = new SerializedObject(asset);

            // 비어 있는 값에만 BALANCE.md 값을 넣는다. 이후 값은 에셋에서 고친다.
            FillIfZero(so.FindProperty("_startCost"), 40);
            FillIfZero(so.FindProperty("_costRegenSeconds"), 1f);
            FillIfZero(so.FindProperty("_maxCost"), 99);
            FillIfZero(so.FindProperty("_ticksPerSecond"), 30);
            FillIfZero(so.FindProperty("_dragSlowPercent"), 10);
            FillIfZero(so.FindProperty("_firstSpawnDelaySeconds"), 20f);
            FillIfZero(so.FindProperty("_defenseHp"), 10);

            var enemies = so.FindProperty("_enemies");
            if (enemies.arraySize == 0)
            {
                enemies.arraySize = 1;
                var row = enemies.GetArrayElementAtIndex(0);
                row.FindPropertyRelative("Id").stringValue = FirstEnemyId;
                row.FindPropertyRelative("SpeedCellsPerSecond").floatValue = 1f;
                row.FindPropertyRelative("LeakDamage").intValue = 1;
            }

            var units = so.FindProperty("_units");
            if (units.arraySize == 0)
            {
                units.arraySize = 2;
                SetUnitRow(units.GetArrayElementAtIndex(0), MeleeUnitId, 10, UnitTerrain.Ground);
                SetUnitRow(units.GetArrayElementAtIndex(1), RangedUnitId, 12, UnitTerrain.Hill);
            }

            // 전투 칸은 1-3 뒤에 생겼으므로, 비어 있는 행에만 BALANCE 5번 값을 넣는다.
            for (var i = 0; i < units.arraySize; i++)
            {
                var row = units.GetArrayElementAtIndex(i);
                var id = row.FindPropertyRelative("Id").stringValue;
                if (id == MeleeUnitId) FillCombat(row, 100, 20, 1f, 2, MeleeRange);
                if (id == RangedUnitId) FillCombat(row, 60, 15, 1.2f, 0, RangedRange);
            }

            for (var i = 0; i < enemies.arraySize; i++)
            {
                var row = enemies.GetArrayElementAtIndex(i);
                if (row.FindPropertyRelative("Id").stringValue == FirstEnemyId) FillCombat(row, 60, 10, 1.5f, -1, null);
            }

            // 3-1 새 적 (BALANCE 3번). 행이 없을 때만 만든다.
            var fast = RowById(enemies, FastEnemyId, out var fastCreated);
            if (fastCreated)
            {
                fast.FindPropertyRelative("SpeedCellsPerSecond").floatValue = 2f;
                fast.FindPropertyRelative("LeakDamage").intValue = 1;
                SetEnemyCombat(fast, 30, 5, 1f);
            }

            var tank = RowById(enemies, TankEnemyId, out var tankCreated);
            if (tankCreated)
            {
                tank.FindPropertyRelative("SpeedCellsPerSecond").floatValue = 0.6f;
                tank.FindPropertyRelative("LeakDamage").intValue = 2;
                SetEnemyCombat(tank, 150, 15, 1.5f);
            }

            FillIfZero(so.FindProperty("_twoStarHpPercent"), 50);

            // SP · 스킬 (BALANCE 7번). 비어 있는 행에만 넣는다.
            for (var i = 0; i < units.arraySize; i++)
            {
                var row = units.GetArrayElementAtIndex(i);
                var id = row.FindPropertyRelative("Id").stringValue;
                if (id == MeleeUnitId)
                    FillSkills(row, SpType.OnHit, 8, 0f, new[] { new[] { 30, 0, 0, 0 }, new[] { 40, 0, 0, 0 }, new[] { 50, 0, 1, 8 } });
                if (id == RangedUnitId)
                    FillSkills(row, SpType.OnAttack, 10, 0f, new[] { new[] { 0, 50, 0, 10 }, new[] { 0, 80, 0, 10 }, new[] { 0, 120, 0, 10 } });
            }

            // 3-2 포지션: 이 칸은 처음 생겼을 때 0(탱커)이다. 근거리 · 원거리는 딜러로 맞춘다 (0일 때만).
            for (var i = 0; i < units.arraySize; i++)
            {
                var row = units.GetArrayElementAtIndex(i);
                var id = row.FindPropertyRelative("Id").stringValue;
                var role = row.FindPropertyRelative("Role");
                if ((id == MeleeUnitId || id == RangedUnitId) && role.enumValueIndex == (int)UnitRole.Tank)
                    role.enumValueIndex = (int)UnitRole.Dealer;
            }

            // 3-2 새 능력자 넷 (BALANCE 5-7 ~ 5-10, 7-6 ~ 7-9). 행이 없을 때만 만든다. 목록 순서의 앞 넷이 기본 편성.
            AddUnitIfMissing(units, TankUnitId, 14, UnitTerrain.Ground, UnitRole.Tank, 200, 10, 1.5f, 3, new[] { "@" },
                SpType.OnHit, 10, 0f, new[] { new[] { 20, 0, 1, 10 }, new[] { 30, 0, 1, 10 }, new[] { 40, 0, 2, 10 } });
            AddUnitIfMissing(units, HealerUnitId, 12, UnitTerrain.Hill, UnitRole.Healer, 50, 15, 1.5f, 0, new[] { "XXX", "X@X", "XXX" },
                SpType.Natural, 15, 1f, new[] { new[] { 0, 50, 0, 10 }, new[] { 0, 80, 0, 10 }, new[] { 0, 120, 0, 10 } });
            AddUnitIfMissing(units, SniperUnitId, 16, UnitTerrain.Hill, UnitRole.Dealer, 50, 40, 2.5f, 0, new[] { "OXXXX" },
                SpType.Natural, 20, 1f, new[] { new[] { 0, 60, 0, 10 }, new[] { 0, 100, 0, 10 }, new[] { 0, 150, 0, 10 } });
            AddUnitIfMissing(units, VanguardUnitId, 6, UnitTerrain.Ground, UnitRole.Dealer, 70, 12, 0.8f, 1, new[] { "@X" },
                SpType.OnAttack, 12, 0f, new[] { new[] { 0, 40, 0, 8 }, new[] { 0, 60, 0, 8 }, new[] { 0, 80, 0, 8 } });

            // G-4 첫 4 · 5성 (BALANCE 5-13 · 5-14, 7-10 · 7-11). 행이 없을 때만 만든다.
            AddUnitIfMissing(units, BladeUnitId, 12, UnitTerrain.Ground, UnitRole.Dealer, 130, 26, 0.9f, 2, new[] { "@X" },
                SpType.OnAttack, 10, 0f, new[] { new[] { 0, 60, 0, 8 }, new[] { 0, 90, 0, 8 }, new[] { 0, 130, 0, 8 } }, 4);
            AddUnitIfMissing(units, IreneUnitId, 22, UnitTerrain.Hill, UnitRole.Healer, 90, 30, 1.4f, 0, new[] { "XXXXX", "XXXXX", "XX@XX", "XXXXX", "XXXXX" },
                SpType.Natural, 18, 1f, new[] { new[] { 0, 80, 0, 12 }, new[] { 0, 120, 0, 12 }, new[] { 0, 180, 0, 12 } }, 5);
            so.FindProperty("_deckSize").intValue = 12;   // 결정 2026-11-11, 사용자: 출전 12명 (예전 4명이면 바꿈)

            // 등급: 비어 있으면 3성 (지금 능력자는 모두 3성, BALANCE 5-12).
            for (var i = 0; i < units.arraySize; i++)
                FillIfZero(units.GetArrayElementAtIndex(i).FindPropertyRelative("Rarity"), 3);

            // 중첩 · 퇴각 · 재배치 (BALANCE 6번). 비어 있을 때만 넣는다.
            FillIfEmpty(so.FindProperty("_stackHpPercents"), 100, 130, 160);
            FillIfEmpty(so.FindProperty("_stackAttackPercents"), 100, 130, 160);
            FillIfEmpty(so.FindProperty("_stackBlockBonus"), 0, 0, 1);
            FillIfZero(so.FindProperty("_stackCostPercent"), 100);
            FillIfZero(so.FindProperty("_redeployCooldownSeconds"), 10f);
            FillIfZero(so.FindProperty("_redeployCostIncrease"), 5);
            FillIfZero(so.FindProperty("_retreatRefundPercent"), 50);


            // 뽑기 · 재화 (BALANCE 10번). 비어 있을 때만 넣는다.
            FillIfZero(so.FindProperty("_gachaSingleCost"), 10);
            FillIfZero(so.FindProperty("_gachaMultiCount"), 10);
            FillIfZero(so.FindProperty("_gachaMultiCost"), 90);
            FillIfZero(so.FindProperty("_gachaDuplicateRefund"), 2);
            FillIfZero(so.FindProperty("_currencyPerStar"), 10);
            FillIfZero(so.FindProperty("_startingCurrency"), 100);
            var weights = so.FindProperty("_gachaRarityWeights");
            if (weights.arraySize == 0)
            {
                var table = new[] { new[] { 3, 90 }, new[] { 4, 8 }, new[] { 5, 2 } };
                weights.arraySize = table.Length;
                for (var w = 0; w < table.Length; w++)
                {
                    weights.GetArrayElementAtIndex(w).FindPropertyRelative("Rarity").intValue = table[w][0];
                    weights.GetArrayElementAtIndex(w).FindPropertyRelative("Weight").intValue = table[w][1];
                }
            }
            var starting = so.FindProperty("_startingUnits");
            if (starting.arraySize == 0)
            {
                var ids = new[] { MeleeUnitId, RangedUnitId, TankUnitId, HealerUnitId };
                starting.arraySize = ids.Length;
                for (var u = 0; u < ids.Length; u++) starting.GetArrayElementAtIndex(u).stringValue = ids[u];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        /// <summary>스킬 행: 회복 % · 공격 추가 % · 저지 추가 · 지속 초.</summary>
        private static void FillSkills(SerializedProperty row, SpType spType, int maxSp, float naturalSeconds, int[][] skills)
        {
            if (row.FindPropertyRelative("MaxSp").intValue != 0) return;
            row.FindPropertyRelative("SpType").enumValueIndex = (int)spType;
            row.FindPropertyRelative("MaxSp").intValue = maxSp;
            row.FindPropertyRelative("NaturalSpSeconds").floatValue = naturalSeconds;

            var list = row.FindPropertyRelative("Skills");
            list.arraySize = skills.Length;
            for (var i = 0; i < skills.Length; i++)
            {
                var skill = list.GetArrayElementAtIndex(i);
                skill.FindPropertyRelative("HealPercent").intValue = skills[i][0];
                skill.FindPropertyRelative("AttackBonusPercent").intValue = skills[i][1];
                skill.FindPropertyRelative("BlockBonus").intValue = skills[i][2];
                skill.FindPropertyRelative("DurationSeconds").floatValue = skills[i][3];
            }
        }

        private static void FillIfEmpty(SerializedProperty array, params int[] values)
        {
            if (array.arraySize != 0) return;
            array.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).intValue = values[i];
        }

        /// <summary>
        /// 능력자 행이 없으면 끝에 만들고 모든 칸을 채운다. 배열을 늘리면 Unity가 마지막 행 값을 복사하므로 조건 없이 덮어쓴다.
        /// 스킬 행: 회복 % · 공격 추가 % · 저지 추가 · 지속 초.
        /// </summary>
        private static void AddUnitIfMissing(SerializedProperty units, string id, int cost, UnitTerrain terrain, UnitRole role,
            int maxHp, int attack, float interval, int blockCount, string[] range, SpType spType, int maxSp, float naturalSeconds, int[][] skills,
            int rarity = 3)
        {
            var row = RowById(units, id, out var created);
            if (!created) return;

            // 배열을 늘리면 Unity가 마지막 행 값을 복사하므로 등급도 덮어쓴다.
            row.FindPropertyRelative("Rarity").intValue = rarity;

            row.FindPropertyRelative("Cost").intValue = cost;
            row.FindPropertyRelative("Terrain").enumValueIndex = (int)terrain;
            row.FindPropertyRelative("Role").enumValueIndex = (int)role;
            row.FindPropertyRelative("MaxHp").intValue = maxHp;
            row.FindPropertyRelative("Attack").intValue = attack;
            row.FindPropertyRelative("AttackIntervalSeconds").floatValue = interval;
            row.FindPropertyRelative("BlockCount").intValue = blockCount;

            var rangeRows = row.FindPropertyRelative("Range");
            rangeRows.arraySize = range.Length;
            for (var r = 0; r < range.Length; r++) rangeRows.GetArrayElementAtIndex(r).stringValue = range[r];

            row.FindPropertyRelative("MaxSp").intValue = 0;   // FillSkills가 비었다고 보고 채우게 한다
            FillSkills(row, spType, maxSp, naturalSeconds, skills);
        }

        /// <summary>새로 만든 적 행에 쓴다. 배열을 늘리면 Unity가 마지막 행 값을 복사하므로 조건 없이 덮어쓴다.</summary>
        private static void SetEnemyCombat(SerializedProperty row, int maxHp, int attack, float interval)
        {
            row.FindPropertyRelative("MaxHp").intValue = maxHp;
            row.FindPropertyRelative("Attack").intValue = attack;
            row.FindPropertyRelative("AttackIntervalSeconds").floatValue = interval;
        }

        private static void FillCombat(SerializedProperty row, int maxHp, int attack, float interval, int blockCount, string[] range)
        {
            if (row.FindPropertyRelative("MaxHp").intValue != 0) return;
            row.FindPropertyRelative("MaxHp").intValue = maxHp;
            row.FindPropertyRelative("Attack").intValue = attack;
            row.FindPropertyRelative("AttackIntervalSeconds").floatValue = interval;
            if (blockCount >= 0) row.FindPropertyRelative("BlockCount").intValue = blockCount;
            if (range == null) return;

            var rows = row.FindPropertyRelative("Range");
            rows.arraySize = range.Length;
            for (var r = 0; r < range.Length; r++) rows.GetArrayElementAtIndex(r).stringValue = range[r];
        }

        private static void SetUnitRow(SerializedProperty row, string id, int cost, UnitTerrain terrain)
        {
            row.FindPropertyRelative("Id").stringValue = id;
            row.FindPropertyRelative("Cost").intValue = cost;
            row.FindPropertyRelative("Terrain").enumValueIndex = (int)terrain;
        }

        /// <summary>스테이지 셋과 목록. 비어 있는 칸에만 값을 넣는다 (맵 · 길 · 등장 표는 에셋에서 고친다).</summary>
        private static void EnsureStages()
        {
            var stage01 = EnsureStage(Stage01Path, "stage_01", Stage01Rows, new[] { Stage01Route }, new[]
            {
                new Spawn(FirstEnemyId, 0, 0f, 2f, 10),
            });
            var stage02 = EnsureStage(Stage02Path, "stage_02", Stage02Rows, Stage02Routes, new[]
            {
                new Spawn(FirstEnemyId, 0, 0f, 3f, 8),
                new Spawn(FirstEnemyId, 1, 1.5f, 3f, 8),
                new Spawn(FastEnemyId, 0, 20f, 4f, 3),
                new Spawn(FastEnemyId, 1, 22f, 4f, 3),
            });
            var stage03 = EnsureStage(Stage03Path, "stage_03", Stage03Rows, Stage03Routes, new[]
            {
                new Spawn(FirstEnemyId, 0, 0f, 3f, 6),
                new Spawn(FirstEnemyId, 1, 1.5f, 3f, 6),
                new Spawn(TankEnemyId, 0, 15f, 10f, 2),
                new Spawn(TankEnemyId, 1, 20f, 10f, 2),
                new Spawn(FastEnemyId, 0, 30f, 2f, 4),
            });

            var catalog = LoadOrCreate<StageCatalogAsset>(CatalogPath);
            var so = new SerializedObject(catalog);
            var list = so.FindProperty("_stages");
            if (list.arraySize == 0)
            {
                var stages = new[] { stage01, stage02, stage03 };
                list.arraySize = stages.Length;
                for (var i = 0; i < stages.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = stages[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private readonly struct Spawn
        {
            public readonly string EnemyId;
            public readonly int Route;
            public readonly float Start;
            public readonly float Interval;
            public readonly int Count;

            public Spawn(string enemyId, int route, float start, float interval, int count)
            {
                EnemyId = enemyId;
                Route = route;
                Start = start;
                Interval = interval;
                Count = count;
            }
        }

        private static StageAsset EnsureStage(string path, string stageId, string[] mapRows, string[] routeList, Spawn[] spawns)
        {
            var asset = LoadOrCreate<StageAsset>(path);
            var so = new SerializedObject(asset);

            var id = so.FindProperty("_id");
            if (string.IsNullOrEmpty(id.stringValue)) id.stringValue = stageId;

            FillIfEmpty(so.FindProperty("_mapRows"), mapRows);
            FillIfEmpty(so.FindProperty("_routes"), routeList);

            var groups = so.FindProperty("_spawnGroups");
            if (groups.arraySize == 0)
            {
                groups.arraySize = spawns.Length;
                for (var i = 0; i < spawns.Length; i++)
                {
                    var group = groups.GetArrayElementAtIndex(i);
                    group.FindPropertyRelative("EnemyId").stringValue = spawns[i].EnemyId;
                    group.FindPropertyRelative("RouteIndex").intValue = spawns[i].Route;
                    group.FindPropertyRelative("StartSeconds").floatValue = spawns[i].Start;
                    group.FindPropertyRelative("IntervalSeconds").floatValue = spawns[i].Interval;
                    group.FindPropertyRelative("Count").intValue = spawns[i].Count;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private static void FillIfEmpty(SerializedProperty array, string[] values)
        {
            if (array.arraySize != 0) return;
            array.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).stringValue = values[i];
        }

        /// <summary>이 ID의 행을 찾고, 없으면 끝에 하나 더한다. 새로 만들었으면 created = true.</summary>
        private static SerializedProperty RowById(SerializedProperty array, string id, out bool created)
        {
            for (var i = 0; i < array.arraySize; i++)
            {
                var row = array.GetArrayElementAtIndex(i);
                if (row.FindPropertyRelative("Id").stringValue != id) continue;
                created = false;
                return row;
            }

            array.arraySize++;
            var added = array.GetArrayElementAtIndex(array.arraySize - 1);
            added.FindPropertyRelative("Id").stringValue = id;
            created = true;
            return added;
        }

        private static BattleViewAsset EnsureView()
        {
            var existing = AssetDatabase.LoadAssetAtPath<BattleViewAsset>(ViewPath);
            var asset = existing != null ? existing : CreateView();

            var so = new SerializedObject(asset);
            var enemies = so.FindProperty("_enemies");
            if (enemies.arraySize == 0)
            {
                enemies.arraySize = 1;
                var row = enemies.GetArrayElementAtIndex(0);
                row.FindPropertyRelative("Id").stringValue = FirstEnemyId;
                row.FindPropertyRelative("Color").colorValue = new Color(0.62f, 0.25f, 0.85f);
                row.FindPropertyRelative("Size").floatValue = 0.35f;
            }

            SetEnemyLookIfNew(enemies, FastEnemyId, new Color(0.95f, 0.45f, 0.75f), 0.28f);
            SetEnemyLookIfNew(enemies, TankEnemyId, new Color(0.40f, 0.18f, 0.55f), 0.45f);
            FillModelPrefabs(enemies, EnemyArtFolder);
            FillIfEmpty(so.FindProperty("_victoryColor"), new Color(1.00f, 0.85f, 0.35f));
            FillIfEmpty(so.FindProperty("_defeatColor"), new Color(0.90f, 0.35f, 0.30f));
            FillIfEmpty(so.FindProperty("_starOnColor"), new Color(1.00f, 0.85f, 0.30f));
            FillIfEmpty(so.FindProperty("_starOffColor"), new Color(0.30f, 0.30f, 0.33f));
            FillIfEmpty(so.FindProperty("_formationSelectedColor"), new Color(1.00f, 0.82f, 0.40f));

            var unitLooks = so.FindProperty("_units");
            SetUnitLookIfNew(unitLooks, TankUnitId, new Color(0.55f, 0.62f, 0.70f), 0.50f);
            SetUnitLookIfNew(unitLooks, HealerUnitId, new Color(0.45f, 0.90f, 0.55f), 0.36f);
            SetUnitLookIfNew(unitLooks, SniperUnitId, new Color(0.85f, 0.85f, 0.95f), 0.34f);
            SetUnitLookIfNew(unitLooks, VanguardUnitId, new Color(0.90f, 0.40f, 0.30f), 0.40f);
            SetUnitLookIfNew(unitLooks, BladeUnitId, new Color(0.25f, 0.35f, 0.70f), 0.40f);
            SetUnitLookIfNew(unitLooks, IreneUnitId, new Color(0.95f, 0.70f, 0.85f), 0.38f);
            FillModelPrefabs(unitLooks, UnitArtFolder);
            FillUnitPortraits(unitLooks);

            FillIfZero(so.FindProperty("_damageNumberSeconds"), 1f);
            FillIfEmpty(so.FindProperty("_damageNumberColor"), new Color(1f, 0.35f, 0.3f));

            var units = so.FindProperty("_units");
            if (units.arraySize == 0)
            {
                units.arraySize = 2;
                SetUnitVisual(units.GetArrayElementAtIndex(0), MeleeUnitId, new Color(0.95f, 0.70f, 0.20f), 0.45f);
                SetUnitVisual(units.GetArrayElementAtIndex(1), RangedUnitId, new Color(0.20f, 0.75f, 0.85f), 0.40f);
            }

            FillIfZero(so.FindProperty("_messageSeconds"), 1.5f);
            FillIfEmpty(so.FindProperty("_directionMarkerColor"), new Color(0.95f, 0.95f, 0.90f));
            FillIfEmpty(so.FindProperty("_previewValidColor"), new Color(0.35f, 0.85f, 0.45f));
            FillIfEmpty(so.FindProperty("_previewInvalidColor"), new Color(0.90f, 0.25f, 0.20f));
            FillIfZero(so.FindProperty("_directionDragPixels"), 24f);
            var cardSize = so.FindProperty("_cardSize");
            if (cardSize.vector2Value == Vector2.zero) cardSize.vector2Value = new Vector2(160f, 190f);
            FillIfZero(so.FindProperty("_cardSpacing"), 16f);
            FillIfEmpty(so.FindProperty("_cardColor"), new Color(0.90f, 0.88f, 0.82f));
            FillIfEmpty(so.FindProperty("_cardDisabledColor"), new Color(0.45f, 0.45f, 0.45f));
            FillIfEmpty(so.FindProperty("_cardTextColor"), new Color(0.10f, 0.10f, 0.10f));
            FillIfEmpty(so.FindProperty("_cardPortraitDisabledColor"), new Color(0.55f, 0.55f, 0.55f));
            FillIfZero(so.FindProperty("_cameraBottomReserve"), 0.22f);
            FillIfZero(so.FindProperty("_cameraFieldOfView"), 30f);
            FillIfZero(so.FindProperty("_cameraTopReserve"), 0.13f);
            FillIfEmpty(so.FindProperty("_gachaRarity3Color"), new Color(0.35f, 0.65f, 1.00f));
            FillIfEmpty(so.FindProperty("_gachaRarity4Color"), new Color(0.72f, 0.42f, 1.00f));
            FillIfEmpty(so.FindProperty("_gachaRarity5Color"), new Color(1.00f, 0.80f, 0.25f));
            FillIfEmpty(so.FindProperty("_gachaCardBackColor"), new Color(0.16f, 0.18f, 0.24f));
            FillIfZero(so.FindProperty("_gachaLightSeconds"), 1.6f);
            FillIfZero(so.FindProperty("_gachaSpreadSeconds"), 0.4f);
            FillIfZero(so.FindProperty("_gachaFlipSeconds"), 0.3f);
            FillIfZero(so.FindProperty("_gachaFlipIntervalSeconds"), 0.15f);
            FillIfEmpty(so.FindProperty("_rangeColor"), new Color(0.95f, 0.55f, 0.15f));
            var hpBarSize = so.FindProperty("_hpBarSize");
            if (hpBarSize.vector2Value == Vector2.zero) hpBarSize.vector2Value = new Vector2(0.6f, 0.06f);
            FillIfEmpty(so.FindProperty("_hpBarBackColor"), new Color(0.12f, 0.12f, 0.12f));
            FillIfEmpty(so.FindProperty("_hpBarEnemyColor"), new Color(0.90f, 0.30f, 0.25f));
            FillIfEmpty(so.FindProperty("_hpBarUnitColor"), new Color(0.35f, 0.85f, 0.45f));
            FillIfEmpty(so.FindProperty("_damageToEnemyColor"), new Color(1.00f, 0.95f, 0.70f));
            FillIfEmpty(so.FindProperty("_damageToUnitColor"), new Color(1.00f, 0.35f, 0.30f));
            FillIfZero(so.FindProperty("_floatingNumberSeconds"), 0.8f);
            FillIfZero(so.FindProperty("_floatingNumberRise"), 0.6f);
            FillIfZero(so.FindProperty("_floatingNumberSize"), 3f);
            FillIfEmpty(so.FindProperty("_stagePipColor"), new Color(1.00f, 0.85f, 0.30f));
            var actionOffset = so.FindProperty("_unitActionOffset");
            if (actionOffset.vector2Value == Vector2.zero) actionOffset.vector2Value = new Vector2(0f, 40f);
            FillIfEmpty(so.FindProperty("_spColor"), new Color(0.30f, 0.60f, 0.95f));
            FillIfEmpty(so.FindProperty("_spFullColor"), new Color(0.55f, 0.95f, 1.00f));
            FillIfEmpty(so.FindProperty("_skillActiveColor"), new Color(1.00f, 0.60f, 0.20f));

            so.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private static void SetUnitLookIfNew(SerializedProperty units, string id, Color color, float size)
        {
            var row = RowById(units, id, out var created);
            if (!created) return;
            row.FindPropertyRelative("Prefab").objectReferenceValue = null;
            row.FindPropertyRelative("Portrait").objectReferenceValue = null;   // 마지막 행에서 복사된 얼굴을 지운다
            row.FindPropertyRelative("Color").colorValue = color;
            row.FindPropertyRelative("Size").floatValue = size;
        }

        /// <summary>
        /// &lt;artFolder&gt;/&lt;id&gt;/&lt;id&gt;.fbx가 있는 줄(능력자 Art/Units · 적 Art/Enemies)은 프리팹을 만들어(없을 때만) 비어 있는 모습 칸에 넣는다.
        /// FBX는 Tools/Blender/combine_character.py가 만든 것: 재질 이름 body(· outfit), 텍스처 &lt;id&gt;_&lt;재질&gt;_albedo.png, 앞이 +X.
        /// </summary>
        private static void FillModelPrefabs(SerializedProperty rows, string artFolder)
        {
            for (var i = 0; i < rows.arraySize; i++)
            {
                var row = rows.GetArrayElementAtIndex(i);
                var prefab = row.FindPropertyRelative("Prefab");
                if (prefab.objectReferenceValue != null) continue;
                var made = EnsureModelPrefab(artFolder, row.FindPropertyRelative("Id").stringValue);
                if (made != null) prefab.objectReferenceValue = made;
            }
        }

        private static GameObject EnsureModelPrefab(string artFolder, string id)
        {
            var folder = artFolder + "/" + id;
            var fbxPath = folder + "/" + id + ".fbx";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath) == null) return null;

            var prefabPath = folder + "/" + id + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing != null) return existing;

            // 재질: 텍스처가 있는 부위마다 URP Lit, 없을 때만 만든다. FBX의 같은 이름 재질을 이것으로 바꿔 끼운다.
            var importer = (ModelImporter)AssetImporter.GetAtPath(fbxPath);
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            foreach (var part in ModelParts)
            {
                var material = EnsureModelMaterial(folder, id, part);
                if (material != null) importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), part), material);
            }
            importer.SaveAndReimport();

            var root = new GameObject(id);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath));
            model.transform.SetParent(root.transform, false);
            var prefabAsset = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefabAsset;
        }

        /// <summary>
        /// Art/Units/&lt;id&gt;/&lt;id&gt;_portrait.png가 있는 능력자는 스프라이트로 가져와(설정은 한 번만) 비어 있는 얼굴 칸에 넣는다.
        /// 그림은 Tools/Blender/make_portrait.py가 컨셉 그림에서 자른 것.
        /// </summary>
        private static void FillUnitPortraits(SerializedProperty units)
        {
            for (var i = 0; i < units.arraySize; i++)
            {
                var row = units.GetArrayElementAtIndex(i);
                var portrait = row.FindPropertyRelative("Portrait");
                if (portrait.objectReferenceValue != null) continue;

                var id = row.FindPropertyRelative("Id").stringValue;
                var path = UnitArtFolder + "/" + id + "/" + id + "_portrait.png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                if (importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.SaveAndReimport();
                }
                portrait.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
        }

        /// <summary>부위 재질. 그 부위 텍스처가 없으면(겉옷 없는 적) null.</summary>
        private static Material EnsureModelMaterial(string folder, string id, string part)
        {
            var path = folder + "/" + id + "_" + part + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/" + id + "_" + part + "_albedo.png");
            if (texture == null) return null;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Smoothness", 0.1f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void SetEnemyLookIfNew(SerializedProperty enemies, string id, Color color, float size)
        {
            var row = RowById(enemies, id, out var created);
            if (!created) return;
            row.FindPropertyRelative("Color").colorValue = color;
            row.FindPropertyRelative("Size").floatValue = size;
        }

        private static void SetUnitVisual(SerializedProperty row, string id, Color color, float size)
        {
            row.FindPropertyRelative("Id").stringValue = id;
            row.FindPropertyRelative("Color").colorValue = color;
            row.FindPropertyRelative("Size").floatValue = size;
        }

        private static void FillIfEmpty(SerializedProperty property, Color value)
        {
            if (Mathf.Approximately(property.colorValue.a, 0f)) property.colorValue = value;
        }

        private static BattleViewAsset CreateView()
        {
            var asset = LoadOrCreate<BattleViewAsset>(ViewPath);
            var so = new SerializedObject(asset);
            so.FindProperty("_cellMaterial").objectReferenceValue = EnsureGreyboxMaterial();
            so.FindProperty("_cellSize").floatValue = 1f;
            so.FindProperty("_cellGap").floatValue = 0.04f;
            so.FindProperty("_cameraPitch").floatValue = 58f;
            so.FindProperty("_cameraYaw").floatValue = 0f;
            so.FindProperty("_cameraPerspective").boolValue = true;
            so.FindProperty("_cameraFieldOfView").floatValue = 30f;
            so.FindProperty("_cameraDistance").floatValue = 30f;
            so.FindProperty("_cameraPadding").floatValue = 0.6f;

            var cells = so.FindProperty("_cells");
            cells.arraySize = 5;
            SetCell(cells, 0, CellType.Blocked, new Color(0.08f, 0.09f, 0.11f), 0.05f);
            SetCell(cells, 1, CellType.Ground, new Color(0.55f, 0.55f, 0.52f), 0.2f);
            SetCell(cells, 2, CellType.Hill, new Color(0.30f, 0.38f, 0.30f), 0.4f);
            SetCell(cells, 3, CellType.Entrance, new Color(0.75f, 0.18f, 0.15f), 0.25f);
            SetCell(cells, 4, CellType.Exit, new Color(0.15f, 0.40f, 0.80f), 0.25f);
            so.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private static void SetCell(SerializedProperty cells, int index, CellType type, Color color, float height)
        {
            var cell = cells.GetArrayElementAtIndex(index);
            cell.FindPropertyRelative("Type").enumValueIndex = (int)type;
            cell.FindPropertyRelative("Color").colorValue = color;
            cell.FindPropertyRelative("Height").floatValue = height;
        }

        private static Material EnsureGreyboxMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(GreyboxMaterialPath);
            if (material != null) return material;

            EnsureFolderChain(GreyboxMaterialPath);
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, GreyboxMaterialPath);
            return material;
        }

        private static void BuildBattleScene()
        {
            EnsureFolderChain(ScenePath);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var cameraObject = Camera.main;
            cameraObject.clearFlags = CameraClearFlags.SolidColor;
            cameraObject.backgroundColor = new Color(0.05f, 0.06f, 0.08f);
            var cameraRig = cameraObject.gameObject.AddComponent<BattleCameraRig>();

            var mapView = new GameObject("MapView", typeof(MapView)).GetComponent<MapView>();
            var enemyView = new GameObject("EnemyView", typeof(EnemyView)).GetComponent<EnemyView>();
            var unitView = new GameObject("UnitView", typeof(UnitView)).GetComponent<UnitView>();
            var placement = new GameObject("PlacementController", typeof(PlacementController)).GetComponent<PlacementController>();
            var damageNumbers = new GameObject("DamageNumbers", typeof(DamageNumbers)).GetComponent<DamageNumbers>();
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var title = CreateLabel(canvasGo.transform, "TitleLabel", new Vector2(0, -50), 48);
            var defenseHp = CreateLabel(canvasGo.transform, "DefenseHpLabel", new Vector2(0, -110), 32);
            var damage = CreateLabel(canvasGo.transform, "DamageLabel", new Vector2(0, -165), 40);

            // 아래쪽: 카드 줄, 그 위에 코스트, 다시 그 위에 배치 안내 글.
            var cost = CreateLabel(canvasGo.transform, "CostLabel", new Vector2(0, 245), 32, bottom: true);
            var message = CreateLabel(canvasGo.transform, "MessageLabel", new Vector2(0, 295), 30, bottom: true);
            var cardBar = CreateCardBar(canvasGo.transform);
            var unitActions = CreateUnitActionPanel(canvasGo.transform);
            var speedControls = CreateSpeedControls(canvasGo.transform);

            // 판이 끝나면 뜨는 창들은 맨 나중에 만들어 다른 UI 위에 그린다.
            var result = CreateResultPanel(canvasGo.transform);
            var stageSelect = CreateStageSelectPanel(canvasGo.transform);
            var formation = CreateFormationPanel(canvasGo.transform);
            var gacha = CreateGachaPanel(canvasGo.transform);

            var hud = canvasGo.AddComponent<BattleHud>();
            var hudSo = new SerializedObject(hud);
            hudSo.FindProperty("_defenseHpLabel").objectReferenceValue = defenseHp;
            hudSo.FindProperty("_damageLabel").objectReferenceValue = damage;
            hudSo.FindProperty("_costLabel").objectReferenceValue = cost;
            hudSo.FindProperty("_messageLabel").objectReferenceValue = message;
            hudSo.ApplyModifiedPropertiesWithoutUndo();

            var rootGo = new GameObject("GameRoot", typeof(GameRoot));
            var so = new SerializedObject(rootGo.GetComponent<GameRoot>());
            so.FindProperty("_textTable").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(TextTablePath);
            so.FindProperty("_font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            // NewScene이 메모리의 에셋을 내리므로 데이터 에셋은 씬을 만든 뒤에 불러온다.
            so.FindProperty("_balance").objectReferenceValue = AssetDatabase.LoadAssetAtPath<BattleBalanceAsset>(BalancePath);
            so.FindProperty("_catalog").objectReferenceValue = AssetDatabase.LoadAssetAtPath<StageCatalogAsset>(CatalogPath);
            so.FindProperty("_view").objectReferenceValue = AssetDatabase.LoadAssetAtPath<BattleViewAsset>(ViewPath);
            so.FindProperty("_mapView").objectReferenceValue = mapView;
            so.FindProperty("_cameraRig").objectReferenceValue = cameraRig;
            so.FindProperty("_enemyView").objectReferenceValue = enemyView;
            so.FindProperty("_unitView").objectReferenceValue = unitView;
            so.FindProperty("_cardBar").objectReferenceValue = cardBar;
            so.FindProperty("_placement").objectReferenceValue = placement;
            so.FindProperty("_damageNumbers").objectReferenceValue = damageNumbers;
            so.FindProperty("_unitActions").objectReferenceValue = unitActions;
            so.FindProperty("_speedControls").objectReferenceValue = speedControls;
            so.FindProperty("_result").objectReferenceValue = result;
            so.FindProperty("_stageSelect").objectReferenceValue = stageSelect;
            so.FindProperty("_formation").objectReferenceValue = formation;
            so.FindProperty("_gacha").objectReferenceValue = gacha;
            so.FindProperty("_hud").objectReferenceValue = hud;
            so.FindProperty("_titleLabel").objectReferenceValue = title;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        /// <summary>능력자 머리 위에 뜨는 퇴각 버튼. 위치와 글은 런타임에 UnitActionPanel이 정한다.</summary>
        private static UnitActionPanel CreateUnitActionPanel(Transform parent)
        {
            var root = new GameObject("UnitActions", typeof(RectTransform), typeof(UnitActionPanel));
            root.transform.SetParent(parent, false);
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = rootRect.anchorMax = Vector2.zero;
            rootRect.sizeDelta = Vector2.zero;

            // 버튼 둘(퇴각 · 스킬)을 가로로 놓는 판. 판 자체는 클릭을 받지 않는다.
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            panel.transform.SetParent(root.transform, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = Vector2.zero;
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.sizeDelta = new Vector2(440f, 56f);
            var layout = panel.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            var retreat = CreateButton(panel.transform, "RetreatButton", out var retreatLabel);
            var skill = CreateButton(panel.transform, "SkillButton", out var skillLabel);

            var actions = root.GetComponent<UnitActionPanel>();
            var so = new SerializedObject(actions);
            so.FindProperty("_panel").objectReferenceValue = panelRect;
            so.FindProperty("_retreatButton").objectReferenceValue = retreat;
            so.FindProperty("_retreatLabel").objectReferenceValue = retreatLabel;
            so.FindProperty("_skillButton").objectReferenceValue = skill;
            so.FindProperty("_skillLabel").objectReferenceValue = skillLabel;
            so.ApplyModifiedPropertiesWithoutUndo();
            return actions;
        }

        /// <summary>
        /// 결과 창: 화면을 어둡게 덮고 가운데 상자에 제목 · 별 셋 · 버튼 셋. 레이아웃 그룹 없이 절대 위치라 씬을 열어도 값이 바뀌지 않는다.
        /// </summary>
        private static ResultPanel CreateResultPanel(Transform parent)
        {
            var host = new GameObject("Result", typeof(RectTransform), typeof(ResultPanel));
            host.transform.SetParent(parent, false);
            Stretch(host.GetComponent<RectTransform>());

            var root = CreateDim(host.transform, "Root");
            var box = CreateBox(root.transform, "Box", new Vector2(620f, 380f));

            var title = CreateText(box, "Title", new Vector2(0f, 120f), new Vector2(560f, 90f), 60);
            var stars = new Image[3];
            for (var i = 0; i < stars.Length; i++)
            {
                var star = new GameObject("Star" + (i + 1), typeof(RectTransform), typeof(Image));
                star.transform.SetParent(box, false);
                var rect = star.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(44f, 44f);
                rect.anchoredPosition = new Vector2((i - 1) * 80f, 20f);
                rect.localRotation = Quaternion.Euler(0f, 0f, 45f);
                stars[i] = star.GetComponent<Image>();
            }

            var reward = CreateText(box, "Reward", new Vector2(0f, -45f), new Vector2(560f, 44f), 30);
            var retry = CreateButtonAt(box, "RetryButton", new Vector2(-195f, -110f), new Vector2(180f, 60f), out var retryLabel);
            var next = CreateButtonAt(box, "NextButton", new Vector2(0f, -110f), new Vector2(180f, 60f), out var nextLabel);
            var select = CreateButtonAt(box, "SelectButton", new Vector2(195f, -110f), new Vector2(180f, 60f), out var selectLabel);

            var panel = host.GetComponent<ResultPanel>();
            var so = new SerializedObject(panel);
            so.FindProperty("_root").objectReferenceValue = root;
            so.FindProperty("_titleLabel").objectReferenceValue = title;
            var starList = so.FindProperty("_stars");
            starList.arraySize = stars.Length;
            for (var i = 0; i < stars.Length; i++) starList.GetArrayElementAtIndex(i).objectReferenceValue = stars[i];
            so.FindProperty("_rewardLabel").objectReferenceValue = reward;
            so.FindProperty("_retryButton").objectReferenceValue = retry;
            so.FindProperty("_retryLabel").objectReferenceValue = retryLabel;
            so.FindProperty("_nextButton").objectReferenceValue = next;
            so.FindProperty("_nextLabel").objectReferenceValue = nextLabel;
            so.FindProperty("_selectButton").objectReferenceValue = select;
            so.FindProperty("_selectLabel").objectReferenceValue = selectLabel;
            so.ApplyModifiedPropertiesWithoutUndo();
            return panel;
        }

        /// <summary>
        /// 스테이지 선택 창. 목록 칸에만 세로 레이아웃 그룹을 두고, 스테이지 버튼은 런타임에 닫기 버튼을 복제해 만든다 (씬에는 저장되지 않음).
        /// </summary>
        private static StageSelectPanel CreateStageSelectPanel(Transform parent)
        {
            var host = new GameObject("StageSelect", typeof(RectTransform), typeof(StageSelectPanel));
            host.transform.SetParent(parent, false);
            Stretch(host.GetComponent<RectTransform>());

            var root = CreateDim(host.transform, "Root");
            var box = CreateBox(root.transform, "Box", new Vector2(520f, 520f));
            var title = CreateText(box, "Title", new Vector2(0f, 205f), new Vector2(460f, 70f), 44);

            var list = new GameObject("List", typeof(RectTransform), typeof(VerticalLayoutGroup));
            list.transform.SetParent(box, false);
            var listRect = list.GetComponent<RectTransform>();
            listRect.anchorMin = listRect.anchorMax = new Vector2(0.5f, 0.5f);
            listRect.sizeDelta = new Vector2(420f, 280f);
            listRect.anchoredPosition = new Vector2(0f, 10f);
            var layout = list.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var close = CreateButtonAt(box, "CloseButton", new Vector2(0f, -200f), new Vector2(200f, 60f), out var closeLabel);
            var element = close.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 60f;   // 복제한 스테이지 버튼이 목록에서 쓰는 높이

            var panel = host.GetComponent<StageSelectPanel>();
            var so = new SerializedObject(panel);
            so.FindProperty("_root").objectReferenceValue = root;
            so.FindProperty("_titleLabel").objectReferenceValue = title;
            so.FindProperty("_list").objectReferenceValue = listRect;
            so.FindProperty("_closeButton").objectReferenceValue = close;
            so.FindProperty("_closeLabel").objectReferenceValue = closeLabel;
            so.ApplyModifiedPropertiesWithoutUndo();
            return panel;
        }

        /// <summary>
        /// 편성 창: 왼쪽 능력자 목록(카드는 런타임에 템플릿을 복제), 오른쪽 정보 탭과 범위 그림, 아래 출전 버튼.
        /// 레이아웃 그룹 없이 절대 위치라 씬을 열어도 값이 바뀌지 않는다.
        /// </summary>
        private static FormationPanel CreateFormationPanel(Transform parent)
        {
            var host = new GameObject("Formation", typeof(RectTransform), typeof(FormationPanel));
            host.transform.SetParent(parent, false);
            Stretch(host.GetComponent<RectTransform>());

            var root = CreateDim(host.transform, "Root");
            var box = CreateBox(root.transform, "Box", new Vector2(1500f, 820f));
            var title = CreateText(box, "Title", new Vector2(0f, 360f), new Vector2(600f, 70f), 48);
            var count = CreateText(box, "Count", new Vector2(-430f, 290f), new Vector2(560f, 50f), 30);

            // 능력자 목록: 세로로 스크롤되는 칸 (능력자가 많으면 창을 넘으므로). 카드는 Content 아래에 런타임에 놓는다.
            var viewportGo = new GameObject("Roster", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewportGo.transform.SetParent(box, false);
            var viewport = viewportGo.GetComponent<RectTransform>();
            viewport.anchorMin = viewport.anchorMax = new Vector2(0.5f, 0.5f);
            viewport.pivot = new Vector2(0f, 1f);
            viewport.sizeDelta = new Vector2(620f, 560f);
            viewport.anchoredPosition = new Vector2(-710f, 250f);
            viewportGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);   // 휠 입력을 받는 투명 판

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewport, false);
            var roster = contentGo.GetComponent<RectTransform>();
            roster.anchorMin = roster.anchorMax = new Vector2(0f, 1f);
            roster.pivot = new Vector2(0f, 1f);
            roster.sizeDelta = new Vector2(620f, 560f);
            roster.anchoredPosition = Vector2.zero;

            var scroll = viewportGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            scroll.viewport = viewport;
            scroll.content = roster;
            var template = CreateButtonAt(roster, "RosterTemplate", Vector2.zero, new Vector2(300f, 140f), out _);

            var unitTab = CreateButtonAt(box, "UnitTab", new Vector2(110f, 290f), new Vector2(200f, 56f), out var unitTabLabel);
            var enemyTab = CreateButtonAt(box, "EnemyTab", new Vector2(330f, 290f), new Vector2(200f, 56f), out var enemyTabLabel);

            var info = CreateText(box, "Info", new Vector2(360f, 30f), new Vector2(720f, 420f), 26);
            info.alignment = TextAlignmentOptions.TopLeft;

            var rangeGo = new GameObject("Range", typeof(RectTransform));
            rangeGo.transform.SetParent(box, false);
            var range = rangeGo.GetComponent<RectTransform>();
            range.anchorMin = range.anchorMax = new Vector2(0.5f, 0.5f);
            range.sizeDelta = new Vector2(400f, 140f);
            range.anchoredPosition = new Vector2(360f, -260f);

            var confirm = CreateButtonAt(box, "Confirm", new Vector2(0f, -355f), new Vector2(260f, 64f), out var confirmLabel);
            var gachaButton = CreateButtonAt(box, "GachaButton", new Vector2(-620f, 360f), new Vector2(200f, 56f), out var gachaLabel);

            var panel = host.GetComponent<FormationPanel>();
            var so = new SerializedObject(panel);
            so.FindProperty("_root").objectReferenceValue = root;
            so.FindProperty("_titleLabel").objectReferenceValue = title;
            so.FindProperty("_countLabel").objectReferenceValue = count;
            so.FindProperty("_rosterArea").objectReferenceValue = roster;
            so.FindProperty("_rosterTemplate").objectReferenceValue = template;
            so.FindProperty("_unitTabButton").objectReferenceValue = unitTab;
            so.FindProperty("_unitTabLabel").objectReferenceValue = unitTabLabel;
            so.FindProperty("_enemyTabButton").objectReferenceValue = enemyTab;
            so.FindProperty("_enemyTabLabel").objectReferenceValue = enemyTabLabel;
            so.FindProperty("_infoLabel").objectReferenceValue = info;
            so.FindProperty("_rangeArea").objectReferenceValue = range;
            so.FindProperty("_confirmButton").objectReferenceValue = confirm;
            so.FindProperty("_confirmLabel").objectReferenceValue = confirmLabel;
            so.FindProperty("_gachaButton").objectReferenceValue = gachaButton;
            so.FindProperty("_gachaLabel").objectReferenceValue = gachaLabel;
            so.ApplyModifiedPropertiesWithoutUndo();
            return panel;
        }

        /// <summary>
        /// 뽑기 창. 창(별가루 · 확률 · 1회 · 10회 · 닫기) 위에 결과 연출 판(누르면 건너뛰기 · 계속)이 덮인다.
        /// 결과 카드는 런타임에 카드 틀을 복제해 만든다 (씬에는 저장되지 않음).
        /// </summary>
        private static GachaPanel CreateGachaPanel(Transform parent)
        {
            var host = new GameObject("Gacha", typeof(RectTransform), typeof(GachaPanel));
            host.transform.SetParent(parent, false);
            Stretch(host.GetComponent<RectTransform>());

            var root = CreateDim(host.transform, "Root");
            var box = CreateBox(root.transform, "Box", new Vector2(900f, 560f));
            var title = CreateText(box, "Title", new Vector2(0f, 220f), new Vector2(600f, 70f), 48);
            var currency = CreateText(box, "Currency", new Vector2(0f, 140f), new Vector2(800f, 50f), 32);
            var rates = CreateText(box, "Rates", new Vector2(0f, 85f), new Vector2(800f, 40f), 24);
            var single = CreateButtonAt(box, "SingleButton", new Vector2(-200f, -20f), new Vector2(340f, 84f), out var singleLabel);
            var multi = CreateButtonAt(box, "MultiButton", new Vector2(200f, -20f), new Vector2(340f, 84f), out var multiLabel);
            var message = CreateText(box, "Message", new Vector2(0f, -110f), new Vector2(800f, 40f), 26);
            var close = CreateButtonAt(box, "CloseButton", new Vector2(0f, -210f), new Vector2(220f, 60f), out var closeLabel);

            // 결과 연출 판: 화면 전체를 덮는 버튼 (누르면 건너뛰기 · 계속).
            var revealGo = new GameObject("Reveal", typeof(RectTransform), typeof(Image), typeof(Button));
            revealGo.transform.SetParent(root.transform, false);
            Stretch(revealGo.GetComponent<RectTransform>());
            // 불투명: 리니어 색 공간에서는 반투명 판이 생각보다 옅어 뒤 창 글이 비친다.
            revealGo.GetComponent<Image>().color = new Color(0.02f, 0.02f, 0.04f, 1f);
            var reveal = revealGo.GetComponent<Button>();
            reveal.transition = Selectable.Transition.None;

            var lightGo = new GameObject("Light", typeof(RectTransform), typeof(Image));
            lightGo.transform.SetParent(revealGo.transform, false);
            var light = lightGo.GetComponent<RectTransform>();
            light.anchorMin = light.anchorMax = new Vector2(0.5f, 0.5f);
            light.sizeDelta = new Vector2(360f, 360f);
            light.localRotation = Quaternion.Euler(0f, 0f, 45f);
            lightGo.GetComponent<Image>().raycastTarget = false;

            var cardAreaGo = new GameObject("Cards", typeof(RectTransform));
            cardAreaGo.transform.SetParent(revealGo.transform, false);
            var cardArea = cardAreaGo.GetComponent<RectTransform>();
            cardArea.anchorMin = cardArea.anchorMax = new Vector2(0.5f, 0.5f);
            cardArea.sizeDelta = Vector2.zero;
            cardArea.anchoredPosition = new Vector2(0f, 30f);
            var card = CreateGachaCard(cardArea);

            var flashGo = new GameObject("Flash", typeof(RectTransform), typeof(Image));
            flashGo.transform.SetParent(revealGo.transform, false);
            Stretch(flashGo.GetComponent<RectTransform>());
            var flash = flashGo.GetComponent<Image>();
            flash.color = new Color(1f, 1f, 1f, 0f);
            flash.raycastTarget = false;

            var hint = CreateText(revealGo.transform, "Hint", new Vector2(0f, -470f), new Vector2(800f, 44f), 28);

            var panel = host.GetComponent<GachaPanel>();
            var so = new SerializedObject(panel);
            so.FindProperty("_root").objectReferenceValue = root;
            so.FindProperty("_titleLabel").objectReferenceValue = title;
            so.FindProperty("_currencyLabel").objectReferenceValue = currency;
            so.FindProperty("_ratesLabel").objectReferenceValue = rates;
            so.FindProperty("_messageLabel").objectReferenceValue = message;
            so.FindProperty("_singleButton").objectReferenceValue = single;
            so.FindProperty("_singleLabel").objectReferenceValue = singleLabel;
            so.FindProperty("_multiButton").objectReferenceValue = multi;
            so.FindProperty("_multiLabel").objectReferenceValue = multiLabel;
            so.FindProperty("_closeButton").objectReferenceValue = close;
            so.FindProperty("_closeLabel").objectReferenceValue = closeLabel;
            so.FindProperty("_reveal").objectReferenceValue = reveal;
            so.FindProperty("_light").objectReferenceValue = light;
            so.FindProperty("_flash").objectReferenceValue = flash;
            so.FindProperty("_cardArea").objectReferenceValue = cardArea;
            so.FindProperty("_cardTemplate").objectReferenceValue = card;
            so.FindProperty("_hintLabel").objectReferenceValue = hint;
            so.ApplyModifiedPropertiesWithoutUndo();
            return panel;
        }

        /// <summary>뽑기 결과 카드 틀: 테두리(등급 색) 안에 바탕, 그 위에 앞면(등급 · 얼굴 · 이름 · 새로 얻음/돌려받음).</summary>
        private static GachaCard CreateGachaCard(Transform parent)
        {
            var go = new GameObject("CardTemplate", typeof(RectTransform), typeof(Image), typeof(GachaCard));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(200f, 280f);
            go.GetComponent<Image>().raycastTarget = false;

            var innerGo = new GameObject("Inner", typeof(RectTransform), typeof(Image));
            innerGo.transform.SetParent(go.transform, false);
            var inner = innerGo.GetComponent<RectTransform>();
            inner.anchorMin = Vector2.zero;
            inner.anchorMax = Vector2.one;
            inner.offsetMin = new Vector2(6f, 6f);
            inner.offsetMax = new Vector2(-6f, -6f);
            innerGo.GetComponent<Image>().raycastTarget = false;

            var faceGo = new GameObject("Face", typeof(RectTransform));
            faceGo.transform.SetParent(go.transform, false);
            Stretch(faceGo.GetComponent<RectTransform>());

            var rarity = CreateText(faceGo.transform, "Rarity", new Vector2(0f, 112f), new Vector2(180f, 34f), 26);
            var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            portraitGo.transform.SetParent(faceGo.transform, false);
            var portrait = portraitGo.GetComponent<RectTransform>();
            portrait.anchorMin = portrait.anchorMax = new Vector2(0.5f, 0.5f);
            portrait.sizeDelta = new Vector2(160f, 160f);
            portrait.anchoredPosition = new Vector2(0f, 18f);
            portraitGo.GetComponent<Image>().raycastTarget = false;
            portraitGo.GetComponent<Image>().preserveAspect = true;
            var name = CreateText(faceGo.transform, "Name", new Vector2(0f, -82f), new Vector2(190f, 34f), 22);
            var tag = CreateText(faceGo.transform, "Tag", new Vector2(0f, -115f), new Vector2(190f, 30f), 20);

            var card = go.GetComponent<GachaCard>();
            var so = new SerializedObject(card);
            so.FindProperty("_frame").objectReferenceValue = go.GetComponent<Image>();
            so.FindProperty("_inner").objectReferenceValue = innerGo.GetComponent<Image>();
            so.FindProperty("_face").objectReferenceValue = faceGo;
            so.FindProperty("_portrait").objectReferenceValue = portraitGo.GetComponent<Image>();
            so.FindProperty("_nameLabel").objectReferenceValue = name;
            so.FindProperty("_rarityLabel").objectReferenceValue = rarity;
            so.FindProperty("_tagLabel").objectReferenceValue = tag;
            so.ApplyModifiedPropertiesWithoutUndo();
            return card;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        /// <summary>화면 전체를 어둡게 덮어 뒤의 클릭을 막는 판.</summary>
        private static GameObject CreateDim(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>());
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
            return go;
        }

        private static RectTransform CreateBox(Transform parent, string name, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(0.10f, 0.11f, 0.13f, 0.97f);
            return rect;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, Vector2 position, Vector2 size, float fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            var label = go.GetComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = fontSize;
            label.color = Color.white;
            label.raycastTarget = false;
            PinTextStyle(label);
            return label;
        }

        /// <summary>CreateButton을 레이아웃 없이 가운데 기준 절대 위치 · 크기로 놓는다.</summary>
        private static Button CreateButtonAt(Transform parent, string name, Vector2 position, Vector2 size, out TextMeshProUGUI label)
        {
            var button = CreateButton(parent, name, out label);
            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return button;
        }

        /// <summary>오른쪽 위 일시정지 · 배속 버튼.</summary>
        private static SpeedControls CreateSpeedControls(Transform parent)
        {
            var go = new GameObject("SpeedControls", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(SpeedControls));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-24f, -24f);
            rect.sizeDelta = new Vector2(330f, 56f);
            var layout = go.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            var pause = CreateButton(go.transform, "PauseButton", out var pauseLabel);
            var speed = CreateButton(go.transform, "SpeedButton", out var speedLabel);

            var controls = go.GetComponent<SpeedControls>();
            var so = new SerializedObject(controls);
            so.FindProperty("_pauseButton").objectReferenceValue = pause;
            so.FindProperty("_pauseLabel").objectReferenceValue = pauseLabel;
            so.FindProperty("_speedButton").objectReferenceValue = speed;
            so.FindProperty("_speedLabel").objectReferenceValue = speedLabel;
            so.ApplyModifiedPropertiesWithoutUndo();
            return controls;
        }

        /// <summary>어두운 판 + 흰 글의 버튼. 크기는 부모 레이아웃이 정한다.</summary>
        private static Button CreateButton(Transform parent, string name, out TextMeshProUGUI label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.14f, 0.92f);

            // 레이아웃 그룹이 정하는 값은 Unity가 앵커 0 · 크기 0으로 저장한다. 미리 같게 두어 씬을 열 때 바뀌지 않게 한다.
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.sizeDelta = Vector2.zero;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(go.transform, false);
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            label = labelGo.GetComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 26;
            label.color = Color.white;
            label.raycastTarget = false;
            PinTextStyle(label);
            return go.GetComponent<Button>();
        }

        private static CardBar CreateCardBar(Transform parent)
        {
            var go = new GameObject("CardBar", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(CardBar));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 20f);
            rect.sizeDelta = new Vector2(0f, 200f);

            var layout = go.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.LowerCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return go.GetComponent<CardBar>();
        }

        /// <summary>TMP는 처음 읽을 때 기본 스타일 해시를 채워 씬을 바꾼다. 미리 같은 값을 넣어 둔다.</summary>
        private static void PinTextStyle(TMP_Text label) => label.textStyle = TMP_Style.NormalStyle;

        private static TextMeshProUGUI CreateLabel(Transform parent, string name, Vector2 position, float size, bool bottom = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, bottom ? 0f : 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(1600, 80);

            var label = go.GetComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = size;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            PinTextStyle(label);
            return label;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            EnsureFolderChain(path);
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void FillIfZero(SerializedProperty property, int value)
        {
            if (property.intValue == 0) property.intValue = value;
        }

        private static void FillIfZero(SerializedProperty property, float value)
        {
            if (Mathf.Approximately(property.floatValue, 0f)) property.floatValue = value;
        }

        private static void EnsureFolderChain(string assetPath)
        {
            var folder = assetPath.Substring(0, assetPath.LastIndexOf('/'));
            if (AssetDatabase.IsValidFolder(folder)) return;

            EnsureFolderChain(folder);
            var slash = folder.LastIndexOf('/');
            AssetDatabase.CreateFolder(folder.Substring(0, slash), folder.Substring(slash + 1));
        }
    }
}
