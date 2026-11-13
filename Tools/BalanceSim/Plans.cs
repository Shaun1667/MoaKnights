using System.Collections.Generic;

/// <summary>실험용 등장 표 (스테이지 번호 → 그룹). 비어 있으면 에셋 그대로. `run.sh proposed`로 이 표를 쓴다.</summary>
internal static class Plans
{
    private static SpawnRow G(string enemy, int route, float start, float interval, int count) => new SpawnRow(enemy, route, start, interval, count);

    public static readonly Dictionary<int, SpawnRow[]> Tables = new Dictionary<int, SpawnRow[]>
    {
        // 예: { 4, new[] { G("dizes_01", 0, 0, 2.5f, 12), G("dizes_shell", 0, 15, 6, 4) } },
    };
}
