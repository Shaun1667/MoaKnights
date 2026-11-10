using System.Collections.Generic;
using MoaKnights.Core.Sim;
using UnityEngine;

namespace MoaKnights.Game
{
    /// <summary>배치된 능력자를 이벤트로 만들고 지우며, HP를 따라 그리기만 한다.</summary>
    public sealed class UnitView : MonoBehaviour
    {
        private readonly Dictionary<int, Actor> _actors = new Dictionary<int, Actor>();

        private Simulation _simulation;
        private MapView _mapView;
        private BattleViewAsset _view;

        private sealed class Actor
        {
            public UnitModel Model;
            public HealthBar HealthBar;
            public float TopHeight;
        }

        public void Bind(Simulation simulation, MapView mapView, BattleViewAsset view)
        {
            _simulation = simulation;
            _mapView = mapView;
            _view = view;

            _simulation.UnitPlaced += OnPlaced;
            _simulation.UnitDefeated += OnDefeated;
            foreach (var unit in _simulation.World.Units) OnPlaced(unit);
        }

        /// <summary>능력자 머리 위 월드 좌표. 피해 숫자를 띄울 때 쓴다.</summary>
        public bool TryGetTop(int unitId, out Vector3 top)
        {
            top = default;
            if (!_actors.TryGetValue(unitId, out var actor)) return false;
            top = actor.Model.Root.transform.position + Vector3.up * actor.TopHeight;
            return true;
        }

        private void OnDestroy()
        {
            if (_simulation == null) return;
            _simulation.UnitPlaced -= OnPlaced;
            _simulation.UnitDefeated -= OnDefeated;
        }

        private void LateUpdate()
        {
            if (_simulation == null) return;
            foreach (var unit in _simulation.World.Units)
            {
                if (!_actors.TryGetValue(unit.Id, out var actor)) continue;
                _simulation.Definition.TryGetUnit(unit.DefinitionId, out var definition);
                actor.HealthBar.Set(unit.Hp, definition.Stats.MaxHp);
            }
        }

        private void OnPlaced(Unit unit)
        {
            if (!_view.TryGetUnit(unit.DefinitionId, out var visual))
            {
                Debug.LogError("BattleView has no row for unit " + unit.DefinitionId);
                return;
            }

            var model = UnitModel.Create(transform, "Unit_" + unit.Id, visual, _view);
            model.Place(_mapView.CellTop(unit.Cell), unit.Direction);

            var top = visual.Size * 1.2f;
            var bar = new HealthBar(model.Root.transform, top + _view.HpBarSize.y * 2f, _view.HpBarUnitColor, _view);
            _actors[unit.Id] = new Actor { Model = model, HealthBar = bar, TopHeight = top };
        }

        private void OnDefeated(Unit unit)
        {
            if (!_actors.TryGetValue(unit.Id, out var actor)) return;
            _actors.Remove(unit.Id);
            Destroy(actor.Model.Root);
        }
    }
}
