using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tabletop.World
{
    /// <summary>
    /// The world's art (D-033): pack prefabs by name, terrain layers, character bodies and animation clips, sky and
    /// post-processing. Filled by Tabletop → Art → Build World Look. Anything missing falls back to the primitive
    /// placeholders, so a checkout without the (uncommitted) Asset Store packs still builds and plays.
    /// </summary>
    [CreateAssetMenu(fileName = "WorldLook", menuName = "Tabletop/World Look")]
    public sealed class WorldLook : ScriptableObject
    {
        [Serializable]
        public sealed class PrefabEntry
        {
            public string key;
            public GameObject prefab;
        }

        [Serializable]
        public sealed class ClipEntry
        {
            public string key;
            public AnimationClip clip;
        }

        [Serializable]
        public sealed class TextureEntry
        {
            public string key;
            public Texture2D texture;
        }

        [Header("Terrain (grass, meadow, dirt, stone, rock)")]
        public TerrainLayer grass;
        public TerrainLayer meadow;
        public TerrainLayer dirt;
        public TerrainLayer stone;
        public TerrainLayer rock;
        public Material terrainMaterial;

        [Header("Sky and mood")]
        public Material skybox;
        public VolumeProfile postProcessing;

        [Header("Prefabs by name (houses, trees, rocks, furniture, character bodies, props)")]
        public List<PrefabEntry> prefabs = new List<PrefabEntry>();

        [Header("Character animation clips by name (shared KayKit rig)")]
        public List<ClipEntry> clips = new List<ClipEntry>();

        [Header("Character palette textures by body name")]
        public List<TextureEntry> textures = new List<TextureEntry>();

        private Dictionary<string, GameObject> _prefabs;
        private Dictionary<string, AnimationClip> _clips;
        private Dictionary<string, Texture2D> _textures;

        public GameObject Prefab(string key)
        {
            if (_prefabs == null)
            {
                _prefabs = new Dictionary<string, GameObject>();
                foreach (var e in prefabs) if (e != null && e.prefab != null && !string.IsNullOrEmpty(e.key)) _prefabs[e.key] = e.prefab;
            }
            return key != null && _prefabs.TryGetValue(key, out var p) ? p : null;
        }

        public AnimationClip Clip(string key)
        {
            if (_clips == null)
            {
                _clips = new Dictionary<string, AnimationClip>();
                foreach (var e in clips) if (e != null && e.clip != null && !string.IsNullOrEmpty(e.key)) _clips[e.key] = e.clip;
            }
            return key != null && _clips.TryGetValue(key, out var c) ? c : null;
        }

        public Texture2D Texture(string key)
        {
            if (_textures == null)
            {
                _textures = new Dictionary<string, Texture2D>();
                foreach (var e in textures) if (e != null && e.texture != null && !string.IsNullOrEmpty(e.key)) _textures[e.key] = e.texture;
            }
            return key != null && _textures.TryGetValue(key, out var t) ? t : null;
        }

        public bool HasTerrain => grass != null && dirt != null;
        public bool HasCharacters => Prefab("Knight") != null && Clip("Idle") != null;

        private void OnValidate()
        {
            _prefabs = null;
            _clips = null;
            _textures = null;
        }
    }
}
