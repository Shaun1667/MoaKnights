using MoaKnights.Core.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Text = MoaKnights.Localization.Text;

namespace MoaKnights.Game
{
    /// <summary>판이 끝나면 화면 가운데에 결과(승리 · 패배, 별)와 다시 · 다음 · 스테이지 선택 버튼을 띄운다.</summary>
    public sealed class ResultPanel : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private Image[] _stars;
        [SerializeField] private TMP_Text _rewardLabel;
        [SerializeField] private Button _retryButton;
        [SerializeField] private TMP_Text _retryLabel;
        [SerializeField] private Button _nextButton;
        [SerializeField] private TMP_Text _nextLabel;
        [SerializeField] private Button _selectButton;
        [SerializeField] private TMP_Text _selectLabel;

        private Simulation _simulation;
        private BattleViewAsset _view;
        private System.Func<int> _reward;
        private System.Func<int> _currency;

        public void Bind(Simulation simulation, BattleViewAsset view, TMP_FontAsset font, bool hasNextStage,
            System.Action retry, System.Action next, System.Action select, System.Func<int> reward, System.Func<int> currency)
        {
            _simulation = simulation;
            _view = view;
            _reward = reward;
            _currency = currency;

            foreach (var label in new[] { _titleLabel, _rewardLabel, _retryLabel, _nextLabel, _selectLabel }) label.font = font;
            _retryLabel.text = Text.Get("ui.result.retry");
            _nextLabel.text = Text.Get("ui.result.next");
            _selectLabel.text = Text.Get("ui.result.select");

            _retryButton.onClick.AddListener(() => retry());
            _nextButton.onClick.AddListener(() => next());
            _selectButton.onClick.AddListener(() => select());
            _nextButton.gameObject.SetActive(hasNextStage);

            _simulation.BattleEnded += Show;
            _root.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_simulation != null) _simulation.BattleEnded -= Show;
        }

        private void Show(BattleOutcome outcome)
        {
            var victory = outcome == BattleOutcome.Victory;
            _titleLabel.text = Text.Get(victory ? "ui.result.victory" : "ui.result.defeat");
            _titleLabel.color = victory ? _view.VictoryColor : _view.DefeatColor;

            var stars = _simulation.Stars();
            for (var i = 0; i < _stars.Length; i++) _stars[i].color = i < stars ? _view.StarOnColor : _view.StarOffColor;

            // 받은 재화와 지금 가진 재화. 못 받았으면 비운다.
            var reward = _reward();
            _rewardLabel.text = reward > 0 ? Text.Format("ui.result.reward", reward, _currency()) : string.Empty;

            // 지면 "다음 스테이지"는 감춘다.
            if (!victory) _nextButton.gameObject.SetActive(false);
            _root.SetActive(true);
        }
    }
}
