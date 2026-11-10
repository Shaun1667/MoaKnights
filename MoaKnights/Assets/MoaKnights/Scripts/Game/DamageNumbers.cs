using System.Collections.Generic;
using MoaKnights.Core.Sim;
using TMPro;
using UnityEngine;
using Text = MoaKnights.Localization.Text;

namespace MoaKnights.Game
{
    /// <summary>맞은 쪽 머리 위에 피해 숫자를 띄워 올리고 지운다. 적이 맞은 것과 능력자가 맞은 것은 색으로 나눈다.</summary>
    public sealed class DamageNumbers : MonoBehaviour
    {
        private readonly List<Floating> _active = new List<Floating>();

        private Simulation _simulation;
        private EnemyView _enemyView;
        private UnitView _unitView;
        private BattleViewAsset _view;
        private TMP_FontAsset _font;
        private Camera _camera;

        private sealed class Floating
        {
            public TextMeshPro Label;
            public Vector3 Start;
            public float Age;
        }

        public void Bind(Simulation simulation, EnemyView enemyView, UnitView unitView, BattleViewAsset view, TMP_FontAsset font, Camera worldCamera)
        {
            _simulation = simulation;
            _enemyView = enemyView;
            _unitView = unitView;
            _view = view;
            _font = font;
            _camera = worldCamera;

            _simulation.EnemyDamaged += OnEnemyDamaged;
            _simulation.UnitDamaged += OnUnitDamaged;
        }

        private void OnDestroy()
        {
            if (_simulation == null) return;
            _simulation.EnemyDamaged -= OnEnemyDamaged;
            _simulation.UnitDamaged -= OnUnitDamaged;
        }

        private void OnEnemyDamaged(Enemy enemy, int damage)
        {
            if (_enemyView.TryGetTop(enemy.Id, out var top)) Spawn(top, damage, _view.DamageToEnemyColor);
        }

        private void OnUnitDamaged(Unit unit, int damage)
        {
            if (_unitView.TryGetTop(unit.Id, out var top)) Spawn(top, damage, _view.DamageToUnitColor);
        }

        private void Spawn(Vector3 position, int damage, Color color)
        {
            var go = new GameObject("Damage", typeof(TextMeshPro));
            go.transform.SetParent(transform, false);
            var label = go.GetComponent<TextMeshPro>();
            label.font = _font;
            label.text = Text.Format("ui.battle.damage", damage);
            label.fontSize = _view.FloatingNumberSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = color;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            _active.Add(new Floating { Label = label, Start = position });
            Place(_active[_active.Count - 1]);
        }

        private void LateUpdate()
        {
            for (var i = _active.Count - 1; i >= 0; i--)
            {
                var floating = _active[i];
                floating.Age += Time.unscaledDeltaTime;
                if (floating.Age >= _view.FloatingNumberSeconds)
                {
                    Destroy(floating.Label.gameObject);
                    _active.RemoveAt(i);
                    continue;
                }
                Place(floating);
            }
        }

        private void Place(Floating floating)
        {
            var t = floating.Age / _view.FloatingNumberSeconds;
            floating.Label.transform.position = floating.Start + Vector3.up * (_view.FloatingNumberRise * t);
            floating.Label.transform.rotation = _camera.transform.rotation;

            var color = floating.Label.color;
            color.a = 1f - t * t;
            floating.Label.color = color;
        }
    }
}
