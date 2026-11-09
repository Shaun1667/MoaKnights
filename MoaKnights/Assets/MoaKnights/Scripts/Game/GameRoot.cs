using MoaKnights.Core;
using MoaKnights.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MoaKnights.Game
{
    /// <summary>컴포지션 루트. 씬의 모든 연결은 여기서 한다.</summary>
    public sealed class GameRoot : MonoBehaviour
    {
        private const string DefaultLanguage = "ko";

        // 재시작을 넘어 시드를 넘겨주는 유일한 정적 값.
        private static int? _pendingSeed;

        [SerializeField] private TextAsset _textTable;
        [SerializeField] private Font _font;
        [SerializeField] private BattleBalanceAsset _balance;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _statusLabel;

        private TMP_FontAsset _runtimeFont;

        public RunContext Run { get; private set; }
        public BattleBalanceAsset Balance => _balance;

        private void Awake()
        {
            Text.Load(TextTable.Parse(_textTable.text), DefaultLanguage);

            Run = new RunContext(_pendingSeed ?? NewSeed());
            _pendingSeed = null;

            // 폰트 에셋을 런타임에 만들어 글자 데이터 변경이 커밋에 섞이지 않게 한다.
            _runtimeFont = TMP_FontAsset.CreateFontAsset(_font);
            ShowText(_titleLabel, "ui.title.game");
            ShowText(_statusLabel, "ui.boot.ready");
        }

        private void OnDestroy()
        {
            if (_runtimeFont != null) Destroy(_runtimeFont);
        }

        private void ShowText(TMP_Text label, string key)
        {
            label.font = _runtimeFont;
            label.text = Text.Get(key);
        }

        private static int NewSeed() => System.Environment.TickCount;

        public void DebugRestart(bool keepSeed)
        {
            _pendingSeed = keepSeed ? Run.Seed : NewSeed();
            SceneManager.LoadScene(gameObject.scene.buildIndex);
        }
    }
}
