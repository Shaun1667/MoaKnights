using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MoaKnights.Core.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Text = MoaKnights.Localization.Text;

namespace MoaKnights.Game
{
    /// <summary>
    /// 뽑기 창. 위는 가진 별가루와 지금 나올 수 있는 등급 확률, 아래는 1회 · 10회 뽑기와 닫기.
    /// 뽑으면 연출: 가장 높은 등급의 색으로 빛이 맥박치며 커져 예고 → 번쩍 → 카드가 가운데에서 펼쳐지고 → 한 장씩 뒤집힌다.
    /// 연출 중 누르면 바로 모두 뒤집고, 다 뒤집힌 뒤 누르면 창으로 돌아온다. 시간은 실제 시간(일시정지와 무관).
    /// </summary>
    public sealed class GachaPanel : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _currencyLabel;
        [SerializeField] private TMP_Text _ratesLabel;
        [SerializeField] private TMP_Text _messageLabel;
        [SerializeField] private Button _singleButton;
        [SerializeField] private TMP_Text _singleLabel;
        [SerializeField] private Button _multiButton;
        [SerializeField] private TMP_Text _multiLabel;
        [SerializeField] private Button _closeButton;
        [SerializeField] private TMP_Text _closeLabel;

        [SerializeField] private Button _reveal;
        [SerializeField] private RectTransform _light;
        [SerializeField] private Image _flash;
        [SerializeField] private RectTransform _cardArea;
        [SerializeField] private GachaCard _cardTemplate;
        [SerializeField] private TMP_Text _hintLabel;

        private const float CardGap = 24f;
        private const int CardsPerRow = 5;
        private const float FlashSeconds = 0.3f;
        private const float MaxStepSeconds = 0.05f;

        private readonly List<GachaCard> _cards = new List<GachaCard>();

        private PlayerProfile _profile;
        private GachaRules _rules;
        private BattleViewAsset _view;
        private TMP_FontAsset _font;
        private Action _save;
        private Action<bool> _closed;

        private IReadOnlyList<GachaPull> _pulls = Array.Empty<GachaPull>();
        private Coroutine _playing;
        private bool _gotNewUnit;

        public bool IsOpen => _root.activeSelf;

        /// <summary>뽑기 연출 중인지 (빛 · 펼침 · 뒤집기). 다 뒤집힌 뒤에는 거짓.</summary>
        public bool IsRevealing => _playing != null;

        /// <summary>마지막 뽑기 결과 (디버그 · 확인용).</summary>
        public IReadOnlyList<GachaPull> LastPulls => _pulls;

        /// <param name="save">뽑은 뒤 프로필을 저장한다.</param>
        /// <param name="closed">창을 닫을 때. 인자는 이번에 새 능력자를 얻었는지.</param>
        public void Bind(PlayerProfile profile, GachaRules rules, BattleViewAsset view, TMP_FontAsset font, Action save, Action<bool> closed)
        {
            _profile = profile;
            _rules = rules;
            _view = view;
            _font = font;
            _save = save;
            _closed = closed;

            foreach (var label in new[] { _titleLabel, _currencyLabel, _ratesLabel, _messageLabel, _singleLabel, _multiLabel, _closeLabel, _hintLabel })
                label.font = font;
            _titleLabel.text = Text.Get("ui.gacha.title");
            _singleLabel.text = Text.Format("ui.gacha.roll", 1, rules.CostOf(1));
            _multiLabel.text = Text.Format("ui.gacha.roll", rules.MultiCount, rules.CostOf(rules.MultiCount));
            _closeLabel.text = Text.Get("ui.gacha.close");
            _hintLabel.text = Text.Get("ui.gacha.continue");
            _ratesLabel.text = RatesText();

            _singleButton.onClick.AddListener(() => Roll(1));
            _multiButton.onClick.AddListener(() => Roll(_rules.MultiCount));
            _closeButton.onClick.AddListener(Close);
            _reveal.onClick.AddListener(OnRevealClicked);

            _cardTemplate.gameObject.SetActive(false);
            _reveal.gameObject.SetActive(false);
            _root.SetActive(false);
        }

        public void Open()
        {
            _gotNewUnit = false;
            _messageLabel.text = string.Empty;
            _root.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            if (IsRevealing) return;
            _reveal.gameObject.SetActive(false);
            _root.SetActive(false);
            _closed?.Invoke(_gotNewUnit);
        }

        /// <summary>1회 · 10회 뽑기. 별가루가 모자라면 글만 띄운다. 결과는 바로 저장하고 연출을 시작한다.</summary>
        public GachaResult Roll(int count)
        {
            if (IsRevealing) return GachaResult.Ok;
            var result = Gacha.TryRoll(_profile, _rules, count, out var pulls);
            if (result != GachaResult.Ok)
            {
                _messageLabel.text = Text.Get("ui.gacha.not_enough");
                return result;
            }

            _pulls = pulls;
            _gotNewUnit |= pulls.Any(p => p.IsNew);
            _save();
            _messageLabel.text = string.Empty;
            Refresh();
            _playing = StartCoroutine(Reveal());
            return result;
        }

        /// <summary>연출 중이면 건너뛰어 모두 뒤집고, 다 끝났으면 결과를 닫고 창으로 돌아간다.</summary>
        public void OnRevealClicked()
        {
            if (IsRevealing)
            {
                StopCoroutine(_playing);
                _playing = null;
                FinishReveal();
                return;
            }
            _reveal.gameObject.SetActive(false);
        }

        private void Refresh()
        {
            _currencyLabel.text = Text.Format("ui.gacha.currency", _profile.Currency);
            _singleButton.interactable = _profile.Currency >= _rules.CostOf(1);
            _multiButton.interactable = _profile.Currency >= _rules.CostOf(_rules.MultiCount);
        }

        /// <summary>실제로 나올 수 있는 등급의 확률. 능력자가 없는 등급의 가중치는 대신 나오는 등급에 더한다.</summary>
        private string RatesText()
        {
            var total = _rules.RarityWeights.Values.Sum();
            var effective = new SortedDictionary<int, int>();
            foreach (var pair in _rules.RarityWeights)
            {
                var rarity = Gacha.AvailableRarity(_rules, pair.Key);
                effective[rarity] = (effective.TryGetValue(rarity, out var sum) ? sum : 0) + pair.Value;
            }
            var parts = effective.Reverse().Select(p => Text.Format("ui.gacha.rate", p.Key, Mathf.RoundToInt(p.Value * 100f / total)));
            return string.Join(Text.Get("ui.gacha.rate_joiner"), parts);
        }

        private IEnumerator Reveal()
        {
            _reveal.gameObject.SetActive(true);
            _hintLabel.gameObject.SetActive(false);
            ClearCards();
            var color = _view.GachaRarityColor(Gacha.HighestRarity(_pulls));

            // 1. 빛 예고: 등급 색 마름모가 세 번 맥박치며 커지고 돈다.
            _light.gameObject.SetActive(true);
            var lightImage = _light.GetComponent<Image>();
            for (var t = 0f; t < _view.GachaLightSeconds; t += Step)
            {
                var k = t / _view.GachaLightSeconds;
                var pulse = 0.5f + 0.5f * Mathf.Sin(k * Mathf.PI * 6f - Mathf.PI * 0.5f);
                _light.localScale = Vector3.one * Mathf.Lerp(0.2f, 1.4f, k * k) * (0.85f + 0.15f * pulse);
                _light.localRotation = Quaternion.Euler(0f, 0f, 45f + k * 180f);
                lightImage.color = WithAlpha(color, Mathf.Lerp(0.35f, 1f, pulse));
                yield return null;
            }
            _light.gameObject.SetActive(false);

            // 2. 번쩍: 화면을 등급 색으로 덮었다가 걷힌다. 그동안 카드가 가운데 뒷면으로 생긴다.
            BuildCards();
            for (var t = 0f; t < FlashSeconds; t += Step)
            {
                _flash.color = WithAlpha(color, 1f - t / FlashSeconds);
                yield return null;
            }
            _flash.color = WithAlpha(color, 0f);

            // 3. 펼침: 가운데에서 제자리로.
            for (var t = 0f; t < _view.GachaSpreadSeconds; t += Step)
            {
                var k = 1f - Mathf.Pow(1f - t / _view.GachaSpreadSeconds, 3f);
                for (var i = 0; i < _cards.Count; i++) _cards[i].Rect.anchoredPosition = Vector2.Lerp(Vector2.zero, SlotPosition(i), k);
                yield return null;
            }
            for (var i = 0; i < _cards.Count; i++) _cards[i].Rect.anchoredPosition = SlotPosition(i);

            // 4. 한 장씩 뒤집기: 가로로 접혔다가 앞면으로 펴진다.
            for (var i = 0; i < _cards.Count; i++)
            {
                var rect = _cards[i].Rect;
                var half = _view.GachaFlipSeconds * 0.5f;
                for (var t = 0f; t < half; t += Step)
                {
                    rect.localScale = new Vector3(1f - t / half, 1f, 1f);
                    yield return null;
                }
                _cards[i].ShowFace(_view, _pulls[i]);
                for (var t = 0f; t < half; t += Step)
                {
                    rect.localScale = new Vector3(t / half, 1f, 1f);
                    yield return null;
                }
                rect.localScale = Vector3.one;
                if (i + 1 < _cards.Count) yield return new WaitForSecondsRealtime(_view.GachaFlipIntervalSeconds);
            }

            _playing = null;
            FinishReveal();
        }

        /// <summary>연출 끝 상태: 모든 카드가 제자리에서 앞면, 빛 · 번쩍 없음, "눌러서 계속".</summary>
        private void FinishReveal()
        {
            _light.gameObject.SetActive(false);
            _flash.color = WithAlpha(_flash.color, 0f);
            if (_cards.Count != _pulls.Count) BuildCards();
            for (var i = 0; i < _cards.Count; i++)
            {
                _cards[i].Rect.anchoredPosition = SlotPosition(i);
                _cards[i].Rect.localScale = Vector3.one;
                if (!_cards[i].IsFaceUp) _cards[i].ShowFace(_view, _pulls[i]);
            }
            _hintLabel.gameObject.SetActive(true);
        }

        private void BuildCards()
        {
            ClearCards();
            foreach (var _ in _pulls)
            {
                var card = Instantiate(_cardTemplate, _cardArea);
                card.gameObject.SetActive(true);
                card.SetFont(_font);
                card.ShowBack(_view);
                card.Rect.anchoredPosition = Vector2.zero;
                _cards.Add(card);
            }
        }

        private void ClearCards()
        {
            foreach (var card in _cards) Destroy(card.gameObject);
            _cards.Clear();
        }

        /// <summary>카드 자리: 한 줄에 5장, 줄들을 가운데에 맞춘다. 1장이면 한가운데.</summary>
        private Vector2 SlotPosition(int index)
        {
            var size = _cardTemplate.Rect.sizeDelta;
            var count = _pulls.Count;
            var rows = (count + CardsPerRow - 1) / CardsPerRow;
            var row = index / CardsPerRow;
            var inRow = Mathf.Min(CardsPerRow, count - row * CardsPerRow);
            var column = index % CardsPerRow;
            var x = (column - (inRow - 1) * 0.5f) * (size.x + CardGap);
            var y = ((rows - 1) * 0.5f - row) * (size.y + CardGap);
            return new Vector2(x, y);
        }

        /// <summary>연출 한 프레임 시간. 느린 프레임 하나(뽑는 순간 끊김 등)가 빛 예고를 통째로 건너뛰지 않게 1/20초로 묶는다.</summary>
        private static float Step => Mathf.Min(Time.unscaledDeltaTime, MaxStepSeconds);

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
