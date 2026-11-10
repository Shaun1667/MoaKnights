using MoaKnights.Core.Sim;
using UnityEngine;

namespace MoaKnights.Game
{
    /// <summary>능력자 하나의 모습. 배치된 능력자와 배치 미리보기가 같은 모습을 쓴다.</summary>
    public sealed class UnitModel
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private readonly Renderer _body;
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private readonly Color _color;

        public GameObject Root { get; }

        private UnitModel(GameObject root, Renderer body, Color color)
        {
            Root = root;
            _body = body;
            _color = color;
        }

        public static UnitModel Create(Transform parent, string name, BattleViewAsset.UnitVisual visual, BattleViewAsset view)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);

            if (visual.Prefab != null)
            {
                Object.Instantiate(visual.Prefab, root.transform);
                return new UnitModel(root, null, visual.Color);
            }

            // 그레이박스: 몸체 상자와, 바라보는 쪽(+x)으로 튀어나온 납작한 방향 표시.
            var size = visual.Size;
            var body = CreatePart(root.transform, "Body", new Vector3(size, size * 1.2f, size), new Vector3(0f, size * 0.6f, 0f), visual.Color, view.CellMaterial);
            CreatePart(root.transform, "Facing", new Vector3(size * 0.6f, size * 0.15f, size * 0.25f), new Vector3(size * 0.65f, size * 0.6f, 0f), view.DirectionMarkerColor, view.CellMaterial);
            return new UnitModel(root, body, visual.Color);
        }

        public void Place(Vector3 cellTop, Direction direction)
        {
            Root.transform.SetPositionAndRotation(cellTop, Rotation(direction));
        }

        /// <summary>미리보기 색. null이면 원래 색으로 돌린다.</summary>
        public void Tint(Color? color)
        {
            if (_body == null) return;
            _block.SetColor(BaseColor, color ?? _color);
            _body.SetPropertyBlock(_block);
        }

        /// <summary>격자 Right(+x)가 회전 0이고, Up(+y 격자 = 월드 +z)은 y축으로 -90도다.</summary>
        public static Quaternion Rotation(Direction direction) => Quaternion.Euler(0f, -90f * (int)direction, 0f);

        private static Renderer CreatePart(Transform parent, string name, Vector3 scale, Vector3 position, Color color, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            Object.Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localScale = scale;
            part.transform.localPosition = position;

            var partRenderer = part.GetComponent<Renderer>();
            partRenderer.sharedMaterial = material;
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColor, color);
            partRenderer.SetPropertyBlock(block);
            return partRenderer;
        }
    }
}
