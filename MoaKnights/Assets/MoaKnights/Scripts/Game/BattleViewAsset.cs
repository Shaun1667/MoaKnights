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

        /// <summary>적 한 종류의 모습. 프리팹이 없으면 그레이박스 캡슐로 그린다.</summary>
        [Serializable]
        public struct EnemyVisual
        {
            public string Id;
            public GameObject Prefab;
            public Color Color;
            public float Size;
        }

        /// <summary>능력자 한 종류의 모습. 프리팹이 없으면 그레이박스 상자와 방향 표시로 그린다.</summary>
        [Serializable]
        public struct UnitVisual
        {
            public string Id;
            public GameObject Prefab;
            public Color Color;
            public float Size;

            /// <summary>카드에 그리는 얼굴. 없으면 카드에 이름만 나온다.</summary>
            public Sprite Portrait;
        }

        [SerializeField] private Material _cellMaterial;
        [SerializeField] private CellVisual[] _cells;
        [SerializeField] private EnemyVisual[] _enemies;
        [SerializeField] private UnitVisual[] _units;
        [SerializeField] private Color _damageNumberColor;
        [SerializeField] private float _damageNumberSeconds;
        [SerializeField] private float _messageSeconds;
        [SerializeField] private Color _directionMarkerColor;
        [SerializeField] private Color _previewValidColor;
        [SerializeField] private Color _previewInvalidColor;
        [SerializeField] private float _directionDragPixels;
        [SerializeField] private Vector2 _cardSize;
        [SerializeField] private float _cardSpacing;
        [SerializeField] private Color _cardColor;
        [SerializeField] private Color _cardDisabledColor;
        [SerializeField] private Color _cardTextColor;
        [SerializeField] private Color _cardPortraitDisabledColor;
        [SerializeField] private float _cameraBottomReserve;
        [SerializeField] private Color _rangeColor;
        [SerializeField] private Vector2 _hpBarSize;
        [SerializeField] private Color _hpBarBackColor;
        [SerializeField] private Color _hpBarEnemyColor;
        [SerializeField] private Color _hpBarUnitColor;
        [SerializeField] private Color _damageToEnemyColor;
        [SerializeField] private Color _damageToUnitColor;
        [SerializeField] private float _floatingNumberSeconds;
        [SerializeField] private float _floatingNumberRise;
        [SerializeField] private float _floatingNumberSize;
        [SerializeField] private Color _stagePipColor;
        [SerializeField] private Vector2 _unitActionOffset;
        [SerializeField] private Color _spColor;
        [SerializeField] private Color _spFullColor;
        [SerializeField] private Color _skillActiveColor;
        [SerializeField] private Color _victoryColor;
        [SerializeField] private Color _defeatColor;
        [SerializeField] private Color _starOnColor;
        [SerializeField] private Color _starOffColor;
        [SerializeField] private Color _formationSelectedColor;
        [SerializeField] private float _cellSize;
        [SerializeField] private float _cellGap;
        [SerializeField] private float _cameraPitch;
        [SerializeField] private float _cameraYaw;
        [SerializeField] private float _cameraDistance;
        [SerializeField] private float _cameraPadding;
        [SerializeField] private bool _cameraPerspective;
        [SerializeField] private float _cameraFieldOfView;
        [SerializeField] private float _cameraTopReserve;
        [SerializeField] private Color _gachaRarity3Color;
        [SerializeField] private Color _gachaRarity4Color;
        [SerializeField] private Color _gachaRarity5Color;
        [SerializeField] private Color _gachaCardBackColor;
        [SerializeField] private float _gachaLightSeconds;
        [SerializeField] private float _gachaSpreadSeconds;
        [SerializeField] private float _gachaFlipSeconds;
        [SerializeField] private float _gachaFlipIntervalSeconds;

        public Material CellMaterial => _cellMaterial;
        public float CellSize => _cellSize;
        public float CellGap => _cellGap;
        public float CameraPitch => _cameraPitch;
        public float CameraYaw => _cameraYaw;
        public float CameraDistance => _cameraDistance;
        public float CameraPadding => _cameraPadding;

        /// <summary>참이면 원근 카메라(정면에서 내려다봄, 결정 2026-11-11), 거짓이면 예전 직교 쿼터뷰.</summary>
        public bool CameraPerspective => _cameraPerspective;

        /// <summary>원근 카메라의 세로 시야각(도).</summary>
        public float CameraFieldOfView => _cameraFieldOfView;

        /// <summary>화면 위쪽에서 비워 둘 비율 (제목 · 디펜스 HP 자리). 원근 카메라만 쓴다.</summary>
        public float CameraTopReserve => _cameraTopReserve;
        public Color DamageNumberColor => _damageNumberColor;
        public float DamageNumberSeconds => _damageNumberSeconds;
        public float MessageSeconds => _messageSeconds;
        public Color DirectionMarkerColor => _directionMarkerColor;
        public Color PreviewValidColor => _previewValidColor;
        public Color PreviewInvalidColor => _previewInvalidColor;

        /// <summary>방향을 고를 때 이만큼(화면 픽셀) 끌어야 방향이 바뀐다.</summary>
        public float DirectionDragPixels => _directionDragPixels;

        public Vector2 CardSize => _cardSize;
        public float CardSpacing => _cardSpacing;
        public Color CardColor => _cardColor;
        public Color CardDisabledColor => _cardDisabledColor;
        public Color CardTextColor => _cardTextColor;

        /// <summary>지금 쓸 수 없는 카드의 얼굴에 곱하는 색.</summary>
        public Color CardPortraitDisabledColor => _cardPortraitDisabledColor;

        /// <summary>카드 줄이 차지하는 화면 아래쪽 비율(0~1). 카메라가 이만큼 맵을 위로 올린다.</summary>
        public float CameraBottomReserve => _cameraBottomReserve;

        /// <summary>배치할 때 공격 범위 칸 위에 까는 얇은 판의 색. 재질이 불투명이라 알파는 쓰지 않는다.</summary>
        public Color RangeColor => _rangeColor;

        /// <summary>체력바 크기 (월드 단위 너비, 높이).</summary>
        public Vector2 HpBarSize => _hpBarSize;
        public Color HpBarBackColor => _hpBarBackColor;
        public Color HpBarEnemyColor => _hpBarEnemyColor;
        public Color HpBarUnitColor => _hpBarUnitColor;
        public Color DamageToEnemyColor => _damageToEnemyColor;
        public Color DamageToUnitColor => _damageToUnitColor;
        public float FloatingNumberSeconds => _floatingNumberSeconds;
        public float FloatingNumberRise => _floatingNumberRise;
        public float FloatingNumberSize => _floatingNumberSize;

        /// <summary>중첩 단계 점의 색.</summary>
        public Color StagePipColor => _stagePipColor;

        /// <summary>능력자를 클릭했을 때 뜨는 퇴각 버튼의 위치 (머리 위 화면 좌표에서 픽셀).</summary>
        public Vector2 UnitActionOffset => _unitActionOffset;

        /// <summary>SP 바: 차는 중 · 가득 참 · 스킬 사용 중(남은 시간) 색.</summary>
        public Color SpColor => _spColor;
        public Color SpFullColor => _spFullColor;
        public Color SkillActiveColor => _skillActiveColor;

        /// <summary>결과 창 제목 색 (승리 · 패배)과 별 색 (얻음 · 못 얻음).</summary>
        public Color VictoryColor => _victoryColor;
        public Color DefeatColor => _defeatColor;
        public Color StarOnColor => _starOnColor;
        public Color StarOffColor => _starOffColor;

        /// <summary>편성 창에서 출전으로 고른 능력자 카드 색.</summary>
        public Color FormationSelectedColor => _formationSelectedColor;

        /// <summary>뽑기 연출에서 등급(3 · 4 · 5성)을 알리는 빛 · 카드 테두리 색. 그 밖의 등급은 3성 색.</summary>
        public Color GachaRarityColor(int rarity) => rarity >= 5 ? _gachaRarity5Color : rarity == 4 ? _gachaRarity4Color : _gachaRarity3Color;

        /// <summary>뒤집기 전 카드 뒷면 색.</summary>
        public Color GachaCardBackColor => _gachaCardBackColor;

        /// <summary>뽑기 연출 시간(초): 빛 예고 · 카드가 펼쳐지는 시간 · 한 장 뒤집는 시간 · 다음 장까지 간격.</summary>
        public float GachaLightSeconds => _gachaLightSeconds;
        public float GachaSpreadSeconds => _gachaSpreadSeconds;
        public float GachaFlipSeconds => _gachaFlipSeconds;
        public float GachaFlipIntervalSeconds => _gachaFlipIntervalSeconds;

        public bool TryGetUnit(string id, out UnitVisual visual)
        {
            foreach (var unit in _units)
            {
                if (unit.Id != id) continue;
                visual = unit;
                return true;
            }
            visual = default;
            return false;
        }

        public bool TryGetEnemy(string id, out EnemyVisual visual)
        {
            foreach (var enemy in _enemies)
            {
                if (enemy.Id != id) continue;
                visual = enemy;
                return true;
            }
            visual = default;
            return false;
        }

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
