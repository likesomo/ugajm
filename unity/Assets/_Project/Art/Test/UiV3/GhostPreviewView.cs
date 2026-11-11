using SevenSteps.Core;
using UnityEngine;
namespace SevenSteps.Art.Review
{
    public sealed class GhostPreviewView
    {
        readonly ReviewChoiceController v; public GhostPreviewView(ReviewChoiceController view)
        {
            v = view;
        }
        public int NextPosition(State s, bool keep) => keep ? s.Start : s.Pos; public void Draw(State s, bool keep)
        {
            var c = v.Next;
            c.a = .55f;
            if (keep)
            {
                v.Figure(s.Pos, new Color(.8f, .85f, .8f, .38f), true);
                var a = v.Cell(s.Pos);
                var b = v.Cell(s.Start);
                for (float t = 0; t < 1; t += .065f)
                    v.Star(Vector2.Lerp(a, b, t), 4, v.Next);
            }
            v.Figure(NextPosition(s, keep), c);
            if (!keep)
            {
                var p = v.Cell(s.Pos);
                v.Line(p + new Vector2(-20, -18), p + new Vector2(20, 18), v.Next, 5);
                v.Line(p + new Vector2(-20, 18), p + new Vector2(20, -18), v.Next, 5);
            }
        }
    }
}
