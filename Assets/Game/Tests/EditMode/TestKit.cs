using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Tabletop.Domain;

namespace Tabletop.Tests.EditMode
{
    /// <summary>Helpers for building forced, fully deterministic rule scenarios.</summary>
    public static class TestKit
    {
        public static ContentCatalog Catalog => ReferenceContent.Catalog;
        public static ReelSetDefinition Copper => Catalog.ReelSet(ReelTier.Copper);

        /// <summary>No energy, no hammers, no XP on the Copper set.</summary>
        public static readonly string[] Neutral = { "S", "D", "S", "D", "-" };

        public static ScenarioSetup Scenario() => new ScenarioSetup();

        /// <summary>Force both sides' first-spin faces for a round.</summary>
        public static ScenarioSetup Faces(this ScenarioSetup sc, int round, string[] player, string[] opponent)
        {
            sc.ForceCodes(SideId.Player, round, 1, Copper, player ?? Neutral);
            sc.ForceCodes(SideId.Opponent, round, 1, Copper, opponent ?? Neutral);
            return sc;
        }

        public static Match Start(ScenarioSetup sc, string pA = ReferenceContent.Striker, string pB = ReferenceContent.Caster,
            string oA = ReferenceContent.Striker, string oB = ReferenceContent.Caster, long seed = 7)
        {
            var cfg = MatchConfig.Standard(seed, pA, pB, oA, oB, scenario: sc, allowDuplicateUnits: true);
            return Match.Start(cfg, Catalog);
        }

        public static CommandResult Ok(this Match m, MatchCommand c)
        {
            var r = m.Execute(c);
            Assert.IsTrue(r.Accepted, "Expected " + c + " accepted but got " + r.Rejection);
            return r;
        }

        public static CommandRejection Rejected(this Match m, MatchCommand c)
        {
            var hash = m.StateHash();
            int events = m.EventLog.Count;
            int commands = m.AcceptedCommands.Count;
            var r = m.Execute(c);
            Assert.IsFalse(r.Accepted, "Expected " + c + " to be rejected");
            Assert.AreEqual(hash, m.StateHash(), "Rejected command mutated state");
            Assert.AreEqual(events, m.EventLog.Count, "Rejected command produced events");
            Assert.AreEqual(commands, m.AcceptedCommands.Count, "Rejected command entered the command log");
            return r.Rejection;
        }

        /// <summary>Each side spins once, locks all five, and finalizes.</summary>
        public static void CommitOneSpin(this Match m)
        {
            foreach (var side in new[] { SideId.Player, SideId.Opponent })
            {
                m.Ok(MatchCommand.Spin(side));
                for (int r = 0; r < 5; r++) m.Ok(MatchCommand.SetReelLock(side, r, true));
                m.Ok(MatchCommand.FinalizeSpin(side));
            }
        }

        /// <summary>Commit one spin for both sides and resolve the round. Returns resolution events.</summary>
        public static List<MatchEvent> PlayRound(this Match m)
        {
            m.CommitOneSpin();
            return m.Ok(MatchCommand.ResolveRound()).Events.ToList();
        }

        public static List<MatchEvent> OfType(this IEnumerable<MatchEvent> events, MatchEventType type) =>
            events.Where(e => e.Type == type).ToList();

        public static List<MatchEvent> For(this IEnumerable<MatchEvent> events, MatchEventType type, SideId side, int slot) =>
            events.Where(e => e.Type == type && e.Side == (int)side && e.Slot == slot).ToList();

        public static UnitSnapshot Unit(this Match m, SideId side, int slot) => m.Snapshot().Side(side).Units[slot];
        public static SideSnapshot Side(this Match m, SideId side) => m.Snapshot().Side(side);

        /// <summary>Plays a full match with an AI policy controlling each side.</summary>
        public static Match PlayFullMatch(long seed, string pA = ReferenceContent.Striker, string pB = ReferenceContent.Caster,
            string oA = ReferenceContent.Caster, string oB = ReferenceContent.Striker, ReelTier tier = ReelTier.Copper,
            string playerProfile = ControllerIds.AiStandard, string opponentProfile = ControllerIds.AiStandard)
        {
            var cfg = MatchConfig.Standard(seed, pA, pB, oA, oB, tier, opponentProfile, allowDuplicateUnits: true);
            var m = Match.Start(cfg, Catalog);
            var p0 = AiProfiles.Create(playerProfile);
            var p1 = AiProfiles.Create(opponentProfile);
            while (m.Phase != MatchPhase.Ended)
            {
                var start = m.Snapshot();
                var in0 = AiDecisionInput.Capture(start, SideId.Player, Catalog, m.ReelDefinitions(SideId.Player));
                var in1 = AiDecisionInput.Capture(start, SideId.Opponent, Catalog, m.ReelDefinitions(SideId.Opponent));
                AiTurnRunner.PlayTurn(p0, in0, m.Execute);
                AiTurnRunner.PlayTurn(p1, in1, m.Execute);
                m.Ok(MatchCommand.ResolveRound());
            }
            return m;
        }
    }
}
