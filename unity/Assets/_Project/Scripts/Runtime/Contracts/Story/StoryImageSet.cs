using System;
using UnityEngine;

namespace SevenSteps.Contracts
{
    /// <summary>
    /// Story picture keys (story.json "images": ending.1..4, card.&lt;chapter&gt;, carving.&lt;chapter&gt;, ...) → Sprite
    /// (docs/architecture.md 16.2, A7). Values are art's. A missing key shows the text without a picture.
    /// scene-validate compares the keys with story.json: missing sprites are warnings while <see cref="required"/> is false
    /// (default; ending pictures land after 11/14) and errors once art turns it on.
    /// </summary>
    [CreateAssetMenu(menuName = "Seven Steps/Story Image Set")]
    public sealed class StoryImageSet : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string key = "";
            public Sprite sprite;
        }

        public Entry[] entries = new Entry[0];
        [Tooltip("Every story.json image key must have a sprite (scene-validate error). Art turns this on when all are delivered.")]
        public bool required;

        /// The sprite for a key, or null (no entry, or the entry has no sprite yet).
        public Sprite Get(string key)
        {
            if (string.IsNullOrEmpty(key) || entries == null) return null;
            foreach (var e in entries) if (e != null && e.key == key) return e.sprite;
            return null;
        }
    }
}
