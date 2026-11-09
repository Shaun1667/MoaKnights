using MoaKnights.Core.Map;
using MoaKnights.Game;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
            so.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private static StageAsset EnsureStage()
        {
            var existing = AssetDatabase.LoadAssetAtPath<StageAsset>(StagePath);
            if (existing != null) return existing;

            var asset = LoadOrCreate<StageAsset>(StagePath);
            var so = new SerializedObject(asset);
            so.FindProperty("_id").stringValue = "stage_01";
            var rows = so.FindProperty("_mapRows");
            rows.arraySize = Stage01Rows.Length;
            for (var i = 0; i < Stage01Rows.Length; i++)
                rows.GetArrayElementAtIndex(i).stringValue = Stage01Rows[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private static BattleViewAsset EnsureView()
        {
            var existing = AssetDatabase.LoadAssetAtPath<BattleViewAsset>(ViewPath);
            if (existing != null) return existing;

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

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var title = CreateLabel(canvasGo.transform, "TitleLabel", new Vector2(0, -50), 48);
            var status = CreateLabel(canvasGo.transform, "StatusLabel", new Vector2(0, -110), 28);

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
            so.FindProperty("_titleLabel").objectReferenceValue = title;
            so.FindProperty("_statusLabel").objectReferenceValue = status;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private static TextMeshProUGUI CreateLabel(Transform parent, string name, Vector2 position, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
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
