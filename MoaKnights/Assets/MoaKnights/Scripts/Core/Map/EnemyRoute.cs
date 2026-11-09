using System;
using System.Collections.Generic;
using System.Globalization;

namespace MoaKnights.Core.Map
{
    /// <summary>
    /// 적이 지나가는 길. 꺾이는 칸을 "6,3 3,3 3,1"처럼 적으면 사이 칸을 채운다.
    /// 입구에서 시작해 출구에서 끝나고, 사이는 지상 칸만 지난다. 대각선은 받지 않는다.
    /// </summary>
    public sealed class EnemyRoute
    {
        private readonly List<GridPos> _cells;

        public IReadOnlyList<GridPos> Cells => _cells;

        /// <summary>지나는 구간 수. 칸 수보다 하나 적다.</summary>
        public int Length => _cells.Count - 1;

        private EnemyRoute(List<GridPos> cells)
        {
            _cells = cells;
        }

        public static EnemyRoute Parse(GridMap map, string corners)
        {
            if (string.IsNullOrWhiteSpace(corners)) throw new FormatException("Route is empty.");

            var parts = corners.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) throw new FormatException("Route needs at least two corners: " + corners);

            var cells = new List<GridPos>();
            var previous = ParseCorner(parts[0], corners);
            cells.Add(previous);

            for (var i = 1; i < parts.Length; i++)
            {
                var corner = ParseCorner(parts[i], corners);
                AppendSegment(cells, previous, corner, corners);
                previous = corner;
            }

            Validate(map, cells, corners);
            return new EnemyRoute(cells);
        }

        /// <summary>진행도가 어느 두 칸 사이인지 돌려준다. t는 화면 보간 전용이고 규칙에 쓰지 않는다.</summary>
        public void Locate(int progress, int unitsPerCell, out GridPos from, out GridPos to, out float t)
        {
            var clamped = Math.Max(0, Math.Min(progress, Length * unitsPerCell));
            var segment = Math.Min(clamped / unitsPerCell, Length - 1);
            from = _cells[segment];
            to = _cells[segment + 1];
            t = (clamped - segment * unitsPerCell) / (float)unitsPerCell;
        }

        private static GridPos ParseCorner(string text, string corners)
        {
            var pair = text.Split(',');
            if (pair.Length == 2
                && int.TryParse(pair[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)
                && int.TryParse(pair[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y))
                return new GridPos(x, y);

            throw new FormatException("Bad route corner '" + text + "' in: " + corners);
        }

        private static void AppendSegment(List<GridPos> cells, GridPos from, GridPos to, string corners)
        {
            var dx = Math.Sign(to.X - from.X);
            var dy = Math.Sign(to.Y - from.Y);
            if ((dx == 0) == (dy == 0))
                throw new FormatException("Route segment " + from + " to " + to + " must be straight and not empty: " + corners);

            var current = from;
            while (!current.Equals(to))
            {
                current = new GridPos(current.X + dx, current.Y + dy);
                cells.Add(current);
            }
        }

        private static void Validate(GridMap map, List<GridPos> cells, string corners)
        {
            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                if (!map.InBounds(cell)) throw new FormatException("Route leaves the map at " + cell + ": " + corners);

                var expected = i == 0 ? CellType.Entrance : i == cells.Count - 1 ? CellType.Exit : CellType.Ground;
                if (map[cell] != expected)
                    throw new FormatException("Route cell " + cell + " is " + map[cell] + ", expected " + expected + ": " + corners);
            }
        }
    }
}
