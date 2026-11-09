using System;
using System.Collections.Generic;

namespace MoaKnights.Core.Map
{
    /// <summary>칸 종류. enum은 끝에만 추가한다.</summary>
    public enum CellType
    {
        Blocked = 0,
        Ground = 1,
        Hill = 2,
        Entrance = 3,
        Exit = 4,
    }

    public readonly struct GridPos : IEquatable<GridPos>
    {
        public readonly int X;
        public readonly int Y;

        public GridPos(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(GridPos other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridPos other && Equals(other);
        public override int GetHashCode() => (X * 397) ^ Y;
        public override string ToString() => "(" + X + "," + Y + ")";
    }

    /// <summary>
    /// 격자 맵. 글자 격자는 위쪽 줄부터 적고, 좌표는 왼쪽 아래가 (0,0)이다.
    /// # 막힘, . 지상, H 언덕, S 입구, E 출구.
    /// </summary>
    public sealed class GridMap
    {
        private readonly CellType[,] _cells;
        private readonly List<GridPos> _entrances = new List<GridPos>();
        private readonly List<GridPos> _exits = new List<GridPos>();

        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<GridPos> Entrances => _entrances;
        public IReadOnlyList<GridPos> Exits => _exits;

        private GridMap(int width, int height)
        {
            Width = width;
            Height = height;
            _cells = new CellType[width, height];
        }

        public static GridMap Parse(IReadOnlyList<string> rows)
        {
            if (rows == null || rows.Count == 0) throw new FormatException("Map has no rows.");
            var width = rows[0].Length;
            if (width == 0) throw new FormatException("Map row 0 is empty.");

            var map = new GridMap(width, rows.Count);
            for (var r = 0; r < rows.Count; r++)
            {
                if (rows[r].Length != width)
                    throw new FormatException("Map row " + r + " has width " + rows[r].Length + ", expected " + width + ".");

                var y = rows.Count - 1 - r;
                for (var x = 0; x < width; x++)
                {
                    var type = ParseCell(rows[r][x], r, x);
                    map._cells[x, y] = type;
                    if (type == CellType.Entrance) map._entrances.Add(new GridPos(x, y));
                    if (type == CellType.Exit) map._exits.Add(new GridPos(x, y));
                }
            }

            if (map._entrances.Count == 0) throw new FormatException("Map has no entrance (S).");
            if (map._exits.Count == 0) throw new FormatException("Map has no exit (E).");
            return map;
        }

        public bool InBounds(GridPos pos) => pos.X >= 0 && pos.X < Width && pos.Y >= 0 && pos.Y < Height;

        public CellType this[GridPos pos] => _cells[pos.X, pos.Y];

        public int Count(CellType type)
        {
            var count = 0;
            foreach (var cell in _cells)
                if (cell == type) count++;
            return count;
        }

        private static CellType ParseCell(char c, int row, int column)
        {
            switch (c)
            {
                case '#': return CellType.Blocked;
                case '.': return CellType.Ground;
                case 'H': return CellType.Hill;
                case 'S': return CellType.Entrance;
                case 'E': return CellType.Exit;
                default:
                    throw new FormatException("Unknown map character '" + c + "' at row " + row + ", column " + column + ".");
            }
        }
    }
}
