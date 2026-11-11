using SevenSteps.Core;
using UnityEngine;
namespace SevenSteps.Art.Review
{
    public sealed class FootStarsView
    {
        readonly ReviewChoiceController v; public FootStarsView(ReviewChoiceController view)
        {
            v = view;
        }
        public void Draw(State s)
        {
            var p = v.Cell(s.Pos) + new Vector2(-60, -47);
            for (int j = 0; j < 7; j++)
            {
                float size = j == 6 && s.Used == 6 ? 20 + 3 * Mathf.Sin(Time.realtimeSinceStartup * 8) : 16;
                v.Star(p + new Vector2(j * 20, 0), size, v.Scarf);
                v.Star(p + new Vector2(j * 20, 0), size - 4, j < s.Used ? v.Ink : new Color(.06f, .10f, .13f));
            }
        }
    }
}
