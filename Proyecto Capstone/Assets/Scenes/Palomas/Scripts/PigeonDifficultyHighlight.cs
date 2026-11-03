using UnityEngine;
using UnityEngine.UI;

namespace NeuroFlexVR
{
    /// <summary>A rounded, softly glowing border drawn without tinting the button sprite.</summary>
    [ExecuteAlways]
    [AddComponentMenu("NeuroFlex/Palomas/Borde de dificultad")]
    public class PigeonDifficultyHighlight : MaskableGraphic
    {
        [Header("Borde seleccionado")]
        [SerializeField, Min(0f)] private float cornerRadius = 8f;
        [SerializeField, Min(0.1f)] private float thickness = 1.2f;
        [SerializeField, Min(0f)] private float glowWidth = 3f;
        [SerializeField, Min(0f)] private float inset = 1.5f;

        private const int StepsPerCorner = 8;
        private const int PointCount = StepsPerCorner * 4;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect bounds = rectTransform.rect;
            if (bounds.width <= 0f || bounds.height <= 0f) return;

            float inner = -inset - thickness * 0.5f;
            float outer = -inset + thickness * 0.5f;
            Color transparent = color;
            transparent.a = 0f;
            Color glow = color;
            glow.a *= 0.6f;
            AddBand(vh, bounds, inner - 0.6f, inner, transparent, color);
            AddBand(vh, bounds, inner, outer, color, color);
            if (glowWidth > 0f)
                AddBand(vh, bounds, outer, outer + glowWidth, glow, transparent);
        }

        private void AddBand(VertexHelper vh, Rect bounds, float inner, float outer,
            Color innerColor, Color outerColor)
        {
            int start = vh.currentVertCount;
            for (int i = 0; i < PointCount; i++)
            {
                vh.AddVert(RoundedPoint(bounds, inner, i), innerColor, Vector2.zero);
                vh.AddVert(RoundedPoint(bounds, outer, i), outerColor, Vector2.zero);
            }
            for (int i = 0; i < PointCount; i++)
            {
                int a = start + i * 2;
                int b = start + ((i + 1) % PointCount) * 2;
                vh.AddTriangle(a, b, a + 1);
                vh.AddTriangle(a + 1, b, b + 1);
            }
        }

        private Vector2 RoundedPoint(Rect bounds, float expansion, int index)
        {
            float radius = Mathf.Clamp(cornerRadius + expansion, 0f,
                Mathf.Max(0f, Mathf.Min(bounds.width, bounds.height) * 0.5f + expansion));
            int corner = index / StepsPerCorner;
            float angle = (corner * 90f + (index % StepsPerCorner) * 90f / (StepsPerCorner - 1)) * Mathf.Deg2Rad;
            float x = corner == 0 || corner == 3 ? bounds.xMax + expansion - radius : bounds.xMin - expansion + radius;
            float y = corner < 2 ? bounds.yMax + expansion - radius : bounds.yMin - expansion + radius;
            return new Vector2(x + Mathf.Cos(angle) * radius, y + Mathf.Sin(angle) * radius);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            SetVerticesDirty();
        }
#endif
    }
}
