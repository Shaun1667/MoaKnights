using System;
using System.Collections.Generic;
using MoaKnights.Core.Map;

namespace MoaKnights.Core.Sim
{
    /// <summary>
    /// 공격 범위. 오른쪽(Direction.Right)을 앞으로 보고 글자 격자로 적는다. 위쪽 줄이 +y.
    /// O 능력자 자리(공격 안 함), @ 능력자 자리(공격함), X 공격 칸, . 빈 칸. O나 @는 정확히 하나.
    /// 예) 자기 칸 + 앞 1칸: "@X"
    /// </summary>
    public sealed class AttackRange
    {
        private readonly List<GridPos> _offsets;

        /// <summary>오른쪽 기준 오프셋. (1,0)이 바로 앞 칸.</summary>
        public IReadOnlyList<GridPos> Offsets => _offsets;

        private AttackRange(List<GridPos> offsets)
        {
            _offsets = offsets;
        }

        public static AttackRange Parse(IReadOnlyList<string> rows)
        {
            if (rows == null || rows.Count == 0) throw new FormatException("Attack range has no rows.");

            var originRow = -1;
            var originColumn = -1;
            for (var r = 0; r < rows.Count; r++)
            {
                for (var c = 0; c < rows[r].Length; c++)
                {
                    var ch = rows[r][c];
                    if (ch != 'O' && ch != '@') continue;
                    if (originRow >= 0) throw new FormatException("Attack range has more than one origin (O or @).");
                    originRow = r;
                    originColumn = c;
                }
            }
            if (originRow < 0) throw new FormatException("Attack range has no origin (O or @).");

            var offsets = new List<GridPos>();
            for (var r = 0; r < rows.Count; r++)
            {
                for (var c = 0; c < rows[r].Length; c++)
                {
                    var ch = rows[r][c];
                    switch (ch)
                    {
                        case '.':
                        case 'O':
                            break;
                        case 'X':
                        case '@':
                            offsets.Add(new GridPos(c - originColumn, originRow - r));
                            break;
                        default:
                            throw new FormatException("Unknown attack range character '" + ch + "' at row " + r + ", column " + c + ".");
                    }
                }
            }

            if (offsets.Count == 0) throw new FormatException("Attack range has no attack cell (X or @).");
            return new AttackRange(offsets);
        }

        public bool Contains(GridPos origin, Direction direction, GridPos cell)
        {
            foreach (var offset in _offsets)
            {
                var rotated = Rotate(offset, direction);
                if (origin.X + rotated.X == cell.X && origin.Y + rotated.Y == cell.Y) return true;
            }
            return false;
        }

        /// <summary>실제 공격 칸. 맵 밖 칸도 포함하므로 그리는 쪽에서 거른다.</summary>
        public IEnumerable<GridPos> Cells(GridPos origin, Direction direction)
        {
            foreach (var offset in _offsets)
            {
                var rotated = Rotate(offset, direction);
                yield return new GridPos(origin.X + rotated.X, origin.Y + rotated.Y);
            }
        }

        /// <summary>오른쪽 기준 오프셋을 방향에 맞게 돌린다. 시계 반대 방향으로 Right → Up → Left → Down.</summary>
        public static GridPos Rotate(GridPos offset, Direction direction)
        {
            switch (direction)
            {
                case Direction.Right: return offset;
                case Direction.Up: return new GridPos(-offset.Y, offset.X);
                case Direction.Left: return new GridPos(-offset.X, -offset.Y);
                case Direction.Down: return new GridPos(offset.Y, -offset.X);
                default: throw new ArgumentOutOfRangeException(nameof(direction));
            }
        }
    }
}
