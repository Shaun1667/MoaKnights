using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Text = MoaKnights.Localization.Text;

namespace MoaKnights.Game
{
    /// <summary>스테이지 목록 창. 버튼은 StageCatalog 순서로 런타임에 만든다. 모두 열려 있다 (잠금 · 저장은 Phase 5).</summary>
    public sealed class StageSelectPanel : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private RectTransform _list;
        [SerializeField] private Button _closeButton;
        [SerializeField] private TMP_Text _closeLabel;

        public bool IsOpen => _root.activeSelf;

        public void Bind(StageCatalogAsset catalog, int currentIndex, TMP_FontAsset font, Action<int> load)
        {
            _titleLabel.font = font;
            _closeLabel.font = font;
            _titleLabel.text = Text.Get("ui.select.title");
            _closeLabel.text = Text.Get("ui.select.close");
            _closeButton.onClick.AddListener(Close);

            for (var i = 0; i < catalog.Count; i++)
            {
                var index = i;
                var button = Instantiate(_closeButton, _list);
                button.name = "Stage_" + catalog[i].Id;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => load(index));
                button.interactable = true;

                var label = button.GetComponentInChildren<TMP_Text>();
                label.font = font;

                // 스테이지 이름 키는 스테이지 ID로 만든다. 표에 있는지는 ProjectRuleTests가 검사한다.
                var name = Text.Get("stage." + catalog[i].Id + ".name");
                label.text = index == currentIndex ? Text.Format("ui.select.current", name) : name;
            }

            Close();
        }

        public void Open() => _root.SetActive(true);

        public void Close() => _root.SetActive(false);
    }
}
