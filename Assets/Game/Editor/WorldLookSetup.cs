using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tabletop.Presentation;
using Tabletop.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Tabletop.EditorTools
{
    /// <summary>
    /// Builds the world's look and sound (D-033) from the imported packs: terrain layers, sky, colour grading, every
    /// usable prefab by name, the KayKit characters and their clips, and the sound bank. Then points World.unity
    /// and MatchPrototype.unity at them. Safe to run again after importing or removing a pack.
    /// </summary>
    public static class WorldLookSetup
    {
        public const string Folder = "Assets/Game/Art/Look";
        public const string LookPath = Folder + "/WorldLook.asset";
        public const string SoundsPath = Folder + "/SoundBank.asset";
        public const string FigurinesPath = Folder + "/Figurines.asset";

        private static readonly string[] PrefabFolders =
        {
            "Assets/Art/Prefabs/Houses", "Assets/Art/Prefabs/Foliage", "Assets/Art/Meshes/Village_Scatter",
            "Assets/Polytope Studio/Lowpoly_Environments/Prefabs", "Assets/Polytope Studio/Lowpoly_Village/Prefabs",
            "Assets/ithappy/Cute_Furniture_Free/Prefabs", KayKitImport.Folder.TrimEnd('/'),
        };

        [MenuItem("Tabletop/Art/Build World Look and Sounds")]
        public static void Build()
        {
            Directory.CreateDirectory(Folder);
            var look = LoadOrCreate<WorldLook>(LookPath);
            var sounds = LoadOrCreate<SoundBank>(SoundsPath);

            // Terrain.
            // (The pack's "Dirt_A" is really patchy grass, so paths use its sandy soil.)
            look.grass = Layer("Grass", "Assets/TerrainTexturesPackFree/TerrainTextures/GrassUV01.png", "Assets/TerrainTexturesPackFree/TerrainTextures/GrassUV01_N.png", 8f, new Color(0.95f, 1f, 0.85f));
            look.meadow = Layer("Meadow", "Assets/Terrain/Textures/Grass_A/Grass_A_BaseColor.tif", "Assets/Terrain/Textures/Grass_A/Grass_A_Normal.tif", 11f, new Color(0.9f, 0.95f, 0.8f));
            look.dirt = Layer("Dirt", "Assets/Terrain/Textures/Sand_A/Sand_A_BaseColor.tif", "Assets/Terrain/Textures/Sand_A/Sand_A_Normal.tif", 6f, new Color(0.85f, 0.78f, 0.68f));
            look.stone = Layer("Stone", "Assets/TerrainTexturesPackFree/TerrainTextures/GroundStones01.png", "Assets/TerrainTexturesPackFree/TerrainTextures/GroundStones01_N.png", 5f, new Color(1f, 0.97f, 0.92f));
            look.rock = Layer("Rock", "Assets/Terrain/Textures/Cliffs_A/Cliffs_A_BaseColor.tif", "Assets/Terrain/Textures/Cliffs_A/Cliffs_A_Normal.tif", 14f, new Color(0.62f, 0.6f, 0.56f));
            // The land is built at runtime, so the build only keeps shader variants this material asks for:
            // normal-mapped layers, no instancing.
            look.terrainMaterial = MaterialAsset("TerrainLit", "Universal Render Pipeline/Terrain/Lit", m =>
            {
                m.enableInstancing = false;
                m.EnableKeyword("_NORMALMAP");
            });
            look.skybox = MaterialAsset("Sky", "Skybox/Procedural", m =>
            {
                m.SetFloat("_SunSize", 0.035f);
                m.SetFloat("_AtmosphereThickness", 0.85f);
                m.SetColor("_SkyTint", new Color(0.55f, 0.68f, 0.9f));
                m.SetColor("_GroundColor", new Color(0.45f, 0.5f, 0.42f));
                m.SetFloat("_Exposure", 1.25f);
            });
            look.postProcessing = PostProcessing();

            // Prefabs by name: only the ones the world uses, or the build would carry whole packs.
            look.prefabs.Clear();
            var used = new HashSet<string>(WorldBuilder.PrefabKeysUsed);
            var seen = new HashSet<string>();
            foreach (var folder in PrefabFolders.Where(AssetDatabase.IsValidFolder))
                foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { folder }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.Contains("/Compositions/")) continue;
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (go == null) continue;
                    var key = Path.GetFileNameWithoutExtension(path);
                    if (!used.Contains(key)) continue;
                    // A prefab wins over the model of the same name.
                    if (!seen.Add(key))
                    {
                        if (!path.EndsWith(".prefab")) continue;
                        look.prefabs.RemoveAll(e => e.key == key);
                    }
                    look.prefabs.Add(new WorldLook.PrefabEntry { key = key, prefab = go });
                }

            // Characters: palette textures and the shared clips (from the Knight; every KayKit body uses the same rig).
            look.textures.Clear();
            foreach (var t in new[] { "knight", "barbarian", "mage", "rogue" })
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(KayKitImport.Folder + "Textures/" + t + "_texture.png");
                if (tex != null) look.textures.Add(new WorldLook.TextureEntry { key = t, texture = tex });
            }
            look.clips.Clear();
            foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(KayKitImport.Folder + "Knight.fbx").OfType<AnimationClip>())
                if (!clip.name.StartsWith("__preview")) look.clips.Add(new WorldLook.ClipEntry { key = clip.name, clip = clip });
            EditorUtility.SetDirty(look);

            BuildSounds(sounds);
            BuildFigurines(LoadOrCreate<FigurineSet>(FigurinesPath));
            CapTextures(look);
            AssetDatabase.SaveAssets();
            AssignToScenes(look, sounds);
            Debug.Log("[Tabletop] World look: " + look.prefabs.Count + " prefabs, " + look.clips.Count + " clips, terrain " + look.HasTerrain
                      + "; sounds: " + sounds.entries.Count);
        }

        /// <summary>
        /// The packs ship 4K atlases; the camera never gets close enough to need them. Cap everything the world uses at
        /// 2K (1K on iPhone / iPad) so builds stay small and phones keep their memory.
        /// </summary>
        private static void CapTextures(WorldLook look)
        {
            var roots = look.prefabs.Where(e => e.prefab != null).Select(e => AssetDatabase.GetAssetPath(e.prefab)).ToArray();
            int changed = 0;
            foreach (var dep in AssetDatabase.GetDependencies(roots, true))
            {
                if (!(AssetImporter.GetAtPath(dep) is TextureImporter ti)) continue;
                bool dirty = false;
                if (ti.maxTextureSize > 2048) { ti.maxTextureSize = 2048; dirty = true; }
                var ios = ti.GetPlatformTextureSettings("iPhone");
                if (!ios.overridden || ios.maxTextureSize > 1024)
                {
                    ios.overridden = true;
                    ios.maxTextureSize = 1024;
                    ios.format = TextureImporterFormat.Automatic;
                    ti.SetPlatformTextureSettings(ios);
                    dirty = true;
                }
                if (dirty) { ti.SaveAndReimport(); changed++; }
            }
            Debug.Log("[Tabletop] Texture caps applied to " + changed + " textures.");
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            a = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        private static TerrainLayer Layer(string name, string diffusePath, string normalPath, float tile, Color tint)
        {
            // The packs ship 4K-8K textures; 2K is plenty for a terrain seen from above (and for phones).
            foreach (var p in new[] { diffusePath, normalPath })
                if (AssetImporter.GetAtPath(p) is TextureImporter ti && ti.maxTextureSize > 2048)
                {
                    ti.maxTextureSize = 2048;
                    ti.SaveAndReimport();
                }
            var diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(diffusePath);
            if (diffuse == null) return null;
            if (AssetImporter.GetAtPath(normalPath) is TextureImporter ni && ni.textureType != TextureImporterType.NormalMap)
            {
                ni.textureType = TextureImporterType.NormalMap;
                ni.SaveAndReimport();
            }
            var path = Folder + "/" + name + ".terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, path);
            }
            layer.diffuseTexture = diffuse;
            layer.normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            layer.normalScale = 0.7f;
            layer.tileSize = new Vector2(tile, tile);
            layer.smoothness = 0f;
            layer.metallic = 0f;
            // Tint through the remap; alpha remapped to 0 so no texture alpha reads as shine.
            layer.diffuseRemapMin = Vector4.zero;
            layer.diffuseRemapMax = new Vector4(tint.r, tint.g, tint.b, 0f);
            EditorUtility.SetDirty(layer);
            return layer;
        }

        private static Material MaterialAsset(string name, string shader, System.Action<Material> setup)
        {
            var s = Shader.Find(shader);
            if (s == null) { Debug.LogWarning("[Tabletop] Shader missing: " + shader); return null; }
            var path = Folder + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(s);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = s;
            setup?.Invoke(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Warm, gently graded daylight: filmic tonemapping, a little bloom, a soft vignette.</summary>
        private static VolumeProfile PostProcessing()
        {
            var path = Folder + "/WorldPost.asset";
            var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (p == null)
            {
                p = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(p, path);
            }
            foreach (var c in p.components.ToList()) { p.Remove(c.GetType()); Object.DestroyImmediate(c, true); }
            T Add<T>() where T : VolumeComponent
            {
                var c = p.Add<T>(true);
                c.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(c, p);
                return c;
            }
            var tone = Add<Tonemapping>();
            tone.mode.Override(TonemappingMode.Neutral);
            var bloom = Add<Bloom>();
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(0.35f);
            bloom.scatter.Override(0.6f);
            var color = Add<ColorAdjustments>();
            color.postExposure.Override(0.15f);
            color.contrast.Override(14f);
            color.saturation.Override(14f);
            var wb = Add<WhiteBalance>();
            wb.temperature.Override(8f);
            var vig = Add<Vignette>();
            vig.intensity.Override(0.22f);
            vig.smoothness.Override(0.5f);
            EditorUtility.SetDirty(p);
            return p;
        }

        // ------------------------------------------------------------------ sounds

        private const string Kenney = "Assets/Game/ThirdParty/KenneyAudio/";
        private const string Music = "Assets/Game/ThirdParty/Music/";
        private const string Fk = "Assets/Audio/AudioFiles/";

        private static void BuildSounds(SoundBank bank)
        {
            bank.entries.Clear();
            void Add(string key, string path, float volume = 1f, bool stream = false)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null) return;
                if (AssetImporter.GetAtPath(path) is AudioImporter imp)
                {
                    var s = imp.defaultSampleSettings;
                    var load = stream ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                    if (s.loadType != load || s.compressionFormat != AudioCompressionFormat.Vorbis)
                    {
                        s.loadType = load;
                        s.compressionFormat = AudioCompressionFormat.Vorbis;
                        s.quality = stream ? 0.55f : 0.7f;
                        imp.defaultSampleSettings = s;
                        imp.forceToMono = !stream;
                        imp.SaveAndReimport();
                    }
                }
                bank.entries.Add(new SoundBank.Entry { key = key, clip = clip, volume = volume });
            }
            void AddAll(string key, string folder, string prefix, float volume = 1f)
            {
                if (!AssetDatabase.IsValidFolder(folder.TrimEnd('/'))) return;
                foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folder.TrimEnd('/') }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (Path.GetFileNameWithoutExtension(path).StartsWith(prefix)) Add(key, path, volume);
                }
            }

            // Music (OpenGameArt, CC0).
            Add("music/village", Music + "Minstrel_Dance_0.mp3", 1f, true);
            Add("music/town", Music + "Minstrel_Dance_0.mp3", 1f, true);
            Add("music/road", Music + "The_Bards_Tale.mp3", 1f, true);
            Add("music/outpost", Music + "The_Bards_Tale.mp3", 1f, true);
            Add("music/hall", Music + "Kings_Feast_0.mp3", 1f, true);
            Add("music/table", Music + "The_Old_Tower_Inn.mp3", 0.85f, true);
            Add("music/hollow", Music + "The_Bards_Tale.mp3", 0.8f, true);
            Add("music/tournament", Music + "Kings_Feast_0.mp3", 1f, true);
            // Ambience (Fantasy Kingdom pack, the creative director's licence).
            Add("amb/village_calm", Fk + "03_amb/village_calm.wav", 0.8f, true);
            Add("amb/village_busy", Fk + "03_amb/village_busy.wav", 0.7f, true);
            Add("amb/forest", Fk + "03_amb/birdforest_amb.wav", 0.9f, true);
            Add("amb/wind", Fk + "03_amb/skywind_1.wav", 0.7f, true);
            Add("amb/lake", Fk + "03_amb/watershore_amb.wav", 0.8f, true);
            // Effects (Kenney, CC0; stone steps from Fantasy Kingdom).
            AddAll("sfx/click", Kenney + "ui", "click", 0.6f);
            AddAll("sfx/coins", Kenney + "rpg", "handleCoins");
            AddAll("sfx/chips", Kenney + "casino", "chips-handle");
            AddAll("sfx/pickup", Kenney + "rpg", "beltHandle");
            AddAll("sfx/page", Kenney + "rpg", "bookFlip");
            AddAll("sfx/door", Kenney + "rpg", "doorOpen");
            AddAll("step/grass", Kenney + "rpg", "footstep");
            AddAll("step/stone", Fk + "05_sfx", "steps_single", 0.6f);
            if (!bank.Has("step/stone")) AddAll("step/stone", Kenney + "rpg", "footstep");
            AddAll("sfx/spin", Kenney + "casino", "dice-shake");
            AddAll("sfx/lock", Kenney + "rpg", "metalClick");
            AddAll("sfx/lever", Kenney + "rpg", "metalLatch");
            AddAll("sfx/activate", Kenney + "casino", "chip-lay");
            AddAll("sfx/crown_hit", Kenney + "rpg", "chop");
            AddAll("sfx/bulwark_hit", Kenney + "rpg", "metalPot");
            AddAll("sfx/build", Kenney + "casino", "chips-stack");
            AddAll("sfx/rankup", Kenney + "ui", "switch10");
            AddAll("sfx/heal", Kenney + "ui", "rollover2");
            AddAll("sfx/bomb", Kenney + "rpg", "metalPot1");
            AddAll("sfx/energy", Kenney + "ui", "switch1", 0.5f);
            AddAll("sfx/win", Kenney + "rpg", "handleCoins2");
            AddAll("sfx/lose", Kenney + "rpg", "dropLeather");
            EditorUtility.SetDirty(bank);
        }

        // ------------------------------------------------------------------ figurines

        /// <summary>The source's figurines on KayKit bodies: Warrior = Knight, Mage = Mage, Archer = Rogue...</summary>
        private static void BuildFigurines(FigurineSet set)
        {
            set.entries.Clear();
            var clips = new Dictionary<string, AnimationClip>();
            foreach (var c in AssetDatabase.LoadAllAssetsAtPath(KayKitImport.Folder + "Knight.fbx").OfType<AnimationClip>()) clips[c.name] = c;
            void Add(string unit, string body, string pose, float t, bool hat, bool cape, params string[] keep)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(KayKitImport.Folder + body + ".fbx");
                if (model == null) return;
                clips.TryGetValue(pose, out var clip);
                set.entries.Add(new FigurineSet.Entry { unitId = unit, model = model, pose = clip, poseTime = t, hat = hat, cape = cape, keep = keep.ToList() });
            }
            Add(Tabletop.Domain.ReferenceContent.Striker, "Knight", "Block", 0.4f, true, true, "1H_Sword", "Round_Shield");
            Add(Tabletop.Domain.ReferenceContent.Caster, "Mage", "Spellcast_Raise", 0.8f, true, true, "2H_Staff");
            Add(Tabletop.Domain.ReferenceContent.Ranger, "Rogue", "2H_Ranged_Aiming", 0.5f, true, true, "2H_Crossbow");
            Add(Tabletop.Domain.ReferenceContent.Mason, "Barbarian", "2H_Melee_Idle", 0.3f, true, true, "2H_Axe");
            Add(Tabletop.Domain.ReferenceContent.Shade, "RogueHooded", "Dualwield_Melee_Attack_Stab", 0.2f, true, true, "Knife", "Knife_Offhand");
            Add(Tabletop.Domain.ReferenceContent.Mender, "Mage", "Spellcasting", 0.2f, false, true, "Spellbook_open");
            Add(Tabletop.Domain.ReferenceContent.Hexer, "Mage", "Spellcast_Shoot", 0.4f, true, false, "1H_Wand");
            EditorUtility.SetDirty(set);
        }

        // ------------------------------------------------------------------ scenes

        private static void AssignToScenes(WorldLook look, SoundBank sounds)
        {
            var active = EditorSceneManager.GetActiveScene().path;
            foreach (var scenePath in new[] { WorldSceneSetup.WorldScenePath, ProjectSetup.ScenePath })
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                // Opening a scene unloads assets nothing references: load them again afterwards.
                look = AssetDatabase.LoadAssetAtPath<WorldLook>(LookPath);
                sounds = AssetDatabase.LoadAssetAtPath<SoundBank>(SoundsPath);
                bool changed = false;
                foreach (var app in Object.FindObjectsByType<WorldApp>(FindObjectsInactive.Include))
                {
                    var so = new SerializedObject(app);
                    so.FindProperty("look").objectReferenceValue = look;
                    so.FindProperty("sounds").objectReferenceValue = sounds;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }
                foreach (var app in Object.FindObjectsByType<MatchApp>(FindObjectsInactive.Include))
                {
                    var so = new SerializedObject(app);
                    so.FindProperty("sounds").objectReferenceValue = sounds;
                    so.FindProperty("figurines").objectReferenceValue = AssetDatabase.LoadAssetAtPath<FigurineSet>(FigurinesPath);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }
                if (changed) EditorSceneManager.SaveScene(scene);
            }
            if (!string.IsNullOrEmpty(active)) EditorSceneManager.OpenScene(active, OpenSceneMode.Single);
        }
    }
}
