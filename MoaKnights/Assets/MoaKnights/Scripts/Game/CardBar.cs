using System;
using System.Collections.Generic;
using MoaKnights.Core.Sim;
using MoaKnights.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Text = MoaKnights.Localization.Text;

namespace MoaKnights.Game
{
    /// <summary>화면 아래 가로 카드 줄. 카드는 판의 능력자 목록으로 만들고, 누르면 알리기만 한다.</summary>
    public sealed class CardBar : MonoBehaviour
    {
        private readonly List<Card> _cards = new List<Card>();

        private Simulation _simulation;
        private BattleViewAsset _view;

        /// <summary>카드를 누르기 시작했을 때. 값은 능력자 ID.</summary>
        public event Action<string> CardPressed;

        private sealed class Card
        {
            public UnitDefinition Definition;
            public Image Background;
        }

        public void Bind(Simulation simulation, BattleViewAsset view, TMP_FontAsset font)
        {
            _simulation = simulation;
            _view = view;

            var layout = gameObject.GetComponent<HorizontalLayoutGroup>();
            if (layout != null) layout.spacing = view.CardSpacing;

            foreach (var unit in simulation.Definition.Units) _cards.Add(CreateCard(unit, font));
            Refresh();
        }

        private void Update()
        {
            if (_simulation != null) Refresh();
        }

        private void Refresh()
        {
            foreach (var card in _cards)
            {
                var usable = !_simulation.World.IsDeployed(card.Definition.Id) && _simulation.World.Cost >= card.Definition.Cost;
                card.Background.color = usable ? _view.CardColor : _view.CardDisabledColor;
            }
        }

        private Card CreateCard(UnitDefinition unit, TMP_FontAsset font)
        {
            var go = new GameObject("Card_" + unit.Id, typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(CardPressRelay));
            go.transform.SetParent(transform, false);

            var element = go.GetComponent<LayoutElement>();
            element.preferredWidth = _view.CardSize.x;
            element.preferredHeight = _view.CardSize.y;

            go.GetComponent<CardPressRelay>().Pressed = () => CardPressed?.Invoke(unit.Id);

            // 이름 키는 능력자 ID로 만든다. 표에 있는지는 ProjectRuleTests가 검사한다.
            CreateLabel(go.transform, "Name", Text.Get("unit." + unit.Id + ".name"), font, 24f, _view.CardTextColor, new Vector2(0f, 0.35f), new Vector2(1f, 1f));
            CreateLabel(go.transform, "Cost", Text.Format("ui.card.cost", unit.Cost), font, 36f, _view.CardTextColor, new Vector2(0f, 0f), new Vector2(1f, 0.4f));

            return new Card { Definition = unit, Background = go.GetComponent<Image>() };
        }

        private static void CreateLabel(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            var label = go.GetComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.alignment = TextAlignmentOptions.Center;
            label.color = color;
            label.raycastTarget = false;
        }
    }
}
