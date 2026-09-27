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

    /// <summary>
    /// One-match head starts bought with charms (D-033). All zero by default. They only change the starting state,
    /// through the simulation, and are part of the configuration (and so of every replay).
    /// </summary>
    public sealed class SideBoons
    {
        public static readonly SideBoons None = new SideBoons();

        public SideBoons(Rank rankA = Rank.Bronze, Rank rankB = Rank.Bronze, int barrier = 0, int crownBonus = 0, int energyA = 0, int energyB = 0)
        {
            RankA = rankA;
            RankB = rankB;
            Barrier = barrier;
            CrownBonus = crownBonus;
            EnergyA = energyA;
            EnergyB = energyB;
        }

        public Rank RankA { get; }
        public Rank RankB { get; }
        /// <summary>Starting Bulwark height.</summary>
        public int Barrier { get; }
        /// <summary>Added to the starting Crown (the hard cap still applies).</summary>
        public int CrownBonus { get; }
        public int EnergyA { get; }
        public int EnergyB { get; }

        public bool IsEmpty => RankA == Rank.Bronze && RankB == Rank.Bronze && Barrier == 0 && CrownBonus == 0 && EnergyA == 0 && EnergyB == 0;

        public Rank RankOf(int slot) => slot == 0 ? RankA : RankB;
        public int EnergyOf(int slot) => slot == 0 ? EnergyA : EnergyB;

        public string Validate()
        {
            if (Barrier < 0 || Barrier > RulesConstants.MaxBarrier) return "Boon barrier out of range";
            if (CrownBonus < 0 || RulesConstants.StartingCrown + CrownBonus > RulesConstants.HardCrownCap) return "Boon crown out of range";
            if (EnergyA < 0 || EnergyB < 0 || EnergyA > 9 || EnergyB > 9) return "Boon energy out of range";
            return null;
        }

        public string Encode() => (int)RankA + "." + (int)RankB + "." + Barrier + "." + CrownBonus + "." + EnergyA + "." + EnergyB;

        public static SideBoons Decode(string text)
        {
            var p = text.Split('.');
            if (p.Length != 6) throw new FormatException("Bad boons: " + text);
            int I(int i) => int.Parse(p[i], System.Globalization.CultureInfo.InvariantCulture);
            return new SideBoons((Rank)I(0), (Rank)I(1), I(2), I(3), I(4), I(5));
        }
    }

    public sealed class SideConfig
    {
        public SideConfig(string controllerId, ReelTier reelTier, string unitA, string unitB, SideBoons boons = null)
        {
            ControllerId = controllerId;
            ReelTier = reelTier;
            UnitIds = new ReadOnlyCollection<string>(new[] { unitA, unitB });
            Boons = boons ?? SideBoons.None;
        }

        public string ControllerId { get; }
        public ReelTier ReelTier { get; }
        /// <summary>[0] = Channel A / left, [1] = Channel B / right.</summary>
        public IReadOnlyList<string> UnitIds { get; }
        /// <summary>Charm head starts for this side (D-033); <see cref="SideBoons.None"/> in plain matches.</summary>
        public SideBoons Boons { get; }

        /// <summary>Boons are appended only when present, so older replays encode (and decode) unchanged.</summary>
        public string Encode() => ControllerId + ":" + ((int)ReelTier) + ":" + UnitIds[0] + "," + UnitIds[1] + (Boons.IsEmpty ? "" : ":" + Boons.Encode());

        public static SideConfig Decode(string text)
        {
            var parts = text.Split(':');
            if (parts.Length != 3 && parts.Length != 4) throw new FormatException("Bad side config: " + text);
            var units = parts[2].Split(',');
            if (units.Length != 2) throw new FormatException("Bad side units: " + text);
            return new SideConfig(parts[0], (ReelTier)int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture), units[0], units[1],
                parts.Length == 4 ? SideBoons.Decode(parts[3]) : null);
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
            // Each side brings its own fifth wheel (D-033): a bought wheel is the player's alone.
            foreach (var side in Sides)
            {
                var err = side.Boons.Validate();
                if (err != null) return Reject(RejectionCode.ConfigInvalid, err);
            }
            return null;
        }

        private static CommandRejection Reject(RejectionCode code, string message) =>
            new CommandRejection(-1, code, message, MatchPhase.Setup);
    }
}
