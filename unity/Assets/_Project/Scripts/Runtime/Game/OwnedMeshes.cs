using System;
using System.Collections.Generic;
using UnityEngine;

namespace SevenSteps.Game
{
    /// <summary>
    /// Meshes made at runtime for one object (ChapterStage.CopyGround): destroyed with it, so a copy does not leak them.
    /// </summary>
    public sealed class OwnedMeshes : MonoBehaviour
    {
        readonly List<Mesh> meshes = new List<Mesh>();

        public int Count => meshes.Count;

        public void Add(Mesh mesh)
        {
            if (mesh == null) throw new ArgumentNullException(nameof(mesh));
            meshes.Add(mesh);
        }

        void OnDestroy()
        {
            foreach (var m in meshes)
            {
                if (m == null) continue;
                if (Application.isPlaying) Destroy(m);
                else DestroyImmediate(m); // edit-mode tests
            }
            meshes.Clear();
        }
    }
}
