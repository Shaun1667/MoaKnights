using System.Linq;
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

        // 씬을 다시 불러올 때 넘겨주는 값: 시드 · 스테이지 번호 · 편성 (다시 · 다음 · 스테이지 선택 · 출전 · 디버그 재시작).
        private static int? _pendingSeed;
        private static int? _pendingStageIndex;

        // 고른 편성은 판을 다시 불러와도 이어진다 (저장은 Phase 5). 편성 창을 건너뛰는 것은 "출전"으로 다시 불러올 때뿐이다.
        private static string[] _deck;
        private static bool _skipFormation;

        [SerializeField] private TextAsset _textTable;
        [SerializeField] private Font _font;
        [SerializeField] private BattleBalanceAsset _balance;
        [SerializeField] private StageCatalogAsset _catalog;
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
        [SerializeField] private ResultPanel _result;
        [SerializeField] private StageSelectPanel _stageSelect;
        [SerializeField] private FormationPanel _formation;
        [SerializeField] private TMP_Text _titleLabel;

        private TMP_FontAsset _runtimeFont;
        private TickClock _clock;

        public Simulation Simulation { get; private set; }
        public TickClock Clock => _clock;
        public BattleBalanceAsset Balance => _balance;
        public StageCatalogAsset Catalog => _catalog;
        public int StageIndex { get; private set; }
        public System.Collections.Generic.IReadOnlyList<string> Deck => _deck;

        private void Awake()
        {
            Text.Load(TextTable.Parse(_textTable.text), DefaultLanguage);

            var seed = _pendingSeed ?? NewSeed();
            _pendingSeed = null;
            StageIndex = _catalog.Clamp(_pendingStageIndex ?? 0);
            _pendingStageIndex = null;

            if (!BattleDefinitionFactory.IsValidDeck(_balance, _deck)) _deck = BattleDefinitionFactory.DefaultDeck(_balance);
            var showFormation = !_skipFormation;
            _skipFormation = false;

            var stage = _catalog[StageIndex];
            var definition = BattleDefinitionFactory.Create(stage, _balance, _deck);
            Simulation = new Simulation(World.Create(seed, stage.Id, definition.DefenseHp, definition.Cost.Start), definition);
            _clock = new TickClock(_balance.TicksPerSecond);

            _mapView.Build(definition.Map, _view);
            _cameraRig.Frame(_mapView.WorldBounds, _view);
            _enemyView.Bind(Simulation, _clock, _mapView, _view);
            _unitView.Bind(Simulation, _mapView, _view);

            // 폰트 에셋을 런타임에 만들어 글자 데이터 변경이 커밋에 섞이지 않게 한다.
            _runtimeFont = TMP_FontAsset.CreateFontAsset(_font);
            _titleLabel.font = _runtimeFont;
            _titleLabel.text = StageName(StageIndex);
            _cardBar.Bind(Simulation, _view, _runtimeFont);
            _placement.Bind(Simulation, _mapView, _view, _cameraRig.GetComponent<Camera>(), _cardBar);
            _hud.Bind(Simulation, _view, _runtimeFont, _placement);
            _damageNumbers.Bind(Simulation, _enemyView, _unitView, _view, _runtimeFont, _cameraRig.GetComponent<Camera>());
            _unitActions.Bind(Simulation, _mapView, _unitView, _placement, _view, _runtimeFont, _cameraRig.GetComponent<Camera>());
            _speedControls.Bind(_clock, _runtimeFont);
            _stageSelect.Bind(_catalog, StageIndex, _runtimeFont, LoadStage);
            _result.Bind(Simulation, _view, _runtimeFont, StageIndex + 1 < _catalog.Count,
                () => LoadStage(StageIndex), () => LoadStage(StageIndex + 1), _stageSelect.Open);
            _formation.Bind(_balance, definition, _deck, _view, _runtimeFont, ConfirmFormation);

            // 편성 창이 떠 있는 동안 시간은 멈춰 있다.
            if (showFormation)
            {
                _clock.Paused = true;
                _formation.Open();
            }
        }

        /// <summary>출전: 편성이 같으면 창만 닫고 시작, 다르면 그 편성으로 판을 다시 연다 (카드 줄이 편성을 따라야 하므로).</summary>
        private void ConfirmFormation(System.Collections.Generic.IReadOnlyList<string> deck)
        {
            if (deck.SequenceEqual(_deck))
            {
                _formation.Close();
                _clock.Paused = false;
                return;
            }

            _deck = deck.ToArray();
            _skipFormation = true;
            _pendingSeed = NewSeed();
            _pendingStageIndex = StageIndex;
            SceneManager.LoadScene(gameObject.scene.buildIndex);
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

        // 스테이지 이름 키는 스테이지 ID로 만든다. 표에 있는지는 ProjectRuleTests가 검사한다.
        private string StageName(int index) => Text.Get("stage." + _catalog[index].Id + ".name");

        private static int NewSeed() => System.Environment.TickCount;

        /// <summary>스테이지를 새 시드로 처음부터 연다. 다시 하기도 같은 스테이지를 여는 것이다.</summary>
        public void LoadStage(int index)
        {
            _pendingSeed = NewSeed();
            _pendingStageIndex = _catalog.Clamp(index);
            SceneManager.LoadScene(gameObject.scene.buildIndex);
        }

        public void DebugRestart(bool keepSeed)
        {
            _pendingSeed = keepSeed ? Simulation.World.Seed : NewSeed();
            _pendingStageIndex = StageIndex;
            _skipFormation = true;
            SceneManager.LoadScene(gameObject.scene.buildIndex);
        }

        public void DebugSetPaused(bool paused) => _clock.Paused = paused;

        public void DebugSetSpeed(int speed) => _clock.SetSpeed(speed);

        public void DebugAddCost(int amount) => Simulation.World.ChangeCost(amount, Simulation.Definition.Cost);

        public void DebugDamageDefense(int amount) => Simulation.World.DamageDefense(amount);

        public void DebugStepTicks(int ticks)
        {
            for (var i = 0; i < ticks; i++) Simulation.Step();
        }
    }
}
