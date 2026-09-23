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

        private void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').StartsWith(IconFolder + "/")) return;
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
