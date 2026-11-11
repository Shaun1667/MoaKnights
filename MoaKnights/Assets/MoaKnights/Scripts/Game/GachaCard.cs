using MoaKnights.Core.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Text = MoaKnights.Localization.Text;

namespace MoaKnights.Game
{
    /// <summary>뽑기 결과 카드 한 장. 뒷면(어두운 색)과 앞면(등급 색 테두리 · 얼굴 · 이름 · 등급 · 새로 얻음/돌려받음)을 바꿔 보여 준다.</summary>
    public sealed class GachaCard : MonoBehaviour
    {
        [SerializeField] private Image _frame;
        [SerializeField] private Image _inner;
        [SerializeField] private GameObject _face;
        [SerializeField] private Image _portrait;
        [SerializeField] private TMP_Text _nameLabel;
        [SerializeField] private TMP_Text _rarityLabel;
        [SerializeField] private TMP_Text _tagLabel;

        public RectTransform Rect => (RectTransform)transform;
        public bool IsFaceUp { get; private set; }

        public void SetFont(TMP_FontAsset font)
        {
            foreach (var label in new[] { _nameLabel, _rarityLabel, _tagLabel }) label.font = font;
        }

        public void ShowBack(BattleViewAsset view)
        {
            IsFaceUp = false;
            _frame.color = view.StarOffColor;
            _inner.color = view.GachaCardBackColor;
            _face.SetActive(false);
        }

        public void ShowFace(BattleViewAsset view, GachaPull pull)
        {
            IsFaceUp = true;
            var rarityColor = view.GachaRarityColor(pull.Rarity);
            _frame.color = rarityColor;
            _inner.color = view.CardColor;
            _face.SetActive(true);

            var hasPortrait = view.TryGetUnit(pull.UnitId, out var visual) && visual.Portrait != null;
            _portrait.enabled = hasPortrait;
            if (hasPortrait) _portrait.sprite = visual.Portrait;

            _nameLabel.color = view.CardTextColor;
            _nameLabel.text = Text.Get("unit." + pull.UnitId + ".name");
            _rarityLabel.color = rarityColor;
            _rarityLabel.text = Text.Format("ui.gacha.rarity", pull.Rarity);
            _tagLabel.color = pull.IsNew ? view.DefeatColor : view.CardTextColor;
            _tagLabel.text = pull.IsNew ? Text.Get("ui.gacha.new") : Text.Format("ui.gacha.refund", pull.Refund);
        }
    }
}
