using System.Collections.Generic;

namespace Tabletop.Domain
{
    /// <summary>Printed-symbol totals for a set of faces (RULES_SPEC 5.2).</summary>
    public struct SymbolTotals
    {
        public int ChannelA;
        public int ChannelB;
        public int Hammer;
        public int XpA;
        public int XpB;

        public int Symbols(Channel c) => c == Channel.A ? ChannelA : ChannelB;
        public int Xp(Channel c) => c == Channel.A ? XpA : XpB;
        public int Energy(Channel c) => RulesConstants.ResourceFor(Symbols(c));
        public int BarrierGain => RulesConstants.ResourceFor(Hammer);
    }

    public static class SymbolEvaluator
    {
        public static SymbolTotals Evaluate(IEnumerable<ReelFace> faces)
        {
            var t = new SymbolTotals();
            foreach (var f in faces)
            {
                if (f == null) continue;
                t.ChannelA += f.ChannelA;
                t.ChannelB += f.ChannelB;
                t.Hammer += f.Hammer;
                if (f.XpChannel == Channel.A) t.XpA++;
                else if (f.XpChannel == Channel.B) t.XpB++;
            }
            return t;
        }
    }
}
