using System.Collections.Generic;

namespace Tabletop.Domain
{
    /// <summary>
    /// Canonical rules content from RULES_SPEC Sections 5.3 and 9, with original project names.
    /// Unity authoring assets are generated from and validated against this data.
    /// Only Striker and Caster are player-facing before the human playtest gate (D-008).
    /// </summary>
    public static class ReferenceContent
    {
        public const string Striker = "striker";
        public const string Caster = "caster";
        public const string Ranger = "ranger";
        public const string Mason = "mason";
        public const string Shade = "shade";
        public const string Mender = "mender";
        public const string Hexer = "hexer";

        public static readonly string[] FixedReelCodes =
        {
            "S,D,S,S+,D,H,DD+,H",
            "S+,D,SS,D+,S,H,DD,HH",
            "S+,D,D+,S,D,HH,SS,HH",
            "S,D,S+,D,HH,S,D+,HH",
        };

        public static readonly Dictionary<ReelTier, string> FifthReelCodes = new Dictionary<ReelTier, string>
        {
            { ReelTier.Copper, "S,D,H,-,-,S,D,-" },
            { ReelTier.Bronze, "S,D,H,-,-,S,D,HH" },
            { ReelTier.Silver, "S,D,H,-,D,SS+,D,HH" },
            { ReelTier.Gold, "S,DD+,H,S,D,SS+,D,HH" },
            { ReelTier.Diamond, "S,DD+,HH,SS,DD,SS+,D,HH" },
            { ReelTier.Platinum, "S,DD+,HHH,SS+,DD+,SS+,D,HH" },
        };

        private static ContentCatalog _catalog;

        public static ContentCatalog Catalog => _catalog ?? (_catalog = Build());

        public static ReelDefinition ParseReel(string id, string codes)
        {
            var faces = new List<ReelFace>();
            foreach (var code in codes.Split(',')) faces.Add(ReelFace.Parse(code.Trim()));
            return new ReelDefinition(id, faces);
        }

        public static List<UnitDefinition> BuildUnits()
        {
            return new List<UnitDefinition>
            {
                new UnitDefinition(Striker, "Striker", "Ground attacker",
                    "Fires one low shot at height 1. Heavy damage, but any Barrier stops it.",
                    ActionKind.Projectiles, new[] { 1 },
                    new[] { new UnitRankStats(3, 3, 3), new UnitRankStats(3, 5, 5), new UnitRankStats(3, 7, 5) }, true),
                new UnitDefinition(Caster, "Caster", "Twin-shot caster",
                    "Fires two shots: one at height 1, then one at height 6 that always clears the Barrier.",
                    ActionKind.Projectiles, new[] { 1, 6 },
                    new[] { new UnitRankStats(5, 2, 2), new UnitRankStats(4, 3, 3), new UnitRankStats(4, 3, 5) }, true),
                // Developer-only definitions (generic engine capability; not exposed before the gate).
                new UnitDefinition(Ranger, "Ranger", "Arcing shooter",
                    "Fires one shot at height 3. Clears Barrier 0-2; stopped by 3 or more.",
                    ActionKind.Projectiles, new[] { 3 },
                    new[] { new UnitRankStats(4, 3, 1), new UnitRankStats(3, 4, 2), new UnitRankStats(3, 6, 3) }, false),
                new UnitDefinition(Mason, "Mason", "Siege builder",
                    "Fires one low shot, then adds 2 to your own Barrier (max 5).",
                    ActionKind.EngineerBuild, new[] { 1 },
                    new[] { new UnitRankStats(4, 1, 3, friendlyBarrier: 2), new UnitRankStats(4, 2, 5, friendlyBarrier: 2), new UnitRankStats(3, 4, 5, friendlyBarrier: 2) }, false),
                new UnitDefinition(Shade, "Shade", "Saboteur",
                    "Acts first. Drains energy from the enemy unit closest to acting, then hits the Crown directly.",
                    ActionKind.AssassinStrike, new int[0],
                    new[] { new UnitRankStats(3, crownDamage: 1, delay: 1), new UnitRankStats(3, crownDamage: 2, delay: 1), new UnitRankStats(3, crownDamage: 2, delay: 2) }, false),
                new UnitDefinition(Mender, "Mender", "Support",
                    "Heals your Crown (up to 12) and gives energy to the other unit.",
                    ActionKind.PriestBlessing, new int[0],
                    new[] { new UnitRankStats(4, heal: 1, energyGrant: 2), new UnitRankStats(3, heal: 2, energyGrant: 2), new UnitRankStats(3, heal: 2, energyGrant: 3) }, false),
                new UnitDefinition(Hexer, "Hexer", "Reckless volley",
                    "Takes 2 damage to your own Crown (never below 1), then fires at heights 5, 3, and 1.",
                    ActionKind.WarlockVolley, new[] { 5, 3, 1 },
                    new[] { new UnitRankStats(4, 1, 1, selfDamage: 2), new UnitRankStats(4, 2, 2, selfDamage: 2), new UnitRankStats(4, 3, 3, selfDamage: 2) }, false),
            };
        }

        public static List<ReelSetDefinition> BuildReelSets()
        {
            var sets = new List<ReelSetDefinition>();
            foreach (ReelTier tier in new[] { ReelTier.Copper, ReelTier.Bronze, ReelTier.Silver, ReelTier.Gold, ReelTier.Diamond, ReelTier.Platinum })
            {
                var reels = new List<ReelDefinition>();
                for (int i = 0; i < 4; i++) reels.Add(ParseReel("reel_" + (i + 1), FixedReelCodes[i]));
                reels.Add(ParseReel("reel_5_" + tier.ToString().ToLowerInvariant(), FifthReelCodes[tier]));
                sets.Add(new ReelSetDefinition(tier, reels));
            }
            return sets;
        }

        public static ContentCatalog Build() => new ContentCatalog(RulesConstants.RulesVersion, BuildUnits(), BuildReelSets());
    }
}
