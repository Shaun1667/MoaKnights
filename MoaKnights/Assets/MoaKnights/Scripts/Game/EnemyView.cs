using System.Collections.Generic;
using MoaKnights.Core.Sim;
using UnityEngine;

namespace MoaKnights.Game
{
    /// <summary>적을 이벤트로 만들고 지우며, 매 프레임 규칙의 진행도와 HP를 따라 그리기만 한다.</summary>
    public sealed class EnemyView : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private readonly Dictionary<int, Actor> _actors = new Dictionary<int, Actor>();

        private Simulation _simulation;
        private TickClock _clock;
        private MapView _mapView;
        private BattleViewAsset _view;

        private sealed class Actor
        {
            public Transform Transform;
            public float BaseOffset;
            public float TopHeight;
            public HealthBar HealthBar;
        }

        public void Bind(Simulation simulation, TickClock clock, MapView mapView, BattleViewAsset view)
        {
            _simulation = simulation;
            _clock = clock;
            _mapView = mapView;
            _view = view;

            _simulation.EnemySpawned += OnSpawned;
            _simulation.EnemyLeaked += OnRemoved;
            _simulation.EnemyKilled += OnRemoved;
            foreach (var enemy in _simulation.World.Enemies) OnSpawned(enemy);
        }

        /// <summary>적 머리 위 월드 좌표. 피해 숫자를 띄울 때 쓴다.</summary>
        public bool TryGetTop(int enemyId, out Vector3 top)
        {
            top = default;
            if (!_actors.TryGetValue(enemyId, out var actor)) return false;
            top = actor.Transform.position + Vector3.up * (actor.TopHeight - actor.BaseOffset);
            return true;
        }

        private void OnDestroy()
        {
            if (_simulation == null) return;
            _simulation.EnemySpawned -= OnSpawned;
            _simulation.EnemyLeaked -= OnRemoved;
            _simulation.EnemyKilled -= OnRemoved;
        }

        private void LateUpdate()
        {
            if (_simulation == null) return;

            var definition = _simulation.Definition;
            foreach (var enemy in _simulation.World.Enemies)
            {
                if (!_actors.TryGetValue(enemy.Id, out var actor)) continue;

                // 틱 사이를 메워 부드럽게 보이게 한다. 막힌 적은 움직이지 않는다. 규칙의 진행도는 바꾸지 않는다.
                var enemyDefinition = definition.Enemy(enemy.DefinitionId);
                var lead = enemy.IsBlocked ? 0 : Mathf.RoundToInt(enemyDefinition.SpeedPerTick * _clock.TickFraction);
                definition.Routes[enemy.RouteIndex].Locate(enemy.Progress + lead, definition.UnitsPerCell, out var from, out var to, out var t);

                var position = Vector3.Lerp(_mapView.CellTop(from), _mapView.CellTop(to), t);
                actor.Transform.position = position + Vector3.up * actor.BaseOffset;
                actor.HealthBar.Set(enemy.Hp, enemyDefinition.Stats.MaxHp);
            }
        }

        private void OnSpawned(Enemy enemy)
        {
            if (!_view.TryGetEnemy(enemy.DefinitionId, out var visual))
            {
                Debug.LogError("BattleView has no row for enemy " + enemy.DefinitionId);
                return;
            }

            var actor = visual.Prefab != null ? CreateFromPrefab(enemy, visual) : CreateGreybox(enemy, visual);
            actor.HealthBar = new HealthBar(actor.Transform, actor.TopHeight - actor.BaseOffset + _view.HpBarSize.y * 2f, _view.HpBarEnemyColor, _view);
            _actors[enemy.Id] = actor;
        }

        private void OnRemoved(Enemy enemy)
        {
            if (!_actors.TryGetValue(enemy.Id, out var actor)) return;
            _actors.Remove(enemy.Id);
            Destroy(actor.Transform.gameObject);
        }

        private Actor CreateFromPrefab(Enemy enemy, BattleViewAsset.EnemyVisual visual)
        {
            var instance = Instantiate(visual.Prefab, transform);
            instance.name = "Enemy_" + enemy.Id;
            return new Actor { Transform = instance.transform, BaseOffset = 0f, TopHeight = visual.Size * 2f };
        }

        private Actor CreateGreybox(Enemy enemy, BattleViewAsset.EnemyVisual visual)
        {
            var root = new GameObject("Enemy_" + enemy.Id).transform;
            root.SetParent(transform, false);

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(root, false);
            body.transform.localScale = Vector3.one * visual.Size;

            var bodyRenderer = body.GetComponent<Renderer>();
            bodyRenderer.sharedMaterial = _view.CellMaterial;
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColor, visual.Color);
            bodyRenderer.SetPropertyBlock(block);

            // 유니티 캡슐은 높이가 크기의 2배이고 중심이 가운데라, 크기만큼 올려야 바닥에 닿는다.
            return new Actor { Transform = root, BaseOffset = visual.Size, TopHeight = visual.Size * 2f };
        }
    }
}
