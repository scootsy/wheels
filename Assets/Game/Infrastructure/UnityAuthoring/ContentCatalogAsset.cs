using System;
using System.Collections.Generic;
using Tabletop.Domain;
using UnityEngine;

namespace Tabletop.Infrastructure
{
    /// <summary>
    /// Designer-facing authoring data for units and reels (D-004). Never read by the simulation
    /// directly: <see cref="ToCatalog"/> maps it into validated, immutable domain definitions.
    /// </summary>
    [CreateAssetMenu(menuName = "Tabletop/Content Catalog", fileName = "ContentCatalog")]
    public sealed class ContentCatalogAsset : ScriptableObject
    {
        [Serializable]
        public sealed class RankRow
        {
            public int energyCost;
            public int crownDamage;
            public int barrierDamage;
            public int delay;
            public int heal;
            public int energyGrant;
            public int selfDamage;
            public int friendlyBarrier;
        }

        [Serializable]
        public sealed class UnitEntry
        {
            public string id;
            public string displayName;
            public string role;
            [TextArea] public string description;
            public ActionKind action;
            public int[] heights = new int[0];
            public RankRow bronze = new RankRow();
            public RankRow silver = new RankRow();
            public RankRow gold = new RankRow();
            [Tooltip("Visible in normal unit selection. Only Striker and Caster before the playtest gate.")]
            public bool playerFacing;
        }

        [Serializable]
        public sealed class ReelSetEntry
        {
            public ReelTier tier;
            [Tooltip("Five reels, each eight comma-separated faces in spec notation (S, DD+, HH, -).")]
            public string[] reels = new string[5];
        }

        public string rulesVersion = RulesConstants.RulesVersion;
        public List<UnitEntry> units = new List<UnitEntry>();
        public List<ReelSetEntry> reelSets = new List<ReelSetEntry>();

        /// <summary>Maps authoring data to an immutable catalog. Returns null and errors when invalid.</summary>
        public ContentCatalog ToCatalog(out IReadOnlyList<string> errors)
        {
            var list = new List<string>();
            errors = list;
            try
            {
                var defs = new List<UnitDefinition>();
                foreach (var u in units)
                {
                    defs.Add(new UnitDefinition(u.id, u.displayName, u.role, u.description, u.action, u.heights ?? new int[0],
                        new[] { Row(u.bronze), Row(u.silver), Row(u.gold) }, u.playerFacing));
                }
                var sets = new List<ReelSetDefinition>();
                foreach (var s in reelSets)
                {
                    var reels = new List<ReelDefinition>();
                    for (int i = 0; i < s.reels.Length; i++)
                    {
                        string id = i < 4 ? "reel_" + (i + 1) : "reel_5_" + s.tier.ToString().ToLowerInvariant();
                        reels.Add(ReferenceContent.ParseReel(id, s.reels[i]));
                    }
                    sets.Add(new ReelSetDefinition(s.tier, reels));
                }
                var catalog = new ContentCatalog(rulesVersion, defs, sets);
                list.AddRange(catalog.Validate());
                return list.Count == 0 ? catalog : null;
            }
            catch (Exception ex)
            {
                list.Add(ex.Message);
                return null;
            }
        }

        private static UnitRankStats Row(RankRow r) =>
            new UnitRankStats(r.energyCost, r.crownDamage, r.barrierDamage, r.delay, r.heal, r.energyGrant, r.selfDamage, r.friendlyBarrier);

        /// <summary>Overwrites this asset with the canonical spec content (used by the editor menu and tests).</summary>
        public void PopulateFromReference()
        {
            rulesVersion = RulesConstants.RulesVersion;
            units.Clear();
            foreach (var d in ReferenceContent.BuildUnits())
            {
                RankRow R(int i)
                {
                    var s = d.Ranks[i];
                    return new RankRow
                    {
                        energyCost = s.EnergyCost, crownDamage = s.CrownDamage, barrierDamage = s.BarrierDamage, delay = s.Delay,
                        heal = s.Heal, energyGrant = s.EnergyGrant, selfDamage = s.SelfDamage, friendlyBarrier = s.FriendlyBarrier,
                    };
                }
                var heights = new int[d.Heights.Count];
                for (int i = 0; i < heights.Length; i++) heights[i] = d.Heights[i];
                units.Add(new UnitEntry
                {
                    id = d.Id, displayName = d.DisplayName, role = d.Role, description = d.Description, action = d.Action,
                    heights = heights, bronze = R(0), silver = R(1), gold = R(2), playerFacing = d.PlayerFacing,
                });
            }
            reelSets.Clear();
            foreach (var set in ReferenceContent.BuildReelSets())
            {
                var entry = new ReelSetEntry { tier = set.Tier, reels = new string[set.Reels.Count] };
                for (int r = 0; r < set.Reels.Count; r++)
                {
                    var codes = new string[set.Reels[r].Faces.Count];
                    for (int f = 0; f < codes.Length; f++) codes[f] = set.Reels[r].Faces[f].Code;
                    entry.reels[r] = string.Join(",", codes);
                }
                reelSets.Add(entry);
            }
        }
    }
}
