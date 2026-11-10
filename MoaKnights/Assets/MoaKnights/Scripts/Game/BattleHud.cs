using MoaKnights.Core.Sim;
using MoaKnights.Localization;
using TMPro;
using UnityEngine;

namespace MoaKnights.Game
{
    /// <summary>디펜스 HP · 코스트 · 받은 피해 숫자 · 안내 글을 그리기만 한다. 글은 표에서 가져온다.</summary>
    public sealed class BattleHud : MonoBehaviour
    {
        [SerializeField] private TMP_Text _defenseHpLabel;
        [SerializeField] private TMP_Text _damageLabel;
        [SerializeField] private TMP_Text _costLabel;
        [SerializeField] private TMP_Text _messageLabel;

        private Simulation _simulation;
        private BattleViewAsset _view;
        private float _damageSecondsLeft;
        private float _messageSecondsLeft;
        private int _shownCost = -1;

        public void Bind(Simulation simulation, BattleViewAsset view, TMP_FontAsset font, PlacementController placement)
        {
            _simulation = simulation;
            _view = view;

            _defenseHpLabel.font = font;
            _damageLabel.font = font;
            _costLabel.font = font;
            _messageLabel.font = font;
            _damageLabel.color = view.DamageNumberColor;
            _damageLabel.text = string.Empty;
            _messageLabel.text = string.Empty;

            _simulation.DefenseDamaged += OnDefenseDamaged;
            placement.PlacementRejected += OnPlacementRejected;
            RefreshHp();
            RefreshCost();
        }

        private void OnDestroy()
        {
            if (_simulation != null) _simulation.DefenseDamaged -= OnDefenseDamaged;
        }

        private void Update()
        {
            if (_simulation == null) return;
            RefreshCost();
            Countdown(ref _damageSecondsLeft, _damageLabel);
            Countdown(ref _messageSecondsLeft, _messageLabel);
        }

        private static void Countdown(ref float secondsLeft, TMP_Text label)
        {
            if (secondsLeft <= 0f) return;
            secondsLeft -= Time.unscaledDeltaTime;
            if (secondsLeft <= 0f) label.text = string.Empty;
        }

        private void OnDefenseDamaged(int damage)
        {
            RefreshHp();
            _damageLabel.text = Text.Format("ui.hud.damage", damage);
            _damageSecondsLeft = _view.DamageNumberSeconds;
        }

        private void OnPlacementRejected(PlacementResult result)
        {
            var key = RejectKey(result);
            if (key == null) return;
            _messageLabel.text = Text.Get(key);
            _messageSecondsLeft = _view.MessageSeconds;
        }

        private static string RejectKey(PlacementResult result)
        {
            switch (result)
            {
                case PlacementResult.WrongTerrain: return "ui.place.wrong_terrain";
                case PlacementResult.Occupied: return "ui.place.occupied";
                case PlacementResult.AlreadyDeployed: return "ui.place.already_deployed";
                case PlacementResult.NotEnoughCost: return "ui.place.not_enough_cost";
                case PlacementResult.OnCooldown: return "ui.place.on_cooldown";
                case PlacementResult.MaxStackReached: return "ui.place.max_stack";
                default: return null;
            }
        }

        private void RefreshHp()
        {
            _defenseHpLabel.text = Text.Format("ui.hud.defense_hp", _simulation.World.DefenseHp, _simulation.Definition.DefenseHp);
        }

        private void RefreshCost()
        {
            if (_shownCost == _simulation.World.Cost) return;
            _shownCost = _simulation.World.Cost;
            _costLabel.text = Text.Format("ui.hud.cost", _shownCost, _simulation.Definition.Cost.Max);
        }
    }
}
