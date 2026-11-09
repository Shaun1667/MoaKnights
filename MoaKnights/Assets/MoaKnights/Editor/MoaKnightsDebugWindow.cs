using MoaKnights.Core.Map;
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
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("플레이 모드에서 사용합니다.", MessageType.Info);
                return;
            }

            var root = FindAnyObjectByType<GameRoot>();
            if (root == null || root.Simulation == null)
            {
                EditorGUILayout.HelpBox("이 씬에 GameRoot가 없습니다.", MessageType.Warning);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawRunSection(root);
            EditorGUILayout.Space();
            DrawClockSection(root);
            EditorGUILayout.Space();
            DrawMapSection(root);
            EditorGUILayout.Space();
            DrawEnemySection(root);
            EditorGUILayout.EndScrollView();
        }

        private static void DrawRunSection(GameRoot root)
        {
            EditorGUILayout.LabelField("판 (Run)", EditorStyles.boldLabel);
            var seed = root.Simulation.World.Seed;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("시드", seed.ToString());
            if (GUILayout.Button("복사", GUILayout.Width(50))) EditorGUIUtility.systemCopyBuffer = seed.ToString();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("스테이지", root.Simulation.World.StageId);
            if (GUILayout.Button("[상태 변경] 재시작 (같은 시드)")) root.DebugRestart(keepSeed: true);
            if (GUILayout.Button("[상태 변경] 재시작 (새 시드)")) root.DebugRestart(keepSeed: false);
        }

        private static void DrawClockSection(GameRoot root)
        {
            EditorGUILayout.LabelField("틱 (Clock)", EditorStyles.boldLabel);
            var clock = root.Clock;
            var tick = root.Simulation.World.Tick;
            var tps = root.Balance.TicksPerSecond;

            EditorGUILayout.LabelField("틱", tick + "  (" + (tick / (float)tps).ToString("0.0") + "초)");
            EditorGUILayout.LabelField("초당 틱 / 배속", tps + " / x" + clock.Speed);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(clock.Paused ? "[상태 변경] 계속" : "[상태 변경] 일시정지")) root.DebugSetPaused(!clock.Paused);
            if (GUILayout.Button("[상태 변경] 1틱")) root.DebugStepTicks(1);
            if (GUILayout.Button("[상태 변경] 1초")) root.DebugStepTicks(tps);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("[상태 변경] x1")) root.DebugSetSpeed(1);
            if (GUILayout.Button("[상태 변경] x2")) root.DebugSetSpeed(2);
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawMapSection(GameRoot root)
        {
            EditorGUILayout.LabelField("맵 (Map)", EditorStyles.boldLabel);
            var map = root.Simulation.Map;
            EditorGUILayout.LabelField("크기", map.Width + " x " + map.Height);
            EditorGUILayout.LabelField("지상 / 언덕 / 막힘",
                map.Count(CellType.Ground) + " / " + map.Count(CellType.Hill) + " / " + map.Count(CellType.Blocked));
            EditorGUILayout.LabelField("입구 / 출구", string.Join(" ", map.Entrances) + " / " + string.Join(" ", map.Exits));
        }

        private static void DrawEnemySection(GameRoot root)
        {
            EditorGUILayout.LabelField("적 · 디펜스 (Enemy)", EditorStyles.boldLabel);
            var world = root.Simulation.World;
            var definition = root.Simulation.Definition;

            EditorGUILayout.LabelField("디펜스 HP", world.DefenseHp + " / " + definition.DefenseHp);
            EditorGUILayout.LabelField("경로 수", definition.Routes.Count.ToString());
            EditorGUILayout.LabelField("나온 적 / 전체", world.NextSpawnIndex + " / " + definition.Spawns.Count);
            EditorGUILayout.LabelField("살아 있는 적", world.Enemies.Count.ToString());

            foreach (var enemy in world.Enemies)
            {
                var route = definition.Routes[enemy.RouteIndex];
                var cells = enemy.Progress / (float)definition.UnitsPerCell;
                EditorGUILayout.LabelField("#" + enemy.Id + " " + enemy.DefinitionId,
                    "경로 " + enemy.RouteIndex + "  " + cells.ToString("0.0") + " / " + route.Length + "칸");
            }
        }
    }
}
