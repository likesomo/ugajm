using System.Collections.Generic;
using SevenSteps.Core;
using UnityEngine;
namespace SevenSteps.Art.Review
{
    public sealed class StarTrailReviewView
    {
        readonly ReviewChoiceController v; public StarTrailReviewView(ReviewChoiceController view)
        {
            v = view;
        }
        void Route(State s, Color c, bool fixedTrail)
        {
            Vector2? prev = null;
            foreach (var f in s.Trail)
            {
                var p = v.Cell(f.Cell);
                if (prev.HasValue)
                {
                    var elbow = new Vector2(p.x, prev.Value.y);
                    Glow(prev.Value, elbow, c);
                    Glow(elbow, p, c);
                }
                v.Star(p, fixedTrail ? 10 : 12, c);
                v.Star(p, 4, v.Ink);
                prev = p;
            }
        }
        void Glow(Vector2 a, Vector2 b, Color c)
        {
            v.Line(a, b, new Color(.02f, .05f, .07f, .9f), 7);
            v.Line(a, b, new Color(c.r, c.g, c.b, .25f), 6);
            v.Line(a, b, c, 3);
        }
        public void Draw(State s)
        {
            var seen = new HashSet<int>();
            for (var h = s; h?.Prev != null; h = h.Prev)
            {
                if (h.Prev.Status == Status.Choose && h.Bodies < h.Prev.Bodies && seen.Add(h.Prev.Bodies))
                {
                    Route(h.Prev, v.BodyColor(s.Level.Bodies - h.Prev.Bodies), true);
                    if (h.Start != h.Prev.Start)
                    {
                        var p = v.Cell(h.Start);
                        Glow(p - new Vector2(15, 12), p + new Vector2(15, 12), v.Next);
                        Glow(p - new Vector2(15, -12), p + new Vector2(15, -12), v.Scarf);
                    }
                }
            }
            Route(s, v.Scarf, false);
            foreach (var stone in s.Statues)
                v.Star(v.Cell(stone) + new Vector2(0, 25), 16, v.Ink);
            if (s.Used == 7)
            {
                var p = v.Cell(s.Pos);
                v.Star(p + new Vector2(0, 45), 24, v.Ink);
                v.Line(p, p + new Vector2(0, 39), new Color(v.Scarf.r, v.Scarf.g, v.Scarf.b, .7f), 3);
            }
        }
    }
}
