using System.Collections.Generic;
using System.Linq;
using Tabletop.Domain;

namespace Tabletop.Application
{
    /// <summary>
    /// Unit selection model (MATCH_UX_SPEC 4.2). Slot 0 = A/left, slot 1 = B/right.
    /// The duplicate rule comes from configuration (<see cref="AllowDuplicates"/>), not from the view.
    /// </summary>
    public sealed class UnitSelection
    {
        private readonly ContentCatalog _catalog;
        private readonly string[] _slots = new string[2];

        public UnitSelection(ContentCatalog catalog)
        {
            _catalog = catalog;
        }

        public bool DeveloperMode { get; set; }
        /// <summary>Pieces the player owns (world journey, D-027). Null = the catalog's player-facing units.</summary>
        public IReadOnlyCollection<string> Unlocked { get; set; }

        private bool Selectable(UnitDefinition u, bool developer) =>
            developer || (Unlocked != null ? Unlocked.Contains(u.Id) : u.PlayerFacing);
        public bool AllowDuplicates { get; set; }
        public IReadOnlyList<string> Slots => _slots;
        public bool IsComplete => _slots[0] != null && _slots[1] != null;
        public string LastMessage { get; private set; } = "";

        public IReadOnlyList<UnitDefinition> Available(bool developer)
        {
            var list = new List<UnitDefinition>();
            foreach (var u in _catalog.Units)
                if (Selectable(u, developer)) list.Add(u);
            return list;
        }

        public int SlotOf(string unitId)
        {
            if (_slots[0] == unitId) return 0;
            if (_slots[1] == unitId) return 1;
            return -1;
        }

        /// <summary>Selecting a unit assigns it to the first empty slot; selecting an assigned unit removes it.</summary>
        public CommandRejection Toggle(string unitId)
        {
            if (!_catalog.TryGetUnit(unitId, out var def) || !Selectable(def, DeveloperMode))
                return Fail(RejectionCode.ConfigInvalid, "That figurine is not available.");
            int slot = SlotOf(unitId);
            if (slot >= 0 && !AllowDuplicates)
            {
                _slots[slot] = null;
                LastMessage = def.DisplayName + " removed from " + SlotName(slot) + ".";
                return null;
            }
            int empty = _slots[0] == null ? 0 : _slots[1] == null ? 1 : -1;
            if (empty < 0) return Fail(RejectionCode.UnitSelectionIncomplete, "Both slots are full. Remove a figurine or choose a slot to replace.");
            _slots[empty] = unitId;
            LastMessage = def.DisplayName + " assigned to " + SlotName(empty) + ".";
            return null;
        }

        /// <summary>Place a unit into a specific slot, replacing its occupant.</summary>
        public CommandRejection Assign(int slot, string unitId)
        {
            if (slot < 0 || slot > 1) return Fail(RejectionCode.InvalidReelIndex, "No such slot.");
            if (!_catalog.TryGetUnit(unitId, out var def) || !Selectable(def, DeveloperMode))
                return Fail(RejectionCode.ConfigInvalid, "That figurine is not available.");
            if (!AllowDuplicates && _slots[1 - slot] == unitId)
                return Fail(RejectionCode.DuplicateUnitNotAllowed, "Choose two different figurines.");
            _slots[slot] = unitId;
            LastMessage = def.DisplayName + " assigned to " + SlotName(slot) + ".";
            return null;
        }

        public void Remove(int slot)
        {
            if (slot < 0 || slot > 1) return;
            _slots[slot] = null;
            LastMessage = SlotName(slot) + " cleared.";
        }

        public void Swap()
        {
            var t = _slots[0];
            _slots[0] = _slots[1];
            _slots[1] = t;
            LastMessage = "Swapped A and B.";
        }

        public void Clear()
        {
            _slots[0] = null;
            _slots[1] = null;
        }

        public CommandRejection Validate()
        {
            if (!IsComplete) return new CommandRejection(-1, RejectionCode.UnitSelectionIncomplete, "Choose two figurines.", MatchPhase.Setup);
            if (!AllowDuplicates && _slots[0] == _slots[1])
                return new CommandRejection(-1, RejectionCode.DuplicateUnitNotAllowed, "Choose two different figurines.", MatchPhase.Setup);
            return null;
        }

        public static string SlotName(int slot) => slot == 0 ? "A / Left" : "B / Right";

        private CommandRejection Fail(RejectionCode code, string message)
        {
            LastMessage = message;
            return new CommandRejection(-1, code, message, MatchPhase.Setup);
        }
    }
}
