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
            public Image Portrait;
            public TMP_Text CostLabel;
            public string ShownCost;
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

        /// <summary>
        /// 카드 아래 글: 판 위에 있으면 중첩 코스트(최대면 "최대"), 재배치 대기 중이면 남은 초, 아니면 배치 코스트.
        /// 지금 쓸 수 있으면 밝은 색, 아니면 회색.
        /// </summary>
        private void Refresh()
        {
            var world = _simulation.World;
            var deploy = _simulation.Definition.Deploy;
            foreach (var card in _cards)
            {
                var id = card.Definition.Id;
                string text;
                bool usable;

                var placed = world.UnitOf(id);
                var waitTicks = _simulation.RedeployTicksLeft(id);
                if (placed != null)
                {
                    var maxed = placed.Stage >= deploy.MaxStage;
                    text = maxed ? Text.Get("ui.card.max_stack") : Text.Format("ui.card.stack", deploy.StackCost(card.Definition));
                    usable = _simulation.CheckStack(id) == PlacementResult.Ok;
                }
                else if (waitTicks > 0)
                {
                    var tps = _simulation.Definition.TicksPerSecond;
                    text = Text.Format("ui.card.cooldown", (waitTicks + tps - 1) / tps);
                    usable = false;
                }
                else
                {
                    var cost = _simulation.DeployCost(id);
                    text = Text.Format("ui.card.cost", cost);
                    usable = world.Cost >= cost;
                }

                card.Background.color = usable ? _view.CardColor : _view.CardDisabledColor;
                if (card.Portrait != null) card.Portrait.color = usable ? Color.white : _view.CardPortraitDisabledColor;
                if (card.ShownCost == text) continue;
                card.ShownCost = text;
                card.CostLabel.text = text;
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

            // 얼굴이 있으면 위쪽에 얼굴, 그 아래 이름 · 코스트. 없으면 예전처럼 이름과 코스트만.
            Image portrait = null;
            var hasPortrait = _view.TryGetUnit(unit.Id, out var visual) && visual.Portrait != null;
            if (hasPortrait) portrait = CreatePortrait(go.transform, visual.Portrait);

            // 이름 키는 능력자 ID로 만든다. 표에 있는지는 ProjectRuleTests가 검사한다.
            var name = Text.Get("unit." + unit.Id + ".name");
            var costText = Text.Format("ui.card.cost", unit.Cost);
            TMP_Text cost;
            if (hasPortrait)
            {
                CreateLabel(go.transform, "Name", name, font, 20f, _view.CardTextColor, new Vector2(0f, 0.18f), new Vector2(1f, 0.34f));
                cost = CreateLabel(go.transform, "Cost", costText, font, 28f, _view.CardTextColor, new Vector2(0f, 0f), new Vector2(1f, 0.2f));
            }
            else
            {
                CreateLabel(go.transform, "Name", name, font, 24f, _view.CardTextColor, new Vector2(0f, 0.35f), new Vector2(1f, 1f));
                cost = CreateLabel(go.transform, "Cost", costText, font, 36f, _view.CardTextColor, new Vector2(0f, 0f), new Vector2(1f, 0.4f));
            }

            return new Card { Definition = unit, Background = go.GetComponent<Image>(), Portrait = portrait, CostLabel = cost };
        }

        private static Image CreatePortrait(Transform parent, Sprite sprite)
        {
            var go = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.06f, 0.33f);
            rect.anchorMax = new Vector2(0.94f, 0.98f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text CreateLabel(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color, Vector2 anchorMin, Vector2 anchorMax)
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
            return label;
        }
    }
}
