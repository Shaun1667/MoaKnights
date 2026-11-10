using System.Collections.Generic;
using MoaKnights.Core.Map;
using MoaKnights.Core.Sim;
using UnityEngine;

namespace MoaKnights.Game
{
    /// <summary>공격 범위 칸을 납작한 판으로 덮어 보여 준다. 배치 미리보기에서 쓴다. 판은 재사용한다.</summary>
    public sealed class RangeView
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private readonly List<GameObject> _tiles = new List<GameObject>();
        private readonly Transform _parent;
        private readonly MapView _mapView;
        private readonly BattleViewAsset _view;
        private readonly GridMap _map;

        public RangeView(Transform parent, MapView mapView, BattleViewAsset view, GridMap map)
        {
            _parent = parent;
            _mapView = mapView;
            _view = view;
            _map = map;
        }

        public void Show(AttackRange range, GridPos origin, Direction direction)
        {
            var used = 0;
            foreach (var cell in range.Cells(origin, direction))
            {
                if (!_map.InBounds(cell)) continue;
                var tile = Tile(used++);
                tile.SetActive(true);
                tile.transform.position = _mapView.CellTop(cell) + Vector3.up * 0.02f;
            }

            for (var i = used; i < _tiles.Count; i++) _tiles[i].SetActive(false);
        }

        public void Hide()
        {
            foreach (var tile in _tiles) tile.SetActive(false);
        }

        public void Destroy()
        {
            foreach (var tile in _tiles) Object.Destroy(tile);
            _tiles.Clear();
        }

        private GameObject Tile(int index)
        {
            if (index < _tiles.Count) return _tiles[index];

            var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.name = "RangeTile";
            Object.Destroy(tile.GetComponent<Collider>());
            tile.transform.SetParent(_parent, false);
            var size = _view.CellSize - _view.CellGap;
            tile.transform.localScale = new Vector3(size * 0.9f, 0.02f, size * 0.9f);

            var tileRenderer = tile.GetComponent<Renderer>();
            tileRenderer.sharedMaterial = _view.CellMaterial;
            tileRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColor, _view.RangeColor);
            tileRenderer.SetPropertyBlock(block);

            _tiles.Add(tile);
            return tile;
        }
    }
}
