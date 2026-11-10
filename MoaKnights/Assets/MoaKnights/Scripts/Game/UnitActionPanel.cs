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
        [SerializeField] private Button _skillButton;
        [SerializeField] private TMP_Text _skillLabel;

        private Simulation _simulation;
        private MapView _mapView;
        private UnitView _unitView;
        private PlacementController _placement;
        private BattleViewAsset _view;
        private Camera _camera;

        private int _selectedId;
        private int _shownRefund = -1;
        private string _shownSkill;

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
            _skillLabel.font = font;
            _retreatButton.onClick.AddListener(Retreat);
            _skillButton.onClick.AddListener(UseSkill);
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

            ShowSkill(unit);

            var refund = _simulation.Definition.Deploy.Refund(unit.SpentCost);
            if (refund == _shownRefund) return;
            _shownRefund = refund;
            _retreatLabel.text = Text.Format("ui.unit.retreat", refund);
        }

        /// <summary>스킬 버튼: 이름과 SP, 쓰는 중이면 "사용 중". SP가 차야 누를 수 있다.</summary>
        private void ShowSkill(Unit unit)
        {
            var skills = _simulation.SkillsOf(unit);
            var stage = unit.IsSkillActive ? unit.ActiveSkillStage : unit.Stage;

            // 스킬 이름 키는 능력자 ID와 단계로 만든다. 표에 있는지는 ProjectRuleTests가 검사한다.
            var name = Text.Get("skill." + unit.DefinitionId + "_" + stage + ".name");
            var text = unit.IsSkillActive ? Text.Format("ui.unit.skill_active", name)
                : unit.Sp >= skills.MaxSp ? Text.Format("ui.unit.skill_ready", name)
                : Text.Format("ui.unit.skill_charging", name, unit.Sp, skills.MaxSp);

            _skillButton.interactable = _simulation.CheckSkill(unit.Id) == PlacementResult.Ok;
            if (text == _shownSkill) return;
            _shownSkill = text;
            _skillLabel.text = text;
        }

        private void UseSkill()
        {
            if (_selectedId != 0) _simulation.TryUseSkill(_selectedId);
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
            _shownSkill = null;
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
