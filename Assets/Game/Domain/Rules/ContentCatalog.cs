using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace Tabletop.Domain
{
    /// <summary>Immutable, validated rules content for one rules version.</summary>
    public sealed class ContentCatalog
    {
        private readonly Dictionary<string, UnitDefinition> _units;
        private readonly Dictionary<ReelTier, ReelSetDefinition> _reelSets;

        public ContentCatalog(string rulesVersion, IList<UnitDefinition> units, IList<ReelSetDefinition> reelSets)
        {
            RulesVersion = rulesVersion;
            Units = new ReadOnlyCollection<UnitDefinition>(new List<UnitDefinition>(units));
            ReelSets = new ReadOnlyCollection<ReelSetDefinition>(new List<ReelSetDefinition>(reelSets));
            _units = new Dictionary<string, UnitDefinition>(StringComparer.Ordinal);
            foreach (var u in units) _units[u.Id] = u;
            _reelSets = new Dictionary<ReelTier, ReelSetDefinition>();
            foreach (var r in reelSets) _reelSets[r.Tier] = r;
            ContentHash = ComputeHash();
        }

        public string RulesVersion { get; }
        public IReadOnlyList<UnitDefinition> Units { get; }
        public IReadOnlyList<ReelSetDefinition> ReelSets { get; }
        /// <summary>Stable 64-bit FNV-1a hash of the canonical content text, as 16 hex digits.</summary>
        public string ContentHash { get; }

        public bool TryGetUnit(string id, out UnitDefinition unit) => _units.TryGetValue(id ?? "", out unit);
        public UnitDefinition Unit(string id) => _units[id];
        public bool TryGetReelSet(ReelTier tier, out ReelSetDefinition set) => _reelSets.TryGetValue(tier, out set);
        public ReelSetDefinition ReelSet(ReelTier tier) => _reelSets[tier];

        /// <summary>Canonical text used for hashing; independent of platform, locale, and dictionary order.</summary>
        public string CanonicalText()
        {
            var sb = new StringBuilder();
            sb.Append("rules=").Append(RulesVersion).Append('\n');
            foreach (var u in Units)
            {
                sb.Append("unit=").Append(u.Id).Append(';').Append((int)u.Action).Append(';');
                foreach (var h in u.Heights) sb.Append(h).Append(',');
                sb.Append(';').Append(u.PlayerFacing ? 1 : 0);
                foreach (var r in u.Ranks)
                {
                    sb.Append('|').Append(r.EnergyCost).Append(',').Append(r.CrownDamage).Append(',').Append(r.BarrierDamage)
                      .Append(',').Append(r.Delay).Append(',').Append(r.Heal).Append(',').Append(r.EnergyGrant)
                      .Append(',').Append(r.SelfDamage).Append(',').Append(r.FriendlyBarrier);
                }
                sb.Append('\n');
            }
            foreach (var set in ReelSets)
            {
                sb.Append("reels=").Append((int)set.Tier);
                foreach (var reel in set.Reels)
                {
                    sb.Append('|').Append(reel.Id).Append(':');
                    foreach (var f in reel.Faces) sb.Append(f.Code).Append(',');
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }

        private string ComputeHash() => StableHash.Fnv1a64Hex(CanonicalText());

        /// <summary>Validates content per MATCH_UX_SPEC 12.4. Returns an empty list when valid.</summary>
        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            if (RulesVersion != RulesConstants.RulesVersion) errors.Add("Unknown rules version " + RulesVersion);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var u in Units)
            {
                if (string.IsNullOrEmpty(u.Id)) { errors.Add("Unit with empty id"); continue; }
                if (!seen.Add(u.Id)) errors.Add("Duplicate unit id " + u.Id);
                if (u.Ranks.Count != 3) errors.Add(u.Id + ": requires Bronze/Silver/Gold rows");
                foreach (var r in u.Ranks)
                {
                    if (r.EnergyCost <= 0) errors.Add(u.Id + ": nonpositive energy cost");
                    if (r.CrownDamage < 0 || r.BarrierDamage < 0 || r.Delay < 0 || r.Heal < 0 || r.EnergyGrant < 0
                        || r.SelfDamage < 0 || r.FriendlyBarrier < 0) errors.Add(u.Id + ": negative value");
                }
                bool needsHeights = u.Action == ActionKind.Projectiles || u.Action == ActionKind.EngineerBuild || u.Action == ActionKind.WarlockVolley;
                if (needsHeights && u.Heights.Count == 0) errors.Add(u.Id + ": attack action without heights");
                foreach (var h in u.Heights) if (h <= 0) errors.Add(u.Id + ": nonpositive height");
                if (!Enum.IsDefined(typeof(ActionKind), u.Action)) errors.Add(u.Id + ": unknown action");
            }
            foreach (var set in ReelSets)
            {
                if (set.Reels.Count != ReelSetDefinition.ReelCount) errors.Add("Reel set " + set.Tier + ": requires five reels");
                foreach (var reel in set.Reels)
                {
                    if (reel.Faces.Count != ReelDefinition.FaceCount) errors.Add("Reel " + reel.Id + ": requires eight faces");
                    foreach (var f in reel.Faces)
                    {
                        if (f == null) { errors.Add("Reel " + reel.Id + ": null face"); continue; }
                        if (f.ChannelA > 3 || f.ChannelB > 3 || f.Hammer > 3) errors.Add("Reel " + reel.Id + ": face quantity above 3 cannot be rendered");
                        if (f.XpChannel.HasValue && f.Count(f.XpChannel.Value) == 0) errors.Add("Reel " + reel.Id + ": XP face without its channel symbol");
                    }
                }
            }
            if (!_reelSets.ContainsKey(ReelTier.Copper)) errors.Add("Copper reel set missing");
            return errors;
        }
    }

    public static class RulesConstants
    {
        public const string RulesVersion = "0.1.0";
        public const int StartingCrown = 10;
        public const int NormalCrownCap = 10;
        public const int HardCrownCap = 12;
        public const int MaxBarrier = 5;
        public const int XpThreshold = 6;
        public const int ActionXp = 2;
        public const int BombDamage = 2;
        public const int SpinsPerRound = 3;
        public const int UnitsPerSide = 2;
        /// <summary>Hard cap on rounds so a pathological configuration cannot loop forever. Ends in a tie.</summary>
        public const int MaxRounds = 200;

        /// <summary>RULES_SPEC 5.2: max(0, symbolCount - 2).</summary>
        public static int ResourceFor(int symbolCount) => symbolCount > 2 ? symbolCount - 2 : 0;
    }

    public static class StableHash
    {
        public static ulong Fnv1a64(string text)
        {
            ulong hash = 14695981039346656037UL;
            var bytes = Encoding.UTF8.GetBytes(text);
            foreach (var b in bytes)
            {
                hash ^= b;
                hash *= 1099511628211UL;
            }
            return hash;
        }

        public static string Fnv1a64Hex(string text) => Fnv1a64(text).ToString("x16");
    }
}
