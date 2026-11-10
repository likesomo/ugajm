using UnityEngine;

namespace SevenSteps.Art
{
    // Shared Blender-authored geometry; no rules or stage state live in the kit.
    public sealed class BoardShapeAKit : ScriptableObject
    {
        public Mesh[] crust, menhirs;
        public Mesh kerb, corner, medallion, chest, backGate, wall, joist;
        public Mesh rubble, fragment, grass;
        public Mesh hollowArch, terrace, frostSpire, timberFrame, mossPatch;
    }
}
