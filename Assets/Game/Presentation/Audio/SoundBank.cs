using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tabletop.Presentation
{
    /// <summary>
    /// Every sound the game plays, by key (D-033): "music/village", "amb/forest", "sfx/coin", "step/grass"...
    /// Filled by Tabletop → Art → Build World Look from the imported packs. A missing clip is simply silent, so the
    /// game still runs in a checkout without the Asset Store packs.
    /// </summary>
    [CreateAssetMenu(fileName = "SoundBank", menuName = "Tabletop/Sound Bank")]
    public sealed class SoundBank : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string key;
            public AudioClip clip;
            [Range(0f, 1.5f)] public float volume = 1f;
        }

        public List<Entry> entries = new List<Entry>();

        private Dictionary<string, List<Entry>> _byKey;

        /// <summary>One clip for this key (a random one when several share it), or null.</summary>
        public Entry Pick(string key)
        {
            if (_byKey == null)
            {
                _byKey = new Dictionary<string, List<Entry>>();
                foreach (var e in entries)
                {
                    if (e == null || e.clip == null || string.IsNullOrEmpty(e.key)) continue;
                    if (!_byKey.TryGetValue(e.key, out var list)) _byKey[e.key] = list = new List<Entry>();
                    list.Add(e);
                }
            }
            if (!_byKey.TryGetValue(key, out var l) || l.Count == 0) return null;
            return l[UnityEngine.Random.Range(0, l.Count)];
        }

        public bool Has(string key) => Pick(key) != null;

        private void OnValidate() => _byKey = null;
    }
}
