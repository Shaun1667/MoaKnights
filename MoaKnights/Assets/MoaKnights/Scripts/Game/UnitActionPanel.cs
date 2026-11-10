using MoaKnights.Core.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Text = MoaKnights.Localization.Text;

namespace MoaKnights.Game
{
    /// <summary>
    /// 배치된 능력자를 클릭하면 머리 위에 퇴각 버튼을 띄운다. 빈 곳 클릭 · 오른쪽 클릭 · Esc로 닫힌다.
    /// 배치 중(카드를 끄는 중)에는 열리지 않는다. 판정은 Simulation이 하고 여기서는 요청만 한다.
    /// </summary>
    public sealed class UnitActionPanel : MonoBehaviour
    {
        [SerializeField] private RectTransform _panel;
        [SerializeField] private Button _retreatButton;
        [SerializeField] private TMP_Text _retreatLabel;

        private Simulation _simulation;
        private MapView _mapView;
        private UnitView _unitView;
        private PlacementController _placement;
        private BattleViewAsset _view;
        private Camera _camera;

        private int _selectedId;
        private int _shownRefund = -1;

        public int SelectedUnitId => _selectedId;

        public void Bind(Simulation simulation, MapView mapView, UnitView unitView, PlacementController placement, BattleViewAsset view, TMP_FontAsset font, Camera worldCamera)
        {
            _simulation = simulation;
            _mapView = mapView;
            _unitView = unitView;
            _placement = placement;
            _view = view;
            _camera = worldCamera;

            _retreatLabel.font = font;
            _retreatButton.onClick.AddListener(Retreat);
            Close();
        }

        private void Update()
        {
            if (_simulation == null) return;

            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            if (_placement.IsBusy)
            {
                Close();
                return;
            }

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Close();
            if (mouse == null) return;
            if (mouse.rightButton.wasPressedThisFrame) Close();

            // 버튼이나 카드 위를 누른 것은 UI가 처리한다.
            if (mouse.leftButton.wasPressedThisFrame && !IsPointerOverUi()) Select(mouse.position.ReadValue());
        }

        private void LateUpdate()
        {
            if (_selectedId == 0) return;

            var unit = _simulation.World.UnitById(_selectedId);
            if (unit == null || !_unitView.TryGetTop(_selectedId, out var top))
            {
                Close();
                return;
            }

            var screen = (Vector2)_camera.WorldToScreenPoint(top);
            _panel.position = screen + _view.UnitActionOffset;

            var refund = _simulation.Definition.Deploy.Refund(unit.SpentCost);
            if (refund == _shownRefund) return;
            _shownRefund = refund;
            _retreatLabel.text = Text.Format("ui.unit.retreat", refund);
        }

        private void Select(Vector2 pointer)
        {
            Unit unit = null;
            if (_mapView.TryPickCell(_camera.ScreenPointToRay(pointer), out var cell)) unit = _simulation.World.UnitAt(cell);

            if (unit == null)
            {
                Close();
                return;
            }

            _selectedId = unit.Id;
            _shownRefund = -1;
            _panel.gameObject.SetActive(true);
        }

        private void Retreat()
        {
            if (_selectedId != 0) _simulation.TryRetreat(_selectedId);
            Close();
        }

        private void Close()
        {
            _selectedId = 0;
            _panel.gameObject.SetActive(false);
        }

        private static bool IsPointerOverUi() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
