using UnityEngine;
using UnityEngine.UI;
namespace SevenSteps.Art.Review
{
    public sealed class ReviewStarGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            var c = r.center;
            vh.AddVert(c, color, Vector2.zero);
            for (int j = 0; j < 16; j++)
            {
                float a = Mathf.PI / 2 + j * Mathf.PI / 8;
                float radius = (j % 2 == 0 ? 1 : .27f) * r.width * .5f;
                vh.AddVert(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, color, Vector2.zero);
            }
            for (int j = 0; j < 16; j++)
                vh.AddTriangle(0, j + 1, (j + 1) % 16 + 1);
        }
    }
}
