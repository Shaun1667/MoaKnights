using System;
using MoaKnights.Core.Map;
using UnityEngine;

namespace MoaKnights.Game
{
    /// <summary>전투 화면 표현값 한 곳. 칸 종류마다 표 한 줄. 규칙에는 영향이 없다.</summary>
    [CreateAssetMenu(fileName = "BattleView", menuName = "MoaKnights/Battle View")]
    public sealed class BattleViewAsset : ScriptableObject
    {
        [Serializable]
        public struct CellVisual
        {
            public CellType Type;
            public Color Color;
            public float Height;
        }

        [SerializeField] private Material _cellMaterial;
        [SerializeField] private CellVisual[] _cells;
        [SerializeField] private float _cellSize;
        [SerializeField] private float _cellGap;
        [SerializeField] private float _cameraPitch;
        [SerializeField] private float _cameraYaw;
        [SerializeField] private float _cameraDistance;
        [SerializeField] private float _cameraPadding;

        public Material CellMaterial => _cellMaterial;
        public float CellSize => _cellSize;
        public float CellGap => _cellGap;
        public float CameraPitch => _cameraPitch;
        public float CameraYaw => _cameraYaw;
        public float CameraDistance => _cameraDistance;
        public float CameraPadding => _cameraPadding;

        public bool TryGetCell(CellType type, out CellVisual visual)
        {
            foreach (var cell in _cells)
            {
                if (cell.Type != type) continue;
                visual = cell;
                return true;
            }
            visual = default;
            return false;
        }
    }
}
