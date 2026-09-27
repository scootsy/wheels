using System;
using System.Collections.Generic;
using System.IO;
using Tabletop.Application;
using Tabletop.Domain;
using UnityEngine;

namespace Tabletop.World
{
    /// <summary>What is written to disk for a journey (D-027). Plain fields so JsonUtility can read and write it.</summary>
    [Serializable]
    public sealed class SaveData
    {
        public int version = SaveGame.Version;
        public string savedAt;
        public List<string> defeated = new List<string>();
        public List<string> lossIds = new List<string>();
        public List<int> lossCounts = new List<int>();
        public List<string> unlocked = new List<string>();
        public float x;
        public float z;
        public float yaw;
        public bool firstPerson;
        // Version 2 (D-033): coins, the satchel and errands.
        public int coins;
        public List<string> itemIds = new List<string>();
        public List<int> itemCounts = new List<int>();
        public List<string> errandIds = new List<string>();
        public List<int> errandStages = new List<int>();
    }

    /// <summary>
    /// Saves and resumes the journey (D-027): who you have beaten, the pieces you own, where you stood, and your
    /// view preference. One file, written atomically; a missing or unreadable file simply means "no journey yet".
    /// A match in progress is not saved: resuming puts you back at the table's spot in the world.
    /// </summary>
    public static class SaveGame
    {
        public const int Version = 2;
        public const string FileName = "journey.json";

        /// <summary>Tests and the build self-check point this at a scratch folder so they never touch a real save.</summary>
        public static string DirectoryOverride { get; set; }

        public static string Folder => DirectoryOverride ?? UnityEngine.Application.persistentDataPath;
        public static string FilePath => Path.Combine(Folder, FileName);
        public static bool Exists => File.Exists(FilePath);

        public static SaveData Capture(Vector3 position, float yaw)
        {
            var d = new SaveData
            {
                savedAt = DateTime.Now.ToString("s"),
                x = position.x,
                z = position.z,
                yaw = yaw,
                firstPerson = GameFlow.FirstPersonView,
            };
            d.defeated.AddRange(GameFlow.Defeated);
            foreach (var l in GameFlow.Losses) { d.lossIds.Add(l.Key); d.lossCounts.Add(l.Value); }
            d.unlocked.AddRange(GameFlow.UnlockedUnits);
            d.coins = GameFlow.Coins;
            foreach (var i in GameFlow.Items) { d.itemIds.Add(i.Key); d.itemCounts.Add(i.Value); }
            foreach (var e in GameFlow.Errands) { d.errandIds.Add(e.Key); d.errandStages.Add((int)e.Value); }
            return d;
        }

        public static bool Save(Vector3 position, float yaw)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                var tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(Capture(position, yaw), true));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Tabletop] Could not save the journey: " + e.Message);
                return false;
            }
        }

        public static bool TryLoad(out SaveData data)
        {
            data = null;
            try
            {
                if (!Exists) return false;
                data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
                return data != null && data.version >= 1;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Tabletop] Could not read the saved journey: " + e.Message);
                data = null;
                return false;
            }
        }

        /// <summary>Puts a loaded journey into <see cref="GameFlow"/>.</summary>
        public static void Apply(SaveData data)
        {
            var losses = new List<KeyValuePair<string, int>>();
            for (int i = 0; i < data.lossIds.Count && i < data.lossCounts.Count; i++)
                losses.Add(new KeyValuePair<string, int>(data.lossIds[i], data.lossCounts[i]));
            var items = new List<KeyValuePair<string, int>>();
            for (int i = 0; i < data.itemIds.Count && i < data.itemCounts.Count; i++) items.Add(new KeyValuePair<string, int>(data.itemIds[i], data.itemCounts[i]));
            var errands = new List<KeyValuePair<string, int>>();
            for (int i = 0; i < data.errandIds.Count && i < data.errandStages.Count; i++) errands.Add(new KeyValuePair<string, int>(data.errandIds[i], data.errandStages[i]));
            // Journeys saved before coins existed (version 1) start with the usual purse.
            int coins = data.version >= 2 ? data.coins : GameFlow.StartingCoins;
            GameFlow.Restore(data.defeated, losses, data.unlocked, data.firstPerson,
                id => ReferenceContent.Catalog.TryGetUnit(id, out _), coins, items, errands);
        }

        public static void Delete()
        {
            try { if (Exists) File.Delete(FilePath); }
            catch (Exception e) { Debug.LogWarning("[Tabletop] Could not delete the saved journey: " + e.Message); }
        }
    }
}
