using System.Collections.Generic;
using Tabletop.Domain;

namespace Tabletop.Application
{
    /// <summary>
    /// Non-authoritative live preview for SPIN_DECISION (MATCH_UX_SPEC 4.5). Uses the exact
    /// RULES_SPEC formulas and caps, never mutates state, and never predicts attacks.
    /// </summary>
    public sealed class OutcomePreview
    {
        public sealed class UnitLine
        {
            public string Name;
            public Channel Channel;
            public int Symbols;
            public int EnergyGain;
            public int EnergyBefore;
            public int EnergyAfter;
            public int Cost;
            public int Wasted;
            public int XpFaces;
            public bool RankUpBefore;
            public bool ReadyBeforeResolution;
        }

        public UnitLine[] Units = new UnitLine[2];
        public int Hammers;
        public int BarrierGain;
        public int BarrierBefore;
        public int BarrierAfter;
        public int BarrierWasted;
        public bool HasFaces;

        public static OutcomePreview Compute(SideSnapshot side, IReadOnlyList<ReelDefinition> reels, ContentCatalog catalog)
        {
            var p = new OutcomePreview();
            var faces = new List<ReelFace>();
            for (int r = 0; r < side.Reels.Count; r++)
                if (side.Reels[r].FaceIndex >= 0) faces.Add(reels[r].Faces[side.Reels[r].FaceIndex]);
            p.HasFaces = faces.Count == side.Reels.Count;
            var t = SymbolEvaluator.Evaluate(faces);

            for (int u = 0; u < 2; u++)
            {
                var unit = side.Units[u];
                var def = catalog.Unit(unit.DefinitionId);
                var ch = (Channel)u;
                var rank = unit.Rank;
                int xp = t.Xp(ch);
                bool rankUp = xp > 0 && unit.Xp + xp >= RulesConstants.XpThreshold && rank != Rank.Gold;
                if (rankUp) rank = rank + 1;
                int cost = def.Stats(rank).EnergyCost;
                int before = System.Math.Min(unit.Energy, cost);
                int gain = t.Energy(ch);
                int after = System.Math.Min(cost, before + gain);
                p.Units[u] = new UnitLine
                {
                    Name = def.DisplayName,
                    Channel = ch,
                    Symbols = t.Symbols(ch),
                    EnergyGain = gain,
                    EnergyBefore = unit.Energy,
                    EnergyAfter = after,
                    Cost = cost,
                    Wasted = before + gain - after,
                    XpFaces = xp,
                    RankUpBefore = rankUp,
                    ReadyBeforeResolution = after >= cost,
                };
            }
            p.Hammers = t.Hammer;
            p.BarrierGain = t.BarrierGain;
            p.BarrierBefore = side.Barrier;
            p.BarrierAfter = System.Math.Min(RulesConstants.MaxBarrier, side.Barrier + t.BarrierGain);
            p.BarrierWasted = side.Barrier + t.BarrierGain - p.BarrierAfter;
            return p;
        }

        public string UnitText(int u)
        {
            var l = Units[u];
            string sym = l.Channel == Channel.A ? "A" : "B";
            string s = sym + " x" + l.Symbols + " -> +" + l.EnergyGain + " energy (" + l.EnergyAfter + "/" + l.Cost + ")";
            if (l.Wasted > 0) s += " [WASTED " + l.Wasted + "]";
            if (l.XpFaces > 0) s += "  +" + l.XpFaces + " XP" + (l.RankUpBefore ? " (RANK UP)" : "");
            if (l.ReadyBeforeResolution) s += "  READY BEFORE RESOLUTION";
            return l.Name + ": " + s;
        }

        public string BarrierText()
        {
            string s = "Hammers x" + Hammers + " -> +" + (BarrierAfter - BarrierBefore) + " Barrier (" + BarrierAfter + "/5)";
            if (BarrierWasted > 0) s += " [CAPPED " + BarrierWasted + "]";
            return s;
        }
    }
}
