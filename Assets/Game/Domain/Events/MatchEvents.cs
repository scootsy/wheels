using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace Tabletop.Domain
{
    public enum MatchEventType
    {
        MatchStarted,
        RoundStarted,
        ReelsSpun,
        ReelLockChanged,
        SpinFinalized,
        PanelXpGranted,
        UnitRankedUp,
        BarrierBuilt,
        EnergyGranted,
        EnergyDelayed,
        UnitActivated,
        ProjectileResolved,
        CrownDamaged,
        BarrierDamaged,
        CrownHealed,
        BombQueued,
        BombLaunched,
        ActionXpGranted,
        RoundEnded,
        MatchEnded,
    }

    public enum EnergySource { None = 0, Reels = 1, Priest = 2 }

    public enum Winner { None = 0, Player = 1, Opponent = 2, Tie = 3 }

    /// <summary>
    /// Immutable simulation event. Each carries the authoritative snapshot after it was applied,
    /// so presenters can reconcile at every event boundary.
    /// </summary>
    public sealed class MatchEvent
    {
        internal MatchEvent(int sequence, int round, MatchEventType type, int side, int slot, int targetSide, int targetSlot,
            int amount, int attempted, int before, int after, int height, int projectileIndex, bool targetIsCrown,
            EnergySource source, int stage, int spinNumber, int[] faces, bool[] changed, string note, MatchSnapshot stateAfter)
        {
            Sequence = sequence;
            Round = round;
            Type = type;
            Side = side;
            Slot = slot;
            TargetSide = targetSide;
            TargetSlot = targetSlot;
            Amount = amount;
            Attempted = attempted;
            Before = before;
            After = after;
            Height = height;
            ProjectileIndex = projectileIndex;
            TargetIsCrown = targetIsCrown;
            Source = source;
            Stage = stage;
            SpinNumber = spinNumber;
            Faces = faces == null ? null : new ReadOnlyCollection<int>((int[])faces.Clone());
            Changed = changed == null ? null : new ReadOnlyCollection<bool>((bool[])changed.Clone());
            Note = note ?? "";
            StateAfter = stateAfter;
        }

        public int Sequence { get; }
        public int Round { get; }
        public MatchEventType Type { get; }
        /// <summary>Acting/affected side index, or -1.</summary>
        public int Side { get; }
        /// <summary>Acting/affected unit slot (0 = A/left, 1 = B/right), reel index for lock events, or -1.</summary>
        public int Slot { get; }
        public int TargetSide { get; }
        public int TargetSlot { get; }
        /// <summary>Actual applied change (never the attempted amount).</summary>
        public int Amount { get; }
        /// <summary>Attempted change before caps; Attempted - Amount is wasted/capped.</summary>
        public int Attempted { get; }
        public int Before { get; }
        public int After { get; }
        public int Height { get; }
        public int ProjectileIndex { get; }
        public bool TargetIsCrown { get; }
        public EnergySource Source { get; }
        /// <summary>Resolution stage 1-12 from RULES_SPEC Section 10, or 0 outside resolution.</summary>
        public int Stage { get; }
        public int SpinNumber { get; }
        public IReadOnlyList<int> Faces { get; }
        public IReadOnlyList<bool> Changed { get; }
        public string Note { get; }
        public MatchSnapshot StateAfter { get; }

        public int Wasted => Attempted > Amount ? Attempted - Amount : 0;

        /// <summary>Deterministic one-line description used for event-log hashing and diagnostics.</summary>
        public string Describe()
        {
            var sb = new StringBuilder();
            sb.Append(Sequence).Append(' ').Append(Type).Append(" r").Append(Round)
              .Append(" s").Append(Side).Append(" u").Append(Slot)
              .Append(" t").Append(TargetSide).Append('.').Append(TargetSlot)
              .Append(" amt").Append(Amount).Append('/').Append(Attempted)
              .Append(' ').Append(Before).Append("->").Append(After)
              .Append(" h").Append(Height).Append(" p").Append(ProjectileIndex)
              .Append(TargetIsCrown ? " crown" : "").Append(" src").Append((int)Source)
              .Append(" st").Append(Stage).Append(" sp").Append(SpinNumber);
            if (Faces != null) { sb.Append(" f"); foreach (var f in Faces) sb.Append(f); }
            if (Changed != null) { sb.Append(" c"); foreach (var c in Changed) sb.Append(c ? '1' : '0'); }
            if (Note.Length > 0) sb.Append(" '").Append(Note).Append('\'');
            return sb.ToString();
        }

        public override string ToString() => Describe();
    }

    public sealed class UnitSnapshot
    {
        internal UnitSnapshot(string definitionId, Channel channel, Rank rank, int xp, int energy, int energyCost,
            EnergySource readySource, bool actedFromReels, bool actedFromPriest)
        {
            DefinitionId = definitionId;
            Channel = channel;
            Rank = rank;
            Xp = xp;
            Energy = energy;
            EnergyCost = energyCost;
            ReadySource = readySource;
            ActedFromReels = actedFromReels;
            ActedFromPriest = actedFromPriest;
        }

        public string DefinitionId { get; }
        public Channel Channel { get; }
        public Rank Rank { get; }
        public int Xp { get; }
        public int Energy { get; }
        public int EnergyCost { get; }
        public EnergySource ReadySource { get; }
        public bool ActedFromReels { get; }
        public bool ActedFromPriest { get; }
        public bool IsReady => Energy >= EnergyCost;
    }

    public sealed class ReelSnapshot
    {
        internal ReelSnapshot(int faceIndex, bool locked)
        {
            FaceIndex = faceIndex;
            Locked = locked;
        }

        /// <summary>-1 before the first spin of the match.</summary>
        public int FaceIndex { get; }
        public bool Locked { get; }
    }

    public sealed class SideSnapshot
    {
        internal SideSnapshot(int crownHp, int barrier, ReelTier reelTier, int spinsUsed, bool committed,
            ReelSnapshot[] reels, UnitSnapshot[] units)
        {
            CrownHp = crownHp;
            Barrier = barrier;
            ReelTier = reelTier;
            SpinsUsed = spinsUsed;
            Committed = committed;
            Reels = new ReadOnlyCollection<ReelSnapshot>(reels);
            Units = new ReadOnlyCollection<UnitSnapshot>(units);
        }

        public int CrownHp { get; }
        public int Barrier { get; }
        public ReelTier ReelTier { get; }
        public int SpinsUsed { get; }
        public bool Committed { get; }
        public IReadOnlyList<ReelSnapshot> Reels { get; }
        public IReadOnlyList<UnitSnapshot> Units { get; }
        public int SpinsRemaining => RulesConstants.SpinsPerRound - SpinsUsed;
        public int LockedCount
        {
            get { int n = 0; foreach (var r in Reels) if (r.Locked) n++; return n; }
        }
    }

    public sealed class MatchSnapshot
    {
        internal MatchSnapshot(string rulesVersion, long seed, int round, MatchPhase phase, Winner winner, SideSnapshot[] sides)
        {
            RulesVersion = rulesVersion;
            Seed = seed;
            Round = round;
            Phase = phase;
            Winner = winner;
            Sides = new ReadOnlyCollection<SideSnapshot>(sides);
        }

        public string RulesVersion { get; }
        public long Seed { get; }
        public int Round { get; }
        public MatchPhase Phase { get; }
        public Winner Winner { get; }
        public IReadOnlyList<SideSnapshot> Sides { get; }

        public SideSnapshot Side(SideId id) => Sides[(int)id];

        public string CanonicalText()
        {
            var sb = new StringBuilder();
            sb.Append(RulesVersion).Append('|').Append(Seed).Append('|').Append(Round).Append('|').Append((int)Phase).Append('|').Append((int)Winner);
            foreach (var s in Sides)
            {
                sb.Append("|side:").Append(s.CrownHp).Append(',').Append(s.Barrier).Append(',').Append((int)s.ReelTier)
                  .Append(',').Append(s.SpinsUsed).Append(',').Append(s.Committed ? 1 : 0);
                foreach (var r in s.Reels) sb.Append(",r").Append(r.FaceIndex).Append(r.Locked ? 'L' : 'U');
                foreach (var u in s.Units)
                    sb.Append(",u").Append(u.DefinitionId).Append(':').Append((int)u.Channel).Append(':').Append((int)u.Rank)
                      .Append(':').Append(u.Xp).Append(':').Append(u.Energy).Append(':').Append((int)u.ReadySource)
                      .Append(':').Append(u.ActedFromReels ? 1 : 0).Append(u.ActedFromPriest ? 1 : 0);
            }
            return sb.ToString();
        }

        public string Hash() => StableHash.Fnv1a64Hex(CanonicalText());
    }
}
