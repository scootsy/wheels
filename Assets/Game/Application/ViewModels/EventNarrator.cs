using Tabletop.Domain;

namespace Tabletop.Application
{
    /// <summary>Plain-language cause-and-delta text for every simulation event (MATCH_UX_SPEC 2, 7.1).</summary>
    public static class EventNarrator
    {
        public static string SideName(int side) => side == 0 ? "Your" : "Enemy";
        public static string SideNoun(int side) => side == 0 ? "your" : "the enemy";

        public static string UnitName(Match match, int side, int slot)
        {
            if (side < 0 || slot < 0 || slot > 1) return "";
            return SideName(side) + " " + match.UnitDefinition((SideId)side, slot).DisplayName + " (" + (slot == 0 ? "A" : "B") + ")";
        }

        public static string Describe(MatchEvent e, Match match)
        {
            string unit = UnitName(match, e.Side, e.Slot);
            string waste = e.Wasted > 0 ? " [" + e.Wasted + " WASTED]" : "";
            switch (e.Type)
            {
                case MatchEventType.MatchStarted: return "Match started. Seed " + match.Seed + ".";
                case MatchEventType.RoundStarted: return "Round " + e.Round + " begins.";
                case MatchEventType.ReelsSpun:
                    return (e.Side == 0 ? "You spin" : "Enemy spins") + " (spin " + e.SpinNumber + " of 3).";
                case MatchEventType.ReelLockChanged:
                    return (e.Side == 0 ? "You " : "Enemy ") + (e.Amount == 1 ? "lock" : "unlock") + " wheel " + (e.Slot + 1) + ".";
                case MatchEventType.SpinFinalized:
                    return (e.Side == 0 ? "Your" : "Enemy") + " result is final (" + e.Note + ").";
                case MatchEventType.PanelXpGranted:
                    return unit + " gains +" + e.Amount + " XP from XP faces (" + e.Before + " -> " + e.After + "/6)" + waste + ".";
                case MatchEventType.ActionXpGranted:
                    return unit + " gains +" + e.Amount + " XP for acting (" + e.Before + " -> " + e.After + "/6)" + waste + ".";
                case MatchEventType.UnitRankedUp:
                    return unit + " RANKS UP: " + (Rank)e.Before + " -> " + (Rank)e.After + ".";
                case MatchEventType.BombQueued:
                    return unit + " is Gold at 6 XP: a BOMB is armed (XP resets to 0).";
                case MatchEventType.BarrierBuilt:
                    return SideName(e.Side) + " Bulwark +" + e.Amount + " (" + e.Before + " -> " + e.After + ")"
                        + (e.Note == "unit build" ? " from " + unit : " from hammers") + (e.Wasted > 0 ? " [" + e.Wasted + " CAPPED]" : "") + ".";
                case MatchEventType.EnergyGranted:
                    return unit + " energy +" + e.Amount + " (" + e.Before + " -> " + e.After + ")"
                        + (e.Source == EnergySource.Priest ? " from blessing" : " from symbols") + waste + ".";
                case MatchEventType.EnergyDelayed:
                    return unit + " drains " + e.Amount + " energy from " + UnitName(match, e.TargetSide, e.TargetSlot)
                        + " (" + e.Before + " -> " + e.After + ")" + (e.Note.Length > 0 ? " — " + e.Note : "") + ".";
                case MatchEventType.UnitActivated:
                    return unit + " ACTS" + (e.Source == EnergySource.Priest ? " (blessing energy)" : "") + ".";
                case MatchEventType.ProjectileResolved:
                    return unit + " shot " + (e.ProjectileIndex + 1) + " at height " + e.Height
                        + (e.TargetIsCrown ? " clears Bulwark " + e.Before + " -> hits the Crown." : " is blocked by Bulwark " + e.Before + ".");
                case MatchEventType.CrownDamaged:
                    if (e.Note == "self") return unit + " takes " + e.Amount + " self-damage (" + e.Before + " -> " + e.After + ").";
                    return SideName(e.TargetSide) + " Crown -" + e.Amount + " (" + e.Before + " -> " + e.After + ")"
                        + (e.Note == "bomb" ? " from a BOMB (ignores the Bulwark)" : e.Note == "direct" ? " direct hit" : "") + ".";
                case MatchEventType.BarrierDamaged:
                    return SideName(e.TargetSide) + " Bulwark -" + e.Amount + " (" + e.Before + " -> " + e.After + ")"
                        + (e.Wasted > 0 ? " [" + e.Wasted + " absorbed, no spill]" : "") + ".";
                case MatchEventType.CrownHealed:
                    return SideName(e.Side) + " Crown +" + e.Amount + " (" + e.Before + " -> " + e.After + ")" + waste + ".";
                case MatchEventType.BombLaunched:
                    return unit + " BOMB flies over the Bulwark!";
                case MatchEventType.RoundEnded:
                    return "Round " + e.Round + " resolved.";
                case MatchEventType.MatchEnded:
                    switch (match.Winner)
                    {
                        case Winner.Player: return "Victory! The enemy Crown is broken.";
                        case Winner.Opponent: return "Defeat. Your Crown is broken.";
                        default: return "Tie. Both Crowns are broken.";
                    }
            }
            return e.Type.ToString();
        }
    }
}
