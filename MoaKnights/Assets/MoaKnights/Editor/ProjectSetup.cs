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
        private const string StagePath = Root + "/Data/Stages/Stage01.asset";
        private const string ViewPath = Root + "/Data/View/BattleView.asset";
        private const string GreyboxMaterialPath = Root + "/Art/Greybox/Greybox.mat";
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
        private const string MeleeUnitId = "melee_01";
        private const string RangedUnitId = "ranged_01";

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
            EnsureStage();
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
            FillIfZero(so.FindProperty("_startCost"), 20);
            FillIfZero(so.FindProperty("_costRegenSeconds"), 2f);
            FillIfZero(so.FindProperty("_maxCost"), 99);
            FillIfZero(so.FindProperty("_ticksPerSecond"), 30);
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

            // 중첩 · 퇴각 · 재배치 (BALANCE 6번). 비어 있을 때만 넣는다.
            FillIfEmpty(so.FindProperty("_stackHpPercents"), 100, 130, 160);
            FillIfEmpty(so.FindProperty("_stackAttackPercents"), 100, 130, 160);
            FillIfEmpty(so.FindProperty("_stackBlockBonus"), 0, 0, 1);
            FillIfZero(so.FindProperty("_stackCostPercent"), 100);
            FillIfZero(so.FindProperty("_redeployCooldownSeconds"), 10f);
            FillIfZero(so.FindProperty("_redeployCostIncrease"), 5);
            FillIfZero(so.FindProperty("_retreatRefundPercent"), 50);

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

        private static StageAsset EnsureStage()
        {
            var asset = LoadOrCreate<StageAsset>(StagePath);
            var so = new SerializedObject(asset);

            var id = so.FindProperty("_id");
            if (string.IsNullOrEmpty(id.stringValue)) id.stringValue = "stage_01";

            var rows = so.FindProperty("_mapRows");
            if (rows.arraySize == 0)
            {
                rows.arraySize = Stage01Rows.Length;
                for (var i = 0; i < Stage01Rows.Length; i++)
                    rows.GetArrayElementAtIndex(i).stringValue = Stage01Rows[i];
            }

            var routes = so.FindProperty("_routes");
            if (routes.arraySize == 0)
            {
                routes.arraySize = 1;
                routes.GetArrayElementAtIndex(0).stringValue = Stage01Route;
            }

            var groups = so.FindProperty("_spawnGroups");
            if (groups.arraySize == 0)
            {
                groups.arraySize = 1;
                var group = groups.GetArrayElementAtIndex(0);
                group.FindPropertyRelative("EnemyId").stringValue = FirstEnemyId;
                group.FindPropertyRelative("RouteIndex").intValue = 0;
                group.FindPropertyRelative("StartSeconds").floatValue = 0f;
                group.FindPropertyRelative("IntervalSeconds").floatValue = 2f;
                group.FindPropertyRelative("Count").intValue = 10;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return asset;
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
            FillIfZero(so.FindProperty("_cameraBottomReserve"), 0.22f);
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
            so.FindProperty("_cameraPitch").floatValue = 50f;
            so.FindProperty("_cameraYaw").floatValue = 45f;
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
            so.FindProperty("_stage").objectReferenceValue = AssetDatabase.LoadAssetAtPath<StageAsset>(StagePath);
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
