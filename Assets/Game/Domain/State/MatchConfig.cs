using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Tabletop.Domain
{
    public static class ControllerIds
    {
        public const string Human = "human";
        public const string AiLearner = "ai_learner";
        public const string AiStandard = "ai_standard";
        public const string AiExpert = "ai_expert";

        public static bool IsKnown(string id) => id == Human || id == AiLearner || id == AiStandard || id == AiExpert;
        public static bool IsAi(string id) => id == AiLearner || id == AiStandard || id == AiExpert;
    }

    public sealed class SideConfig
    {
        public SideConfig(string controllerId, ReelTier reelTier, string unitA, string unitB)
        {
            ControllerId = controllerId;
            ReelTier = reelTier;
            UnitIds = new ReadOnlyCollection<string>(new[] { unitA, unitB });
        }

        public string ControllerId { get; }
        public ReelTier ReelTier { get; }
        /// <summary>[0] = Channel A / left, [1] = Channel B / right.</summary>
        public IReadOnlyList<string> UnitIds { get; }

        public string Encode() => ControllerId + ":" + ((int)ReelTier) + ":" + UnitIds[0] + "," + UnitIds[1];

        public static SideConfig Decode(string text)
        {
            var parts = text.Split(':');
            if (parts.Length != 3) throw new FormatException("Bad side config: " + text);
            var units = parts[2].Split(',');
            if (units.Length != 2) throw new FormatException("Bad side units: " + text);
            return new SideConfig(parts[0], (ReelTier)int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture), units[0], units[1]);
        }
    }

    public sealed class MatchConfig
    {
        public const long MaxSeed = int.MaxValue;

        public MatchConfig(string rulesVersion, long seed, SideConfig player, SideConfig opponent, bool allowDuplicateUnits = false,
            ScenarioSetup scenario = null)
        {
            Scenario = scenario;
            RulesVersion = rulesVersion;
            Seed = seed;
            Sides = new ReadOnlyCollection<SideConfig>(new[] { player, opponent });
            AllowDuplicateUnits = allowDuplicateUnits;
        }

        public string RulesVersion { get; }
        public long Seed { get; }
        public IReadOnlyList<SideConfig> Sides { get; }
        public bool AllowDuplicateUnits { get; }
        /// <summary>Developer/test-only forced scenario; null in normal play.</summary>
        public ScenarioSetup Scenario { get; }

        public static MatchConfig Standard(long seed, string playerA, string playerB, string opponentA = ReferenceContent.Striker,
            string opponentB = ReferenceContent.Caster, ReelTier tier = ReelTier.Copper, string aiProfile = ControllerIds.AiStandard,
            ScenarioSetup scenario = null, bool allowDuplicateUnits = false)
        {
            return new MatchConfig(RulesConstants.RulesVersion, seed,
                new SideConfig(ControllerIds.Human, tier, playerA, playerB),
                new SideConfig(aiProfile, tier, opponentA, opponentB), allowDuplicateUnits, scenario);
        }

        /// <summary>Validation per MATCH_UX_SPEC 12.4. Returns null when valid.</summary>
        public CommandRejection Validate(ContentCatalog catalog)
        {
            if (catalog == null) return Reject(RejectionCode.ConfigInvalid, "No content catalog.");
            var contentErrors = catalog.Validate();
            if (contentErrors.Count > 0) return Reject(RejectionCode.ConfigInvalid, "Invalid content: " + contentErrors[0]);
            if (RulesVersion != RulesConstants.RulesVersion || catalog.RulesVersion != RulesVersion)
                return Reject(RejectionCode.ConfigInvalid, "Unknown rules version " + RulesVersion);
            if (Seed < 0 || Seed > MaxSeed) return Reject(RejectionCode.ConfigInvalid, "Seed outside supported range.");
            if (Sides.Count != 2 || Sides[0] == null || Sides[1] == null) return Reject(RejectionCode.ConfigInvalid, "Two sides required.");
            foreach (var side in Sides)
            {
                if (!ControllerIds.IsKnown(side.ControllerId)) return Reject(RejectionCode.ConfigInvalid, "Unknown controller " + side.ControllerId);
                if (!catalog.TryGetReelSet(side.ReelTier, out _)) return Reject(RejectionCode.ConfigInvalid, "Unknown reel set " + side.ReelTier);
                if (side.UnitIds.Count != RulesConstants.UnitsPerSide) return Reject(RejectionCode.ConfigInvalid, "Two units per side required.");
                foreach (var id in side.UnitIds)
                {
                    if (string.IsNullOrEmpty(id)) return Reject(RejectionCode.UnitSelectionIncomplete, "Choose two units.");
                    if (!catalog.TryGetUnit(id, out _)) return Reject(RejectionCode.ConfigInvalid, "Unknown unit " + id);
                }
                if (!AllowDuplicateUnits && side.UnitIds[0] == side.UnitIds[1])
                    return Reject(RejectionCode.DuplicateUnitNotAllowed, "Choose two different units.");
            }
            if (Scenario != null)
            {
                var err = Scenario.Validate();
                if (err != null) return Reject(RejectionCode.ConfigInvalid, err);
            }
            if (Sides[0].ReelTier != Sides[1].ReelTier)
                return Reject(RejectionCode.ConfigInvalid, "Both sides must use the same reel tier.");
            return null;
        }

        private static CommandRejection Reject(RejectionCode code, string message) =>
            new CommandRejection(-1, code, message, MatchPhase.Setup);
    }
}
