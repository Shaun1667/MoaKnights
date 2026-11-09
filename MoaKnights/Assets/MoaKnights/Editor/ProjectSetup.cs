using MoaKnights.Game;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MoaKnights.EditorTools
{
    /// <summary>에셋 · 씬 생성 한 곳. 몇 번 실행해도 같은 결과가 나온다.</summary>
    public static class ProjectSetup
    {
        private const string Root = "Assets/MoaKnights";
        private const string BalancePath = Root + "/Data/Balance/BattleBalance.asset";
        private const string TextTablePath = Root + "/Data/Text/text.csv";
        private const string FontPath = Root + "/Fonts/NotoSansKR-VF.ttf";
        private const string ScenePath = Root + "/Scenes/Battle.unity";

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

            var balance = EnsureBalance();
            BuildBattleScene(balance);
            AssetDatabase.SaveAssets();
            Debug.Log("[Setup] Done: " + ScenePath);
        }

        private static BattleBalanceAsset EnsureBalance()
        {
            EnsureFolder(Root + "/Data");
            EnsureFolder(Root + "/Data/Balance");

            var asset = AssetDatabase.LoadAssetAtPath<BattleBalanceAsset>(BalancePath);
            if (asset != null) return asset;

            // 첫 생성 때만 BALANCE.md 1번 값을 넣는다. 이후 값은 에셋에서 고친다.
            asset = ScriptableObject.CreateInstance<BattleBalanceAsset>();
            AssetDatabase.CreateAsset(asset, BalancePath);
            var so = new SerializedObject(asset);
            so.FindProperty("_startCost").intValue = 20;
            so.FindProperty("_costRegenSeconds").floatValue = 2f;
            so.FindProperty("_maxCost").intValue = 99;
            so.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private static void BuildBattleScene(BattleBalanceAsset balance)
        {
            EnsureFolder(Root + "/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var title = CreateLabel(canvasGo.transform, "TitleLabel", new Vector2(0, 80), 96);
            var status = CreateLabel(canvasGo.transform, "StatusLabel", new Vector2(0, -60), 48);

            var rootGo = new GameObject("GameRoot", typeof(GameRoot));
            var so = new SerializedObject(rootGo.GetComponent<GameRoot>());
            so.FindProperty("_textTable").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(TextTablePath);
            so.FindProperty("_font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            so.FindProperty("_balance").objectReferenceValue = balance;
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
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(1600, 160);

            var label = go.GetComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = size;
            label.textWrappingMode = TextWrappingModes.Normal;
            return label;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
