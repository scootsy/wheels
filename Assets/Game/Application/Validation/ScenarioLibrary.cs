using System.Collections.Generic;
using Tabletop.Domain;

namespace Tabletop.Application
{
    /// <summary>
    /// Developer-only forced acceptance scenarios (MATCH_UX_SPEC 4.1). Each forces round-1 faces
    /// for every spin so the outcome does not depend on luck. Normal play never uses these.
    /// </summary>
    public static class ScenarioLibrary
    {
        public const string Victory = "Victory next round";
        public const string Defeat = "Defeat next round";
        public const string Tie = "Tie next round";
        public const string RankAndBomb = "Rank-up and bomb";
        public const string BarrierWall = "Barrier wall";

        public static readonly string[] Names = { Victory, Defeat, Tie, RankAndBomb, BarrierWall };

        public static ScenarioSetup Build(string name, ReelSetDefinition reels)
        {
            var sc = new ScenarioSetup { Name = name };
            const int Full = 99; // clamped to each unit's cost
            string[] neutral = NeutralFaces(reels);
            string[] faces = neutral;
            switch (name)
            {
                case Victory:
                    sc.SetCrown(SideId.Opponent, 2).SetUnit(SideId.Player, 0, Rank.Bronze, 0, Full).SetUnit(SideId.Player, 1, Rank.Bronze, 0, Full);
                    break;
                case Defeat:
                    sc.SetCrown(SideId.Player, 2).SetUnit(SideId.Opponent, 0, Rank.Bronze, 0, Full).SetUnit(SideId.Opponent, 1, Rank.Bronze, 0, Full);
                    break;
                case Tie:
                    sc.SetCrown(SideId.Player, 2).SetCrown(SideId.Opponent, 2);
                    for (int s = 0; s < 2; s++) { sc.SetUnit((SideId)s, 0, Rank.Bronze, 0, Full); sc.SetUnit((SideId)s, 1, Rank.Bronze, 0, Full); }
                    break;
                case RankAndBomb:
                    sc.SetUnit(SideId.Player, 0, Rank.Gold, 5, 0).SetUnit(SideId.Player, 1, Rank.Silver, 5, 0);
                    faces = XpFaces(reels);
                    break;
                case BarrierWall:
                    sc.SetBarrier(SideId.Opponent, 5).SetBarrier(SideId.Player, 5)
                      .SetUnit(SideId.Player, 0, Rank.Bronze, 0, Full).SetUnit(SideId.Player, 1, Rank.Bronze, 0, Full);
                    break;
                default:
                    return null;
            }
            for (int spin = 1; spin <= RulesConstants.SpinsPerRound; spin++)
            {
                sc.ForceCodes(SideId.Player, 1, spin, reels, faces);
                sc.ForceCodes(SideId.Opponent, 1, spin, reels, neutral);
            }
            return sc;
        }

        /// <summary>Faces yielding no energy, Barrier, or XP (fifth reel uses a blank when the tier has one).</summary>
        public static string[] NeutralFaces(ReelSetDefinition reels)
        {
            string fifth = Has(reels.Reels[4], "-") ? "-" : "S";
            return new[] { "D", "S", "D", "S", fifth };
        }

        /// <summary>One XP face for each channel.</summary>
        public static string[] XpFaces(ReelSetDefinition reels)
        {
            string fifth = Has(reels.Reels[4], "-") ? "-" : "S";
            return new[] { "S+", "D+", "D", "S", fifth };
        }

        private static bool Has(ReelDefinition reel, string code)
        {
            foreach (var f in reel.Faces) if (f.Code == code) return true;
            return false;
        }
    }
}
