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

        /// <summary>
        /// 화면 광선이 닿는 칸을 찾는다. 칸 윗면만 보며, 여러 칸에 닿으면 카메라에 가장 가까운 칸이다.
        /// 콜라이더 없이 계산하므로 칸 오브젝트를 바꿔도 영향이 없다.
        /// </summary>
        public bool TryPickCell(Ray ray, out GridPos cell)
        {
            cell = default;
            var best = float.MaxValue;
            var half = _view.CellSize * 0.5f;

            for (var y = 0; y < _map.Height; y++)
            {
                for (var x = 0; x < _map.Width; x++)
                {
                    var pos = new GridPos(x, y);
                    var top = CellTop(pos);
                    var plane = new Plane(transform.up, top);
                    if (!plane.Raycast(ray, out var distance) || distance >= best) continue;

                    var local = transform.InverseTransformPoint(ray.GetPoint(distance)) - transform.InverseTransformPoint(top);
                    if (Mathf.Abs(local.x) > half || Mathf.Abs(local.z) > half) continue;

                    best = distance;
                    cell = pos;
                }
            }

            return best < float.MaxValue;
        }

        /// <summary>칸 윗면 높이의 수평면에서 광선이 닿는 점. 방향을 고를 때 쓴다.</summary>
        public bool TryPointOnCellPlane(Ray ray, GridPos cell, out Vector3 point)
        {
            var top = CellTop(cell);
            var plane = new Plane(transform.up, top);
            point = default;
            if (!plane.Raycast(ray, out var distance)) return false;
            point = ray.GetPoint(distance);
            return true;
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
