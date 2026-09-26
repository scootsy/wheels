using System.IO;
using Tabletop.Domain;
using Tabletop.Presentation;
using UnityEditor;
using UnityEngine;

namespace Tabletop.EditorTools
{
    /// <summary>Imports Assets/Game/Art/Icons/*.png as UI sprites and builds the IconSet asset.</summary>
    public sealed class IconImport : AssetPostprocessor
    {
        public const string IconFolder = "Assets/Game/Art/Icons";
        public const string IconSetPath = "Assets/Game/Art/IconSet.asset";
        public const string UiFolder = "Assets/Game/Art/UI";

        /// <summary>Nine-slice borders (pixels) for the UI kit; shapes without an entry stretch whole.</summary>
        private static readonly System.Collections.Generic.Dictionary<string, Vector4> UiBorders = new System.Collections.Generic.Dictionary<string, Vector4>
        {
            { "ui_panel", new Vector4(16, 16, 16, 16) },
            { "ui_frame", new Vector4(16, 16, 16, 16) },
            { "ui_keycap", new Vector4(16, 18, 16, 16) },
            { "ui_shadow", new Vector4(34, 34, 34, 34) },
        };

        private void OnPreprocessTexture()
        {
            var path = assetPath.Replace('\\', '/');
            if (path.StartsWith(UiFolder + "/"))
            {
                var ui = (TextureImporter)assetImporter;
                ui.textureType = TextureImporterType.Sprite;
                ui.spriteImportMode = SpriteImportMode.Single;
                ui.alphaIsTransparency = true;
                ui.mipmapEnabled = false;
                ui.textureCompression = TextureImporterCompression.Uncompressed;
                ui.filterMode = FilterMode.Bilinear;
                ui.wrapMode = TextureWrapMode.Clamp;
                if (UiBorders.TryGetValue(Path.GetFileNameWithoutExtension(path), out var border)) ui.spriteBorder = border;
                return;
            }
            if (!path.StartsWith(IconFolder + "/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
        }

        [MenuItem("Tabletop/Setup/4. Create or Refresh Icon Set")]
        public static IconSet CreateIconSet()
        {
            AssetDatabase.Refresh();
            foreach (var file in Directory.GetFiles(IconFolder, "*.png"))
                AssetDatabase.ImportAsset(file.Replace('\\', '/'), ImportAssetOptions.ForceUpdate);
            if (Directory.Exists(UiFolder))
                foreach (var file in Directory.GetFiles(UiFolder, "*.png"))
                    AssetDatabase.ImportAsset(file.Replace('\\', '/'), ImportAssetOptions.ForceUpdate);
            var set = AssetDatabase.LoadAssetAtPath<IconSet>(IconSetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<IconSet>();
                AssetDatabase.CreateAsset(set, IconSetPath);
            }
            Sprite S(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(IconFolder + "/" + name + ".png");
            set.energyA = S("energy_a");
            set.energyB = S("energy_b");
            set.hammer = S("hammer");
            set.xp = S("xp_star");
            set.blank = S("blank");
            set.padlock = S("padlock");
            set.crown = S("crown");
            set.wall = S("wall");
            set.bomb = S("bomb");
            Sprite U(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(UiFolder + "/" + name + ".png");
            set.uiPanel = U("ui_panel");
            set.uiFrame = U("ui_frame");
            set.uiKeycap = U("ui_keycap");
            set.uiCircle = U("ui_circle");
            set.uiRing = U("ui_ring");
            set.uiGlow = U("ui_glow");
            set.uiDiamond = U("ui_diamond");
            set.uiShadow = U("ui_shadow");
            set.units.Clear();
            foreach (var id in new[] { ReferenceContent.Striker, ReferenceContent.Caster, ReferenceContent.Ranger, ReferenceContent.Mason,
                         ReferenceContent.Shade, ReferenceContent.Mender, ReferenceContent.Hexer })
                set.units.Add(new IconSet.UnitIcon { unitId = id, sprite = S("unit_" + id) });
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            Debug.Log("[Tabletop] Icon set " + (set.IsComplete ? "complete" : "INCOMPLETE") + " at " + IconSetPath);
            return set;
        }

        [MenuItem("Tabletop/Setup/5. Assign Icon Set To Scene")]
        public static void AssignToOpenScene()
        {
            var set = CreateIconSet();
            var app = Object.FindAnyObjectByType<MatchApp>();
            if (app == null) { Debug.LogError("[Tabletop] Open MatchPrototype.unity first."); return; }
            var so = new SerializedObject(app);
            so.FindProperty("icons").objectReferenceValue = set;
            so.ApplyModifiedPropertiesWithoutUndo();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(app.gameObject.scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(app.gameObject.scene);
            Debug.Log("[Tabletop] Icon set assigned to MatchApp and scene saved.");
        }
    }
}
