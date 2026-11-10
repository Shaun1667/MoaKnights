using MoaKnights.Core.Sim;
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
        [SerializeField] private StageAsset _stage;
        [SerializeField] private BattleViewAsset _view;
        [SerializeField] private MapView _mapView;
        [SerializeField] private BattleCameraRig _cameraRig;
        [SerializeField] private EnemyView _enemyView;
        [SerializeField] private UnitView _unitView;
        [SerializeField] private CardBar _cardBar;
        [SerializeField] private PlacementController _placement;
        [SerializeField] private BattleHud _hud;
        [SerializeField] private DamageNumbers _damageNumbers;
        [SerializeField] private UnitActionPanel _unitActions;
        [SerializeField] private SpeedControls _speedControls;
        [SerializeField] private TMP_Text _titleLabel;

        private TMP_FontAsset _runtimeFont;
        private TickClock _clock;

        public Simulation Simulation { get; private set; }
        public TickClock Clock => _clock;
        public BattleBalanceAsset Balance => _balance;

        private void Awake()
        {
            Text.Load(TextTable.Parse(_textTable.text), DefaultLanguage);

            var seed = _pendingSeed ?? NewSeed();
            _pendingSeed = null;

            var definition = BattleDefinitionFactory.Create(_stage, _balance);
            Simulation = new Simulation(World.Create(seed, _stage.Id, definition.DefenseHp, definition.Cost.Start), definition);
            _clock = new TickClock(_balance.TicksPerSecond);

            _mapView.Build(definition.Map, _view);
            _cameraRig.Frame(_mapView.WorldBounds, _view);
            _enemyView.Bind(Simulation, _clock, _mapView, _view);
            _unitView.Bind(Simulation, _mapView, _view);

            // 폰트 에셋을 런타임에 만들어 글자 데이터 변경이 커밋에 섞이지 않게 한다.
            _runtimeFont = TMP_FontAsset.CreateFontAsset(_font);
            ShowText(_titleLabel, "ui.title.game");
            _cardBar.Bind(Simulation, _view, _runtimeFont);
            _placement.Bind(Simulation, _mapView, _view, _cameraRig.GetComponent<Camera>(), _cardBar);
            _hud.Bind(Simulation, _view, _runtimeFont, _placement);
            _damageNumbers.Bind(Simulation, _enemyView, _unitView, _view, _runtimeFont, _cameraRig.GetComponent<Camera>());
            _unitActions.Bind(Simulation, _mapView, _unitView, _placement, _view, _runtimeFont, _cameraRig.GetComponent<Camera>());
            _speedControls.Bind(_clock, _runtimeFont);
        }

        private void Update()
        {
            var ticks = _clock.Advance(Time.unscaledDeltaTime);
            for (var i = 0; i < ticks; i++) Simulation.Step();
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
            _pendingSeed = keepSeed ? Simulation.World.Seed : NewSeed();
            SceneManager.LoadScene(gameObject.scene.buildIndex);
        }

        public void DebugSetPaused(bool paused) => _clock.Paused = paused;

        public void DebugSetSpeed(int speed) => _clock.SetSpeed(speed);

        public void DebugAddCost(int amount) => Simulation.World.ChangeCost(amount, Simulation.Definition.Cost);

        public void DebugStepTicks(int ticks)
        {
            for (var i = 0; i < ticks; i++) Simulation.Step();
        }
    }
}
