using System.Collections.Generic;
using System.IO;
using Tabletop.World;
using UnityEditor;
using UnityEngine;

namespace Tabletop.EditorTools
{
    /// <summary>
    /// "Comment out" imported models without deleting them (D-030). Shelving moves a model assignment from
    /// WorldArt.asset onto a shelf asset inside an Editor folder: the slot falls back to its placeholder, and
    /// because nothing the game uses references the model any more, it is left out of every build. The model
    /// files stay in the project (and in Git); Restore puts the assignments back exactly as they were.
    /// </summary>
    public static class ArtShelf
    {
        public const string ShelfPath = "Assets/Game/Editor/Art/ShelvedArt.asset";

        [MenuItem("Tabletop/Art/Shelve Imported People (use placeholders)")]
        public static void ShelvePeople() => Shelve(people: true);

        [MenuItem("Tabletop/Art/Shelve Imported Buildings (use placeholders)")]
        public static void ShelveBuildings() => Shelve(people: false);

        [MenuItem("Tabletop/Art/Restore Shelved Models")]
        public static void RestoreAll()
        {
            var art = Art();
            var shelf = Shelf(create: false);
            if (shelf == null) { Debug.Log("[Tabletop] Nothing is shelved."); return; }
            int n = Move(shelf.people, art.people) + Move(shelf.buildings, art.buildings);
            Save(art, shelf);
            Debug.Log("[Tabletop] Restored " + n + " shelved model(s).");
        }

        /// <summary>Who is currently shelved (for tests and the build log).</summary>
        public static IEnumerable<string> Shelved()
        {
            var shelf = Shelf(create: false);
            if (shelf == null) yield break;
            foreach (var s in shelf.people) if (s.model != null) yield return s.who;
            foreach (var s in shelf.buildings) if (s.model != null) yield return s.who;
        }

        private static void Shelve(bool people)
        {
            var art = Art();
            var shelf = Shelf(create: true);
            int n = people ? Move(art.people, shelf.people) : Move(art.buildings, shelf.buildings);
            Save(art, shelf);
            Debug.Log("[Tabletop] Shelved " + n + " " + (people ? "people" : "building") + " model(s); placeholders are back.");
        }

        /// <summary>Moves every assigned model from one slot list to the other, keyed by who.</summary>
        private static int Move(List<WorldArtSet.Slot> from, List<WorldArtSet.Slot> to)
        {
            int n = 0;
            foreach (var src in from)
            {
                if (src == null || src.model == null) continue;
                var dst = to.Find(s => s != null && s.who == src.who);
                if (dst == null) { dst = new WorldArtSet.Slot { who = src.who }; to.Add(dst); }
                dst.model = src.model;
                dst.pose = src.pose;
                dst.poseTime = src.poseTime;
                dst.loop = src.loop;
                dst.turn = src.turn;
                dst.size = src.size;
                // Clear everything that points into the model file, so builds don't pull it in.
                src.model = null;
                src.pose = null;
                n++;
            }
            return n;
        }

        private static WorldArtSet Art()
        {
            var art = AssetDatabase.LoadAssetAtPath<WorldArtSet>(ModelImportTools.ArtSetPath);
            if (art == null) throw new System.InvalidOperationException("Missing " + ModelImportTools.ArtSetPath);
            return art;
        }

        private static WorldArtSet Shelf(bool create)
        {
            var shelf = AssetDatabase.LoadAssetAtPath<WorldArtSet>(ShelfPath);
            if (shelf != null || !create) return shelf;
            Directory.CreateDirectory(Path.GetDirectoryName(ShelfPath));
            shelf = ScriptableObject.CreateInstance<WorldArtSet>();
            AssetDatabase.CreateAsset(shelf, ShelfPath);
            return shelf;
        }

        private static void Save(WorldArtSet art, WorldArtSet shelf)
        {
            EditorUtility.SetDirty(art);
            if (shelf != null) EditorUtility.SetDirty(shelf);
            AssetDatabase.SaveAssets();
        }
    }
}
