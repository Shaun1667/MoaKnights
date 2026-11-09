using MoaKnights.Core.Sim;
using MoaKnights.Localization;
using TMPro;
using UnityEngine;

namespace MoaKnights.Game
{
    /// <summary>디펜스 HP와 받은 피해 숫자를 그리기만 한다. 글은 표에서 가져온다.</summary>
    public sealed class BattleHud : MonoBehaviour
    {
        [SerializeField] private TMP_Text _defenseHpLabel;
        [SerializeField] private TMP_Text _damageLabel;

        private Simulation _simulation;
        private BattleViewAsset _view;
        private float _damageSecondsLeft;

        public void Bind(Simulation simulation, BattleViewAsset view, TMP_FontAsset font)
        {
            _simulation = simulation;
            _view = view;

            _defenseHpLabel.font = font;
            _damageLabel.font = font;
            _damageLabel.color = view.DamageNumberColor;
            _damageLabel.text = string.Empty;

            _simulation.DefenseDamaged += OnDefenseDamaged;
            RefreshHp();
        }

        private void OnDestroy()
        {
            if (_simulation != null) _simulation.DefenseDamaged -= OnDefenseDamaged;
        }

        private void Update()
        {
            if (_damageSecondsLeft <= 0f) return;

            _damageSecondsLeft -= Time.unscaledDeltaTime;
            if (_damageSecondsLeft <= 0f) _damageLabel.text = string.Empty;
        }

        private void OnDefenseDamaged(int damage)
        {
            RefreshHp();
            _damageLabel.text = Text.Format("ui.hud.damage", damage);
            _damageSecondsLeft = _view.DamageNumberSeconds;
        }

        private void RefreshHp()
        {
            _defenseHpLabel.text = Text.Format("ui.hud.defense_hp", _simulation.World.DefenseHp, _simulation.Definition.DefenseHp);
        }
    }
}
