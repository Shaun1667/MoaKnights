using UnityEngine;

namespace MoaKnights.Game
{
    /// <summary>머리 위 체력바. 상자 두 개로 그리고, 카메라 쪽 가로축에 맞춰 눕힌다.</summary>
    public sealed class HealthBar
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private readonly Transform _fill;
        private readonly Renderer _fillRenderer;
        private readonly MaterialPropertyBlock _fillBlock = new MaterialPropertyBlock();
        private readonly float _width;
        private float _shown = -1f;
        private Color _shownColor;

        public HealthBar(Transform parent, float height, Color fillColor, BattleViewAsset view)
        {
            var size = view.HpBarSize;
            _width = size.x;

            var root = new GameObject("HealthBar").transform;
            root.SetParent(parent, false);
            root.localPosition = Vector3.up * height;
            root.rotation = Quaternion.Euler(0f, view.CameraYaw, 0f);

            CreatePart(root, "Back", new Vector3(size.x, size.y, size.y), Vector3.zero, view.HpBarBackColor, view.CellMaterial);
            _fill = CreatePart(root, "Fill", new Vector3(size.x, size.y * 1.2f, size.y * 1.2f), Vector3.zero, fillColor, view.CellMaterial);
            _fillRenderer = _fill.GetComponent<Renderer>();
            _shownColor = fillColor;
        }

        public void SetColor(Color color)
        {
            if (color == _shownColor) return;
            _shownColor = color;
            _fillBlock.SetColor(BaseColor, color);
            _fillRenderer.SetPropertyBlock(_fillBlock);
        }

        public void Set(int hp, int maxHp)
        {
            var ratio = Mathf.Clamp01(maxHp > 0 ? hp / (float)maxHp : 0f);
            if (Mathf.Approximately(ratio, _shown)) return;
            _shown = ratio;

            var scale = _fill.localScale;
            scale.x = Mathf.Max(0.0001f, _width * ratio);
            _fill.localScale = scale;
            _fill.localPosition = new Vector3(-(1f - ratio) * _width * 0.5f, 0f, 0f);
        }

        private static Transform CreatePart(Transform parent, string name, Vector3 scale, Vector3 position, Color color, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            Object.Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localScale = scale;
            part.transform.localPosition = position;

            var partRenderer = part.GetComponent<Renderer>();
            partRenderer.sharedMaterial = material;
            partRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColor, color);
            partRenderer.SetPropertyBlock(block);
            return part.transform;
        }
    }
}
