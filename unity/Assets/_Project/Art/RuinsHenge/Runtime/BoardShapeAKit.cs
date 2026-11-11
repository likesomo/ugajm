using UnityEngine;

namespace SevenSteps.Art
{
    // Shared Blender-authored geometry; no rules or stage state live in the kit.
    public sealed class BoardShapeAKit : ScriptableObject
    {
        public Mesh[] crust, menhirs;
        public Mesh kerb, plainKerb, corner, medallion, chest, backGate, wall, joist;
        public Mesh rubble, fragment, crumbleShard, grass;
        public Mesh hollowArch, hollowStar, terrace, frostSpire, timberFrame, mossPatch;
        public Mesh hollowCrust, hollowKerb, carvedKerb, carvedKerbMarker, altarMenhir, joineryKerb, joineryCrust;
        public Mesh dressingJoineryKerb;
    }
}
