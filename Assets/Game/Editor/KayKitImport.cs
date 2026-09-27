using UnityEditor;
using UnityEngine;

namespace Tabletop.EditorTools
{
    /// <summary>
    /// Import settings for the KayKit characters (D-033, CC0): looping clips for idles, walks and runs, and
    /// readable palette textures so the game can recolour clothes per villager.
    /// </summary>
    public sealed class KayKitImport : AssetPostprocessor
    {
        public const string Folder = "Assets/Game/ThirdParty/KayKit_Adventurers/";

        private static bool Loops(string clip) =>
            clip.Contains("Idle") || clip.StartsWith("Walking") || clip.StartsWith("Running") || clip == "Spellcasting" || clip == "Blocking"
            || clip == "Cheer";

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.optimizeGameObjects = false; // hand slots and hats are toggled by name
        }

        private void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var importer = (ModelImporter)assetImporter;
            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            bool changed = false;
            foreach (var c in clips)
            {
                bool loop = Loops(c.name);
                if (c.loopTime != loop) { c.loopTime = loop; changed = true; }
            }
            if (changed || importer.clipAnimations.Length == 0) importer.clipAnimations = clips;
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var importer = (TextureImporter)assetImporter;
            importer.isReadable = true;
            importer.mipmapEnabled = false; // flat palette swatches
            importer.filterMode = FilterMode.Bilinear;
        }

        [MenuItem("Tabletop/Art/Reimport KayKit Characters")]
        public static void Reimport()
        {
            foreach (var guid in AssetDatabase.FindAssets("", new[] { Folder.TrimEnd('/') }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".fbx") || path.EndsWith(".png")) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
        }
    }
}
