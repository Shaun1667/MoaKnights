using System.Collections.Generic;
using MoaKnights.Core.Sim;
using UnityEngine;

namespace MoaKnights.Game
{
    /// <summary>배치된 능력자를 이벤트로 만들어 그리기만 한다.</summary>
    public sealed class UnitView : MonoBehaviour
    {
        private readonly Dictionary<int, UnitModel> _models = new Dictionary<int, UnitModel>();

        private Simulation _simulation;
        private MapView _mapView;
        private BattleViewAsset _view;

        public void Bind(Simulation simulation, MapView mapView, BattleViewAsset view)
        {
            _simulation = simulation;
            _mapView = mapView;
            _view = view;

            _simulation.UnitPlaced += OnPlaced;
            foreach (var unit in _simulation.World.Units) OnPlaced(unit);
        }

        private void OnDestroy()
        {
            if (_simulation != null) _simulation.UnitPlaced -= OnPlaced;
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
            _models[unit.Id] = model;
        }
    }
}
