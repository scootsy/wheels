using System.Linq;
using NUnit.Framework;
using Tabletop.Application;
using Tabletop.Domain;
using static Tabletop.Domain.ReferenceContent;

namespace Tabletop.Tests.EditMode
{
    /// <summary>D-040: the developer god wheels and the dev-tool overrides on the journey.</summary>
    public class DevToolsTests
    {
        [SetUp]
        public void Clean() => GameFlow.Reset();

        [TearDown]
        public void After() => GameFlow.Reset();

        private static MatchConfig GodConfig(long seed, string opponentAi = ControllerIds.AiExpert, ReelTier opponentTier = ReelTier.Copper) =>
            new MatchConfig(RulesConstants.RulesVersion, seed,
                new SideConfig(ControllerIds.Human, ReelTier.God, Striker, Caster),
                new SideConfig(opponentAi, opponentTier, Striker, Caster));

        [Test]
        public void GodWheels_AreNotInTheStandardCatalog_SoNormalPlayAndReplaysAreUnchanged()
        {
            Assert.IsFalse(TestKit.Catalog.TryGetReelSet(ReelTier.God, out _));
            Assert.AreEqual(RejectionCode.ConfigInvalid, GodConfig(1).Validate(TestKit.Catalog).Code, "the standard catalog refuses them");
            var god = WithGodWheels(TestKit.Catalog);
            Assert.IsEmpty(god.Validate());
            Assert.AreNotEqual(TestKit.Catalog.ContentHash, god.ContentHash);
            Assert.AreEqual(TestKit.Catalog.Units.Count, god.Units.Count);
            Assert.AreSame(god, WithGodWheels(god));
            var set = god.ReelSet(ReelTier.God);
            Assert.AreEqual(5, set.Reels.Count);
            foreach (var reel in set.Reels) Assert.AreEqual(GodReelCodes, string.Join(",", reel.Faces.Select(f => f.Code)));
        }

        [Test]
        public void GodWheels_AreForThePlayerOnly()
        {
            var god = WithGodWheels(TestKit.Catalog);
            Assert.IsNull(GodConfig(1).Validate(god));
            var aiWithGod = new MatchConfig(RulesConstants.RulesVersion, 1,
                new SideConfig(ControllerIds.Human, ReelTier.Copper, Striker, Caster),
                new SideConfig(ControllerIds.AiExpert, ReelTier.God, Striker, Caster));
            Assert.AreEqual(RejectionCode.ConfigInvalid, aiWithGod.Validate(god).Code);
        }

        [Test]
        public void GodWheels_SpinOnlyGodFaces_AndAlmostAlwaysBeatTheExpert()
        {
            var god = WithGodWheels(TestKit.Catalog);
            int wins = 0;
            const int games = 30;
            for (int seed = 0; seed < games; seed++)
            {
                var m = Match.Start(GodConfig(seed), god);
                var p0 = AiProfiles.Create(ControllerIds.AiStandard);
                var p1 = AiProfiles.Create(ControllerIds.AiExpert);
                while (m.Phase != MatchPhase.Ended)
                {
                    var start = m.Snapshot();
                    AiTurnRunner.PlayTurn(p0, AiDecisionInput.Capture(start, SideId.Player, god, m.ReelDefinitions(SideId.Player)), m.Execute);
                    AiTurnRunner.PlayTurn(p1, AiDecisionInput.Capture(start, SideId.Opponent, god, m.ReelDefinitions(SideId.Opponent)), m.Execute);
                    for (int r = 0; r < 5; r++) Assert.AreEqual(GodReelCodes.Split(',')[m.Side(SideId.Player).Reels[r].FaceIndex], m.FaceOf(SideId.Player, r).Code);
                    m.Ok(MatchCommand.ResolveRound());
                }
                Assert.AreEqual(ReelTier.Copper, m.Side(SideId.Opponent).ReelTier, "the opponent keeps their own wheel");
                if (m.Winner == Winner.Player) wins++;
            }
            Assert.GreaterOrEqual(wins, games - 1, "god wheels win (" + wins + " of " + games + ")");
        }

        [Test]
        public void Session_GodWheelsOption_PlaysTheGodSet_AndItsReplayVerifies()
        {
            long next = 99;
            var s = new MatchSession(TestKit.Catalog, () => next++);
            s.Options.Encounter = EncounterCatalog.Get(EncounterCatalog.Champion);
            s.Options.GodWheels = true;
            s.ContinueFromSetup();
            s.Selection.Toggle(Striker);
            s.Selection.Toggle(Caster);
            Assert.IsTrue(s.ConfirmUnits(), s.LastStatus);
            Assert.AreEqual(ReelTier.God, s.Match.Config.Sides[0].ReelTier);
            Assert.AreEqual(s.Options.Encounter.Tier, s.Match.Config.Sides[1].ReelTier, "the champion keeps their wheel");
            Assert.AreSame(TestKit.Catalog, s.Catalog, "the session's own catalog is untouched");
            int guard = 0;
            while (s.State != UxState.MatchResult && guard++ < 2000)
            {
                switch (s.State)
                {
                    case UxState.RoundReady: case UxState.SpinDecision: Assert.IsTrue(s.RequestSpin()); break;
                    case UxState.Spinning: while (s.DequeueEvent() != null) { } s.NotifySpinPresented(); break;
                    case UxState.AiCommit: s.RunAiCommit(); break;
                    case UxState.Reveal: while (s.DequeueEvent() != null) { } s.NotifyRevealPresented(); break;
                    case UxState.Resolving: while (s.DequeueEvent() != null) { } s.NotifyResolutionPresented(); break;
                }
            }
            Assert.AreEqual(UxState.MatchResult, s.State);
            Assert.IsNull(ReplayRecord.Decode(s.ReplayText).Verify(TestKit.Catalog, out _), "god replays verify against the standard catalog");

            s.Options.GodWheels = false;
            Assert.IsTrue(s.Rematch());
            Assert.AreEqual(s.Options.PlayerTier, s.Match.Config.Sides[0].ReelTier, "switched off: back to the player's own wheel");
        }

        [Test]
        public void GodWheels_ResetWithANewJourney()
        {
            GameFlow.GodWheels = true;
            GameFlow.Reset();
            Assert.IsFalse(GameFlow.GodWheels);
        }

        [Test]
        public void DevTools_SetCoinsItemsAndWheels()
        {
            GameFlow.DevSetCoins(5000);
            Assert.AreEqual(5000, GameFlow.Coins);
            GameFlow.DevSetCoins(-3);
            Assert.AreEqual(0, GameFlow.Coins);

            GameFlow.DevSetItemCount(ItemCatalog.Tonic, 7);
            Assert.AreEqual(7, GameFlow.ItemCount(ItemCatalog.Tonic));
            GameFlow.DevSetItemCount(ItemCatalog.Tonic, 0);
            Assert.AreEqual(0, GameFlow.ItemCount(ItemCatalog.Tonic));
            Assert.IsFalse(GameFlow.Items.ContainsKey(ItemCatalog.Tonic));

            GameFlow.DevSetItemCount(ItemCatalog.DiamondWheel, 4);
            Assert.AreEqual(1, GameFlow.ItemCount(ItemCatalog.DiamondWheel), "a wheel is owned or not");
            Assert.AreEqual(ReelTier.Diamond, GameFlow.PlayerWheel);
            GameFlow.DevSetItemCount(ItemCatalog.DiamondWheel, 0);
            Assert.AreEqual(ReelTier.Copper, GameFlow.PlayerWheel);
            GameFlow.DevSetItemCount("no_such_item", 3);
            Assert.IsFalse(GameFlow.Items.ContainsKey("no_such_item"));
        }

        [Test]
        public void DevTools_FigurinesAndWins()
        {
            GameFlow.DevSetUnlocked(Hexer, true);
            Assert.IsTrue(GameFlow.IsUnlocked(Hexer));
            GameFlow.DevSetUnlocked(Hexer, false);
            Assert.IsFalse(GameFlow.IsUnlocked(Hexer));
            GameFlow.DevSetUnlocked(Striker, false);
            Assert.IsTrue(GameFlow.IsUnlocked(Striker), "the starting pair stays");

            foreach (var champ in EncounterCatalog.TownChampions) GameFlow.DevSetDefeated(champ.Id, true);
            Assert.IsTrue(GameFlow.TournamentQualified);
            foreach (var champ in EncounterCatalog.TownChampions) Assert.IsTrue(GameFlow.IsUnlocked(champ.PrizeUnit), champ.Name + "'s prize");
            GameFlow.DevSetDefeated(EncounterCatalog.Champion, false);
            Assert.IsFalse(GameFlow.HasDefeated(EncounterCatalog.Champion));
            Assert.IsFalse(GameFlow.TournamentQualified);
            GameFlow.DevSetDefeated("nobody", true);
            Assert.AreEqual(GameFlow.ChampionTitlesNeeded - 1, GameFlow.Wins);

            Assert.IsTrue(GameFlow.EnterTournament() == false);
            GameFlow.DevSetDefeated(EncounterCatalog.Champion, true);
            Assert.IsTrue(GameFlow.EnterTournament());
            GameFlow.DevLeaveTournament();
            Assert.AreEqual(0, GameFlow.TournamentRound);
        }
    }
}
