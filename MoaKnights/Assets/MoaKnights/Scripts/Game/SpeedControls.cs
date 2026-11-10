using MoaKnights.Core.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Text = MoaKnights.Localization.Text;

namespace MoaKnights.Game
{
    /// <summary>
    /// 오른쪽 위 일시정지 · 배속 버튼과 단축키(Space 일시정지, Tab 배속 전환).
    /// 시간은 TickClock만 바꾼다 — 규칙은 틱 수만 다르게 받는다 (기획 4-9: 적 진행과 코스트 회복 모두 2배).
    /// </summary>
    public sealed class SpeedControls : MonoBehaviour
    {
        private const int NormalSpeed = 1;
        private const int FastSpeed = 2;

        [SerializeField] private Button _pauseButton;
        [SerializeField] private TMP_Text _pauseLabel;
        [SerializeField] private Button _speedButton;
        [SerializeField] private TMP_Text _speedLabel;

        private TickClock _clock;
        private bool _shownPaused;
        private int _shownSpeed = -1;

        public void Bind(TickClock clock, TMP_FontAsset font)
        {
            _clock = clock;
            _pauseLabel.font = font;
            _speedLabel.font = font;
            _pauseButton.onClick.AddListener(TogglePause);
            _speedButton.onClick.AddListener(ToggleSpeed);
            Refresh(force: true);
        }

        private void Update()
        {
            if (_clock == null) return;

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.spaceKey.wasPressedThisFrame) TogglePause();
                if (keyboard.tabKey.wasPressedThisFrame) ToggleSpeed();
            }

            Refresh(force: false);
        }

        private void TogglePause() => _clock.Paused = !_clock.Paused;

        private void ToggleSpeed() => _clock.SetSpeed(_clock.Speed == FastSpeed ? NormalSpeed : FastSpeed);

        private void Refresh(bool force)
        {
            if (force || _shownPaused != _clock.Paused)
            {
                _shownPaused = _clock.Paused;
                _pauseLabel.text = Text.Get(_clock.Paused ? "ui.hud.resume" : "ui.hud.pause");
            }

            if (force || _shownSpeed != _clock.Speed)
            {
                _shownSpeed = _clock.Speed;
                _speedLabel.text = Text.Format("ui.hud.speed", _clock.Speed);
            }
        }
    }
}
