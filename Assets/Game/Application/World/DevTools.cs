using System;
using Tabletop.Domain;

namespace Tabletop.Application
{
    /// <summary>
    /// Developer overrides for testing the journey (D-040): set coins, items and wheels, own or drop figurines, mark
    /// opponents beaten, and switch on the god wheels. Reached only through the hidden DEV TOOLS menu; nothing in normal
    /// play calls these.
    /// </summary>
    public static partial class GameFlow
    {
        /// <summary>The hidden DEV TOOLS menu has been unlocked this session. Not saved, and kept by a new journey.</summary>
        public static bool DevToolsUnlocked { get; set; }

        /// <summary>
        /// The player brings the god wheels (all five wheels overpowered) to every table (D-040). Saved with the journey;
        /// cleared by <see cref="Reset"/>. Opponents never get them.
        /// </summary>
        public static bool GodWheels { get; set; }

        public static void DevSetCoins(int coins) => Coins = Math.Max(0, coins);

        /// <summary>Sets how many of an item the player has (wheels: owned or not). Unknown ids are ignored.</summary>
        public static void DevSetItemCount(string itemId, int count)
        {
            var item = ItemCatalog.Find(itemId);
            if (item == null) return;
            if (item.Kind == ItemKind.Wheel) count = Math.Min(count, 1);
            if (count <= 0) _items.Remove(itemId);
            else _items[itemId] = count;
        }

        /// <summary>Gives or takes a figurine. The starting pair can't be taken away.</summary>
        public static void DevSetUnlocked(string unitId, bool owned)
        {
            if (owned) { if (!_unlocked.Contains(unitId)) _unlocked.Add(unitId); }
            else if (!System.Linq.Enumerable.Contains(EncounterCatalog.StartingUnits, unitId)) _unlocked.Remove(unitId);
        }

        /// <summary>
        /// Marks an opponent beaten or not. Beating a champion this way also gives their figurine, as a real win would.
        /// </summary>
        public static void DevSetDefeated(string encounterId, bool beaten)
        {
            EncounterDefinition e = null;
            foreach (var x in EncounterCatalog.All) if (x.Id == encounterId) e = x;
            if (e == null) return;
            if (!beaten) { _defeated.Remove(encounterId); return; }
            _defeated.Add(encounterId);
            if (e.PrizeUnit != null) DevSetUnlocked(e.PrizeUnit, true);
        }

        /// <summary>Leaves the Grand Tournament run (round 0), as a loss would.</summary>
        public static void DevLeaveTournament() => TournamentRound = 0;
    }
}
