using System;
using MoaKnights.Core.Map;
using MoaKnights.Core.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MoaKnights.Game
{
    /// <summary>
    /// 카드 드래그 배치 입력. 카드를 끌어 칸에 놓고, 다시 누른 채 끌어 방향을 고른 뒤 떼면 배치를 요청한다.
    /// 오른쪽 클릭이나 Esc는 취소. 판정은 Simulation이 하고 여기서는 미리보기만 그린다.
    /// </summary>
    public sealed class PlacementController : MonoBehaviour
    {
        private enum State
        {
            Idle,
            DraggingCard,
            ChoosingDirection,
        }

        private Simulation _simulation;
        private MapView _mapView;
        private BattleViewAsset _view;
        private Camera _camera;

        private State _state;
        private string _unitId;
        private UnitModel _preview;
        private GridPos _cell;
        private bool _hasCell;
        private Direction _direction;
        private bool _directionDragging;
        private RangeView _range;

        /// <summary>배치가 거절됐을 때. 화면 계층이 결과에 맞는 글을 띄운다.</summary>
        public event Action<PlacementResult> PlacementRejected;

        public bool IsBusy => _state != State.Idle;

        public void Bind(Simulation simulation, MapView mapView, BattleViewAsset view, Camera worldCamera, CardBar cards)
        {
            _simulation = simulation;
            _mapView = mapView;
            _view = view;
            _camera = worldCamera;
            _range = new RangeView(transform, mapView, view, simulation.Map);
            cards.CardPressed += BeginDrag;
        }

        private void OnDestroy() => _range?.Destroy();

        private void ShowRange()
        {
            if (_simulation.Definition.TryGetUnit(_unitId, out var unit)) _range.Show(unit.Range, _cell, _direction);
        }

        private void BeginDrag(string unitId)
        {
            if (_state != State.Idle) return;
            if (!_view.TryGetUnit(unitId, out var visual))
            {
                Debug.LogError("BattleView has no row for unit " + unitId);
                return;
            }

            _unitId = unitId;
            _direction = Direction.Right;
            _preview = UnitModel.Create(transform, "Preview_" + unitId, visual, _view);
            _preview.Root.SetActive(false);
            _state = State.DraggingCard;
        }

        private void Update()
        {
            if (_state == State.Idle) return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            var keyboard = Keyboard.current;
            if (mouse.rightButton.wasPressedThisFrame || (keyboard != null && keyboard.escapeKey.wasPressedThisFrame))
            {
                Cancel();
                return;
            }

            var pointer = mouse.position.ReadValue();
            if (_state == State.DraggingCard) UpdateDragging(mouse, pointer);
            else UpdateChoosingDirection(mouse, pointer);
        }

        private void UpdateDragging(Mouse mouse, Vector2 pointer)
        {
            _hasCell = _mapView.TryPickCell(_camera.ScreenPointToRay(pointer), out _cell);
            var result = _hasCell ? _simulation.CheckPlacement(_unitId, _cell) : PlacementResult.OutOfBounds;

            _preview.Root.SetActive(_hasCell);
            if (_hasCell)
            {
                _preview.Place(_mapView.CellTop(_cell), _direction);
                _preview.Tint(result == PlacementResult.Ok ? _view.PreviewValidColor : _view.PreviewInvalidColor);
                ShowRange();
            }
            else
            {
                _range.Hide();
            }

            if (!mouse.leftButton.wasReleasedThisFrame) return;

            if (!_hasCell)
            {
                Cancel();
                return;
            }

            if (result != PlacementResult.Ok)
            {
                PlacementRejected?.Invoke(result);
                Cancel();
                return;
            }

            _preview.Tint(null);
            _directionDragging = false;
            _state = State.ChoosingDirection;
        }

        private void UpdateChoosingDirection(Mouse mouse, Vector2 pointer)
        {
            if (mouse.leftButton.wasPressedThisFrame) _directionDragging = true;

            UpdateDirection(pointer);
            _preview.Place(_mapView.CellTop(_cell), _direction);
            ShowRange();

            if (!_directionDragging || !mouse.leftButton.wasReleasedThisFrame) return;

            var result = _simulation.TryPlaceUnit(_unitId, _cell, _direction);
            if (result != PlacementResult.Ok) PlacementRejected?.Invoke(result);
            Cancel();
        }

        private void UpdateDirection(Vector2 pointer)
        {
            var cellScreen = (Vector2)_camera.WorldToScreenPoint(_mapView.CellTop(_cell));
            if ((pointer - cellScreen).magnitude < _view.DirectionDragPixels) return;
            if (!_mapView.TryPointOnCellPlane(_camera.ScreenPointToRay(pointer), _cell, out var point)) return;

            // 맵 기준 좌표로 바꿔 격자 축(x, z)에서 더 크게 움직인 쪽을 고른다.
            var delta = _mapView.transform.InverseTransformDirection(point - _mapView.CellTop(_cell));
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.z)) _direction = delta.x >= 0f ? Direction.Right : Direction.Left;
            else _direction = delta.z >= 0f ? Direction.Up : Direction.Down;
        }

        private void Cancel()
        {
            if (_preview != null) Destroy(_preview.Root);
            _preview = null;
            _range.Hide();
            _unitId = null;
            _hasCell = false;
            _state = State.Idle;
        }
    }
}
