using System;
using System.Collections.Generic;
using System.Linq;
using MoaKnights.Core.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Text = MoaKnights.Localization.Text;

namespace MoaKnights.Game
{
    /// <summary>
    /// 판 시작 전 편성 창. 왼쪽은 능력자 목록(눌러 넣고 빼기), 오른쪽은 정보 탭(고른 능력자 · 이번 판 적), 아래는 출전 버튼.
    /// 수치는 BattleBalance 행을 그대로 읽어 보여 주기만 한다. 글은 모두 표에서 가져온다.
    /// </summary>
    public sealed class FormationPanel : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _countLabel;
        [SerializeField] private RectTransform _rosterArea;
        [SerializeField] private Button _rosterTemplate;
        [SerializeField] private Button _unitTabButton;
        [SerializeField] private TMP_Text _unitTabLabel;
        [SerializeField] private Button _enemyTabButton;
        [SerializeField] private TMP_Text _enemyTabLabel;
        [SerializeField] private TMP_Text _infoLabel;
        [SerializeField] private RectTransform _rangeArea;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private TMP_Text _confirmLabel;
        [SerializeField] private Button _gachaButton;
        [SerializeField] private TMP_Text _gachaLabel;

        private readonly List<string> _deck = new List<string>();
        private readonly Dictionary<string, Image> _cardImages = new Dictionary<string, Image>();
        private readonly List<GameObject> _rangeCells = new List<GameObject>();

        private BattleBalanceAsset _balance;
        private IReadOnlyList<string> _owned;
        private BattleDefinition _battle;
        private BattleViewAsset _view;
        private TMP_FontAsset _font;
        private Action<IReadOnlyList<string>> _confirm;
        private string _focusedUnit;

        public bool IsOpen => _root.activeSelf;

        public void Bind(BattleBalanceAsset balance, BattleDefinition battle, IReadOnlyList<string> deck, IReadOnlyList<string> owned, BattleViewAsset view,
            TMP_FontAsset font, Action<IReadOnlyList<string>> confirm, Action openGacha)
        {
            _balance = balance;
            _owned = owned;
            _battle = battle;
            _view = view;
            _font = font;
            _confirm = confirm;
            _deck.AddRange(deck);

            foreach (var label in new[] { _titleLabel, _countLabel, _unitTabLabel, _enemyTabLabel, _infoLabel, _confirmLabel, _gachaLabel }) label.font = font;
            _gachaLabel.text = Text.Get("ui.gacha.open");
            _gachaButton.onClick.AddListener(() => openGacha());
            _titleLabel.text = Text.Get("ui.formation.title");
            _unitTabLabel.text = Text.Get("ui.formation.tab_unit");
            _enemyTabLabel.text = Text.Get("ui.formation.tab_enemy");
            _confirmLabel.text = Text.Get("ui.formation.confirm");

            _unitTabButton.onClick.AddListener(() => ShowUnit(_focusedUnit));
            _enemyTabButton.onClick.AddListener(ShowEnemies);
            _confirmButton.onClick.AddListener(() => _confirm(_deck.ToArray()));

            BuildRoster();
            _rosterTemplate.gameObject.SetActive(false);
            ShowUnit(_deck.Count > 0 ? _deck[0] : _owned[0]);
            Refresh();
            Close();
        }

        public void Open() => _root.SetActive(true);

        public void Close() => _root.SetActive(false);

        /// <summary>가진 능력자 카드를 두 칸씩 줄지어 놓는다 (목록 순서). 위치는 목록 칸 안에서 계산한다.</summary>
        private void BuildRoster()
        {
            const int columns = 2;
            var size = _rosterTemplate.GetComponent<RectTransform>().sizeDelta;
            var gap = 16f;

            var rows = _balance.Units.Where(u => _owned.Contains(u.Id)).ToArray();
            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                var id = row.Id;
                var card = Instantiate(_rosterTemplate, _rosterArea);
                card.name = "Roster_" + id;
                card.gameObject.SetActive(true);
                card.onClick.RemoveAllListeners();
                card.onClick.AddListener(() => Toggle(id));

                var rect = card.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2((i % columns) * (size.x + gap), -(i / columns) * (size.y + gap));

                var label = card.GetComponentInChildren<TMP_Text>();
                label.font = _font;
                label.color = _view.CardTextColor;
                label.text = UnitName(id) + "\n" + Text.Format("ui.formation.card_info", row.Rarity, RoleName(row.Role), row.Cost);
                _cardImages[id] = card.GetComponent<Image>();
            }

            // 목록 칸(스크롤 내용)의 높이를 카드 줄 수에 맞춘다.
            var lines = (rows.Length + columns - 1) / columns;
            _rosterArea.sizeDelta = new Vector2(_rosterArea.sizeDelta.x, Mathf.Max(0f, lines * (size.y + gap) - gap));
        }

        private void Toggle(string id)
        {
            if (_deck.Contains(id)) _deck.Remove(id);
            else if (_deck.Count < _balance.DeckSize) _deck.Add(id);
            ShowUnit(id);
            Refresh();
        }

        private void Refresh()
        {
            foreach (var pair in _cardImages)
                pair.Value.color = _deck.Contains(pair.Key) ? _view.FormationSelectedColor : _view.CardColor;
            _countLabel.text = Text.Format("ui.formation.count", _deck.Count, _balance.DeckSize);
            _confirmButton.interactable = _deck.Count > 0;
        }

        /// <summary>고른 능력자 정보: 포지션 · 칸 · 코스트, 수치, SP, 단계별 스킬, 그리고 공격 범위 그림.</summary>
        private void ShowUnit(string id)
        {
            _focusedUnit = id;
            var row = _balance.Units.First(u => u.Id == id);
            var healer = row.Role == UnitRole.Healer;

            var lines = new List<string>
            {
                UnitName(id),
                Text.Format("ui.formation.unit_kind", row.Rarity, RoleName(row.Role), Text.Get(row.Terrain == UnitTerrain.Ground ? "ui.terrain.ground" : "ui.terrain.hill"), row.Cost),
                Text.Format("ui.formation.unit_stats", row.MaxHp, Text.Get(healer ? "ui.formation.heal" : "ui.formation.attack"), row.Attack, row.AttackIntervalSeconds, row.BlockCount),
                Text.Format("ui.formation.unit_sp", SpName(row.SpType), row.MaxSp),
            };

            for (var stage = 1; stage <= row.Skills.Length; stage++)
            {
                var skill = row.Skills[stage - 1];
                lines.Add(Text.Format("ui.formation.skill_line", stage, Text.Get("skill." + id + "_" + stage + ".name"), SkillEffects(skill)));
            }

            _infoLabel.text = string.Join("\n", lines);
            DrawRange(row.Range);
        }

        /// <summary>이번 판에 나오는 적: 처음 나오는 순서로 종류마다 한 줄.</summary>
        private void ShowEnemies()
        {
            var order = new List<string>();
            var counts = new Dictionary<string, int>();
            foreach (var spawn in _battle.Spawns)
            {
                if (!counts.ContainsKey(spawn.EnemyId))
                {
                    counts[spawn.EnemyId] = 0;
                    order.Add(spawn.EnemyId);
                }
                counts[spawn.EnemyId]++;
            }

            var lines = new List<string> { Text.Format("ui.formation.enemy_total", _battle.Spawns.Count) };
            foreach (var id in order)
            {
                var row = _balance.Enemies.First(e => e.Id == id);
                lines.Add(Text.Format("ui.formation.enemy_line", Text.Get("enemy." + id + ".name"), counts[id], row.MaxHp, row.SpeedCellsPerSecond, row.Attack, row.LeakDamage));
                var traits = TraitsText(row);
                if (traits.Length > 0) lines.Add(Text.Format("ui.formation.enemy_traits", traits));
            }

            _infoLabel.text = string.Join("\n", lines);
            ClearRange();
        }

        private static string SkillEffects(BattleBalanceAsset.SkillRow skill)
        {
            var parts = new List<string>();
            if (skill.HealPercent > 0) parts.Add(Text.Format("ui.skill.heal", skill.HealPercent));
            if (skill.AttackBonusPercent > 0) parts.Add(Text.Format("ui.skill.attack", skill.AttackBonusPercent, skill.DurationSeconds));
            if (skill.BlockBonus > 0) parts.Add(Text.Format("ui.skill.block", skill.BlockBonus, skill.DurationSeconds));
            return string.Join(Text.Get("ui.formation.joiner"), parts);
        }

        /// <summary>오른쪽(앞)을 기준으로 공격 칸을 작은 네모로 그린다. 능력자 자리는 다른 색.</summary>
        private void DrawRange(string[] rows)
        {
            ClearRange();
            var range = AttackRange.Parse(rows);
            var cells = range.Offsets.Select(o => (o.X, o.Y)).ToList();
            cells.Add((0, 0));

            var minX = cells.Min(c => c.X);
            var maxX = cells.Max(c => c.X);
            var minY = cells.Min(c => c.Y);
            var maxY = cells.Max(c => c.Y);
            const float cell = 30f;
            var originX = -(maxX + minX) * 0.5f * cell;
            var originY = -(maxY + minY) * 0.5f * cell;

            foreach (var (x, y) in cells.Distinct())
            {
                var isOrigin = x == 0 && y == 0;
                var attacks = range.Offsets.Any(o => o.X == x && o.Y == y);
                var go = new GameObject("RangeCell", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_rangeArea, false);
                var rect = go.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(cell - 4f, cell - 4f);
                rect.anchoredPosition = new Vector2(originX + x * cell, originY + y * cell);
                go.GetComponent<Image>().color = isOrigin ? (attacks ? _view.FormationSelectedColor : _view.CardColor) : _view.RangeColor;
                _rangeCells.Add(go);
            }
        }

        private void ClearRange()
        {
            foreach (var go in _rangeCells) Destroy(go);
            _rangeCells.Clear();
        }

        private static string UnitName(string id) => Text.Get("unit." + id + ".name");

        private static string RoleName(UnitRole role)
        {
            switch (role)
            {
                case UnitRole.Tank: return Text.Get("ui.role.tank");
                case UnitRole.Healer: return Text.Get("ui.role.healer");
                default: return Text.Get("ui.role.dealer");
            }
        }

        private static string SpName(SpType type)
        {
            switch (type)
            {
                case SpType.Natural: return Text.Get("ui.sp.natural");
                case SpType.OnAttack: return Text.Get("ui.sp.on_attack");
                default: return Text.Get("ui.sp.on_hit");
            }
        }

        /// <summary>적 특성 글 (6-2). 없으면 빈 글.</summary>
        private static string TraitsText(BattleBalanceAsset.EnemyRow row)
        {
            var parts = new List<string>();
            if (row.Armor > 0) parts.Add(Text.Format("enemy.trait.armor", row.Armor));
            if (row.Flying) parts.Add(Text.Get("enemy.trait.flying"));
            if (row.RangedReach > 0) parts.Add(Text.Format("enemy.trait.ranged", row.RangedReach));
            if (row.HealAmount > 0) parts.Add(Text.Format("enemy.trait.heal", row.HealAmount, row.HealIntervalSeconds));
            if (row.BlockWeight > 1) parts.Add(Text.Format("enemy.trait.heavy", row.BlockWeight));
            if (!string.IsNullOrEmpty(row.SplitInto) && row.SplitCount > 0)
                parts.Add(Text.Format("enemy.trait.split", Text.Get("enemy." + row.SplitInto + ".name"), row.SplitCount));
            return string.Join(Text.Get("enemy.trait.joiner"), parts);
        }
    }
}
