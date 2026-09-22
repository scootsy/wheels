using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace Tabletop.Domain
{
    /// <summary>Public, round-start information about one unit.</summary>
    public sealed class AiUnitView
    {
        public AiUnitView(UnitDefinition definition, Rank rank, int xp, int energy)
        {
            Definition = definition;
            Rank = rank;
            Xp = xp;
            Energy = energy;
        }

        public UnitDefinition Definition { get; }
        public Rank Rank { get; }
        public int Xp { get; }
        public int Energy { get; }
        public int Cost => Definition.Stats(Rank).EnergyCost;
        public int EnergyNeeded => Cost > Energy ? Cost - Energy : 0;
    }

    /// <summary>Public, round-start information about one side. Deliberately contains no reel faces or locks.</summary>
    public sealed class AiSideView
    {
        public AiSideView(int crown, int barrier, AiUnitView unitA, AiUnitView unitB)
        {
            Crown = crown;
            Barrier = barrier;
            Units = new ReadOnlyCollection<AiUnitView>(new[] { unitA, unitB });
        }

        public int Crown { get; }
        public int Barrier { get; }
        public IReadOnlyList<AiUnitView> Units { get; }
    }

    /// <summary>
    /// Everything an AI may know when choosing its commands (MATCH_UX_SPEC 4.6):
    /// the public round-start state plus its own reel definitions. Its own faces arrive
    /// separately, from its own <see cref="MatchEventType.ReelsSpun"/> events.
    /// The opposing side's current-round faces, locks, and results are structurally absent.
    /// </summary>
    public sealed class AiDecisionInput
    {
        private AiDecisionInput(SideId self, int round, long seed, AiSideView own, AiSideView opponent, IReadOnlyList<ReelDefinition> ownReels)
        {
            Self = self;
            Round = round;
            Seed = seed;
            Own = own;
            Opponent = opponent;
            OwnReels = ownReels;
        }

        public SideId Self { get; }
        public int Round { get; }
        /// <summary>Match seed, used only to derive deterministic evaluation streams.</summary>
        public long Seed { get; }
        public AiSideView Own { get; }
        public AiSideView Opponent { get; }
        public IReadOnlyList<ReelDefinition> OwnReels { get; }

        /// <summary>Capture from the authoritative snapshot at ROUND_READY (before any spin this round).</summary>
        public static AiDecisionInput Capture(MatchSnapshot roundStart, SideId self, ContentCatalog catalog, IReadOnlyList<ReelDefinition> ownReels)
        {
            AiSideView View(SideSnapshot s) => new AiSideView(s.CrownHp, s.Barrier,
                new AiUnitView(catalog.Unit(s.Units[0].DefinitionId), s.Units[0].Rank, s.Units[0].Xp, s.Units[0].Energy),
                new AiUnitView(catalog.Unit(s.Units[1].DefinitionId), s.Units[1].Rank, s.Units[1].Xp, s.Units[1].Energy));
            return new AiDecisionInput(self, roundStart.Round, roundStart.Seed, View(roundStart.Side(self)),
                View(roundStart.Side(self == SideId.Player ? SideId.Opponent : SideId.Player)), ownReels);
        }

        /// <summary>Canonical description of all information in this input (used by isolation tests).</summary>
        public string CanonicalText()
        {
            var sb = new StringBuilder();
            sb.Append((int)Self).Append('|').Append(Round).Append('|').Append(Seed);
            foreach (var side in new[] { Own, Opponent })
            {
                sb.Append("|").Append(side.Crown).Append(',').Append(side.Barrier);
                foreach (var u in side.Units) sb.Append(',').Append(u.Definition.Id).Append(':').Append((int)u.Rank).Append(':').Append(u.Xp).Append(':').Append(u.Energy);
            }
            foreach (var r in OwnReels) sb.Append('|').Append(r.Id);
            return sb.ToString();
        }
    }
}
