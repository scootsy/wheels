using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tabletop.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tabletop.EditorTools
{
    /// <summary>
    /// Makes models dropped into Assets/Game/Characters and Assets/Game/Buildings game-ready (D-026):
    /// unpacks textures embedded in the FBX, builds one URP/Lit material per model, and points the model at it.
    /// Generated models (e.g. from AI model tools) reference textures by a temporary path on the machine that made
    /// them, so Unity cannot hook them up on its own. Safe to run again after adding more models.
    /// </summary>
    public static class ModelImportTools
    {
        public static readonly string[] ModelFolders = { "Assets/Game/Characters", "Assets/Game/Buildings" };
        public const string ArtSetPath = "Assets/Game/Art/WorldArt.asset";
        private const string PreviewFolder = "Logs/ModelPreviews";

        [MenuItem("Tabletop/Art/1. Prepare Imported Models")]
        public static void PrepareAll()
        {
            var report = new List<string>();
            foreach (var path in ModelPaths()) report.Add(Prepare(path));
            AssetDatabase.SaveAssets();
            Debug.Log("[Tabletop] Prepared models:\n" + string.Join("\n", report));
        }

        /// <summary>
        /// Creates Assets/Game/Art/WorldArt.asset if needed and makes sure it has an empty slot for every person and
        /// building in the world, so assigning art is just dragging a model onto a named slot. Keeps existing choices.
        /// </summary>
        [MenuItem("Tabletop/Art/3. Update World Art Slots")]
        public static void UpdateArtSet()
        {
            var art = AssetDatabase.LoadAssetAtPath<WorldArtSet>(ArtSetPath);
            if (art == null)
            {
                art = ScriptableObject.CreateInstance<WorldArtSet>();
                AssetDatabase.CreateAsset(art, ArtSetPath);
            }
            // Build the placeholder world in a throwaway preview scene just to learn every name.
            var probeScene = EditorSceneManager.NewPreviewScene();
            try
            {
                var holder = new GameObject("ArtProbe");
                SceneManager.MoveGameObjectToScene(holder, probeScene);
                var layout = new WorldBuilder(new WorldKit(null), null).Build(holder.transform);
                AddMissing(art.people, layout.PersonKeys);
                AddMissing(art.buildings, layout.BuildingKeys);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(probeScene);
            }
            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
        }

        private static void AddMissing(List<WorldArtSet.Slot> slots, IEnumerable<string> keys)
        {
            foreach (var key in keys)
                if (!slots.Any(s => s.who == key)) slots.Add(new WorldArtSet.Slot { who = key });
        }

        [MenuItem("Tabletop/Art/2. Render Model Previews")]
        public static void RenderPreviews()
        {
            Directory.CreateDirectory(PreviewFolder);
            foreach (var path in ModelPaths())
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                File.WriteAllBytes(Path.Combine(PreviewFolder, Key(path) + ".png"), RenderPreview(prefab, 512));
            }
            Debug.Log("[Tabletop] Model previews written to " + Path.GetFullPath(PreviewFolder));
        }

        public static IEnumerable<string> ModelPaths() =>
            AssetDatabase.FindAssets("t:Model", ModelFolders).Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p => p);

        /// <summary>Folder-friendly name: file name without extension or the ".animated" suffix.</summary>
        public static string Key(string modelPath) => Path.GetFileNameWithoutExtension(modelPath).Replace(".animated", "");

        private static string Prepare(string path)
        {
            var dir = Path.GetDirectoryName(path).Replace('\\', '/');
            var key = Key(path);
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);

            // 1. Embedded textures -> <folder>/Textures/<key>/
            var texDir = dir + "/Textures/" + key;
            if (!Directory.Exists(texDir) || !Directory.GetFiles(texDir).Any(f => !f.EndsWith(".meta")))
            {
                Directory.CreateDirectory(texDir);
                importer.ExtractTextures(texDir);
                AssetDatabase.Refresh();
            }
            Texture2D baseMap = null, normalMap = null;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { texDir }))
            {
                var tp = AssetDatabase.GUIDToAssetPath(guid);
                var file = Path.GetFileNameWithoutExtension(tp).ToLowerInvariant();
                bool isNormal = file.Contains("normal");
                var ti = (TextureImporter)AssetImporter.GetAtPath(tp);
                int maxSize = dir.EndsWith("Characters") ? 1024 : 2048; // people are small on screen
                if (ti.textureType != (isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default) || ti.maxTextureSize != maxSize)
                {
                    ti.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                    ti.maxTextureSize = maxSize;
                    ti.textureCompression = TextureImporterCompression.Compressed;
                    ti.SaveAndReimport();
                }
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(tp);
                if (isNormal) normalMap = tex;
                // Generators name the color map texture_0 / Image_0; other Image_N files are extra PBR channels.
                else if (file == "texture_0" || file == "image_0" || baseMap == null) baseMap = tex;
            }

            // 2. One URP/Lit material per model -> <folder>/Materials/<key>.mat
            var matDir = dir + "/Materials";
            Directory.CreateDirectory(matDir);
            var matPath = matDir + "/" + key + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.SetTexture("_BaseMap", baseMap);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.15f);
            mat.SetFloat("_Metallic", 0f);
            mat.SetTexture("_BumpMap", normalMap);
            if (normalMap != null) mat.EnableKeyword("_NORMALMAP"); else mat.DisableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(mat);

            // 3. Point every material inside the FBX at ours; static models need no rig or animation import.
            bool changed = false;
            foreach (var embedded in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
            {
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), embedded.name);
                if (!importer.GetExternalObjectMap().TryGetValue(id, out var existing) || existing != mat)
                {
                    importer.AddRemap(id, mat);
                    changed = true;
                }
            }
            bool animated = path.Contains(".animated");
            if (importer.importCameras || importer.importLights)
            {
                importer.importCameras = false;
                importer.importLights = false;
                changed = true;
            }
            if (!animated && (importer.animationType != ModelImporterAnimationType.None || importer.importAnimation))
            {
                importer.animationType = ModelImporterAnimationType.None;
                importer.importAnimation = false;
                changed = true;
            }
            if (changed) importer.SaveAndReimport();
            return key + ": base=" + (baseMap ? baseMap.name : "none") + " normal=" + (normalMap ? normalMap.name : "none") + (animated ? " (rigged)" : "");
        }

        /// <summary>Renders a model on a neutral background (used for the preview sheet and for choosing who gets which model).</summary>
        public static byte[] RenderPreview(GameObject prefab, int size)
        {
            var pru = new PreviewRenderUtility();
            try
            {
                pru.camera.fieldOfView = 30;
                pru.camera.clearFlags = CameraClearFlags.SolidColor;
                pru.camera.backgroundColor = new Color(0.35f, 0.4f, 0.45f);
                var inst = pru.InstantiatePrefabInScene(prefab);
                var clip = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(prefab)).OfType<AnimationClip>()
                    .FirstOrDefault(c => !c.name.StartsWith("__preview"));
                if (clip != null) clip.SampleAnimation(inst, 0f);
                var b = Bounds(inst);
                float d = b.extents.magnitude / Mathf.Sin(15 * Mathf.Deg2Rad);
                var dir = Quaternion.Euler(15, 200, 0) * Vector3.forward;
                pru.camera.transform.position = b.center + dir * d;
                pru.camera.transform.LookAt(b.center);
                pru.camera.nearClipPlane = 0.01f;
                pru.camera.farClipPlane = d * 4;
                pru.lights[0].intensity = 1.2f;
                pru.lights[0].transform.rotation = Quaternion.Euler(40, 160, 0);
                pru.ambientColor = new Color(0.5f, 0.5f, 0.5f);
                pru.BeginPreview(new Rect(0, 0, size, size), GUIStyle.none);
                pru.Render(true);
                var rt = (RenderTexture)pru.EndPreview();
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                var png = tex.EncodeToPNG();
                Object.DestroyImmediate(tex);
                return png;
            }
            finally
            {
                pru.Cleanup();
            }
        }

        private static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            var b = rs.Length > 0 ? rs[0].bounds : new Bounds(go.transform.position, Vector3.one);
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }
    }
}
