using MoaKnights.Core.Map;
using UnityEngine;

namespace MoaKnights.Game
{
    /// <summary>GridMap을 그레이박스 도형으로 그리기만 한다. 규칙은 모른다.</summary>
    public sealed class MapView : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private GridMap _map;
        private BattleViewAsset _view;

        public Bounds WorldBounds { get; private set; }

        public void Build(GridMap map, BattleViewAsset view)
        {
            _map = map;
            _view = view;

            var block = new MaterialPropertyBlock();
            var bounds = new Bounds(transform.position, Vector3.zero);

            for (var y = 0; y < map.Height; y++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    var type = map[new GridPos(x, y)];
                    if (!view.TryGetCell(type, out var visual))
                    {
                        Debug.LogError("BattleView has no row for cell type " + type);
                        continue;
                    }

                    var cell = CreateCell(x, y, map, view, visual, block);
                    bounds.Encapsulate(cell.GetComponent<Renderer>().bounds);
                }
            }

            WorldBounds = bounds;
        }

        /// <summary>칸 윗면 가운데의 월드 좌표. 칸 위에 올라서는 것을 그릴 때 쓴다.</summary>
        public Vector3 CellTop(GridPos pos)
        {
            var height = _view.TryGetCell(_map[pos], out var visual) ? visual.Height : 0f;
            return transform.TransformPoint(CellCenter(pos.X, pos.Y, _map, _view) + Vector3.up * height);
        }

        private GameObject CreateCell(int x, int y, GridMap map, BattleViewAsset view, BattleViewAsset.CellVisual visual, MaterialPropertyBlock block)
        {
            var cell = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cell.name = "Cell_" + x + "_" + y;
            Destroy(cell.GetComponent<Collider>());

            var size = view.CellSize - view.CellGap;
            var center = CellCenter(x, y, map, view);
            cell.transform.SetParent(transform, false);
            cell.transform.localScale = new Vector3(size, visual.Height, size);
            cell.transform.localPosition = center + Vector3.up * (visual.Height * 0.5f);

            var cellRenderer = cell.GetComponent<Renderer>();
            cellRenderer.sharedMaterial = view.CellMaterial;
            block.SetColor(BaseColor, visual.Color);
            cellRenderer.SetPropertyBlock(block);
            return cell;
        }

        private static Vector3 CellCenter(int x, int y, GridMap map, BattleViewAsset view)
        {
            return new Vector3(
                (x - (map.Width - 1) * 0.5f) * view.CellSize,
                0f,
                (y - (map.Height - 1) * 0.5f) * view.CellSize);
        }
    }
}
