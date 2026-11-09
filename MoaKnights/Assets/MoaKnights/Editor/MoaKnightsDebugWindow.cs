using MoaKnights.Game;
using UnityEditor;
using UnityEngine;

namespace MoaKnights.EditorTools
{
    /// <summary>개발자 디버그 창. 시스템이 생길 때마다 섹션을 하나 더한다. 빌드에 들어가지 않는다.</summary>
    public sealed class MoaKnightsDebugWindow : EditorWindow
    {
        private Vector2 _scroll;

        [MenuItem("MoaKnights/Debug/Debug Window")]
        private static void Open() => GetWindow<MoaKnightsDebugWindow>("MoaKnights Debug");

        private void OnInspectorUpdate() => Repaint();

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawRunSection();
            EditorGUILayout.EndScrollView();
        }

        private static void DrawRunSection()
        {
            EditorGUILayout.LabelField("판 (Run)", EditorStyles.boldLabel);

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("플레이 모드에서 사용합니다.", MessageType.Info);
                return;
            }

            var root = FindAnyObjectByType<GameRoot>();
            if (root == null || root.Run == null)
            {
                EditorGUILayout.HelpBox("이 씬에 GameRoot가 없습니다.", MessageType.Warning);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("시드", root.Run.Seed.ToString());
            if (GUILayout.Button("복사", GUILayout.Width(50)))
                EditorGUIUtility.systemCopyBuffer = root.Run.Seed.ToString();
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("[상태 변경] 재시작 (같은 시드)")) root.DebugRestart(keepSeed: true);
            if (GUILayout.Button("[상태 변경] 재시작 (새 시드)")) root.DebugRestart(keepSeed: false);
        }
    }
}
