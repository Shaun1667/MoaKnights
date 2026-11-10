using System.Collections.Generic;
using MoaKnights.Core.Sim;
using UnityEngine;

namespace MoaKnights.Game
{
    /// <summary>배치된 능력자를 이벤트로 만들고 지우며, HP를 따라 그리기만 한다.</summary>
    public sealed class UnitView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private readonly Dictionary<int, Actor> _actors = new Dictionary<int, Actor>();

        private Simulation _simulation;
        private MapView _mapView;
        private BattleViewAsset _view;

        private sealed class Actor
        {
            public UnitModel Model;
            public HealthBar HealthBar;
            public float TopHeight;
            public float Size;
            public Transform Pips;
        }

        public void Bind(Simulation simulation, MapView mapView, BattleViewAsset view)
        {
            _simulation = simulation;
            _mapView = mapView;
            _view = view;

            _simulation.UnitPlaced += OnPlaced;
            _simulation.UnitDefeated += OnRemoved;
            _simulation.UnitRetreated += OnRetreated;
            _simulation.UnitStacked += OnStacked;
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
            _simulation.UnitDefeated -= OnRemoved;
            _simulation.UnitRetreated -= OnRetreated;
            _simulation.UnitStacked -= OnStacked;
        }

        private void LateUpdate()
        {
            if (_simulation == null) return;
            foreach (var unit in _simulation.World.Units)
            {
                if (!_actors.TryGetValue(unit.Id, out var actor)) continue;
                actor.HealthBar.Set(unit.Hp, _simulation.MaxHp(unit));
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
            var actor = new Actor { Model = model, HealthBar = bar, TopHeight = top, Size = visual.Size };
            _actors[unit.Id] = actor;
            ShowStage(actor, unit.Stage);
        }

        private void OnStacked(Unit unit)
        {
            if (_actors.TryGetValue(unit.Id, out var actor)) ShowStage(actor, unit.Stage);
        }

        private void OnRetreated(Unit unit, int refund) => OnRemoved(unit);

        private void OnRemoved(Unit unit)
        {
            if (!_actors.TryGetValue(unit.Id, out var actor)) return;
            _actors.Remove(unit.Id);
            Destroy(actor.Model.Root);
        }

        /// <summary>중첩 단계를 체력바 위 작은 점 개수로 보여 준다. 점은 카메라 가로축으로 늘어선다.</summary>
        private void ShowStage(Actor actor, int stage)
        {
            if (actor.Pips != null) Destroy(actor.Pips.gameObject);

            var pips = new GameObject("StagePips").transform;
            pips.SetParent(actor.Model.Root.transform, false);
            pips.localPosition = Vector3.up * (actor.TopHeight + _view.HpBarSize.y * 5f);
            pips.rotation = Quaternion.Euler(0f, _view.CameraYaw, 0f);
            actor.Pips = pips;

            var size = actor.Size * 0.18f;
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, _view.StagePipColor);
            for (var i = 0; i < stage; i++)
            {
                var pip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pip.name = "Pip";
                Destroy(pip.GetComponent<Collider>());
                pip.transform.SetParent(pips, false);
                pip.transform.localScale = Vector3.one * size;
                pip.transform.localPosition = new Vector3((i - (stage - 1) * 0.5f) * size * 1.8f, 0f, 0f);
                var pipRenderer = pip.GetComponent<Renderer>();
                pipRenderer.sharedMaterial = _view.CellMaterial;
                pipRenderer.SetPropertyBlock(block);
            }
        }
    }
}
