using System.Collections.Generic;
using UnityEngine;

namespace SevenSteps.Art
{
    // View-only footprint registration. No rule/state dependency, renderer or material.
    public sealed class BoardShapeAContact : MonoBehaviour
    {
        internal static readonly List<BoardShapeAContact> Active = new List<BoardShapeAContact>();
        public Vector2 radius = new Vector2(.40f,.31f);
        void OnEnable(){Active.Add(this);}
        void OnDisable(){Active.Remove(this);}
    }
}
