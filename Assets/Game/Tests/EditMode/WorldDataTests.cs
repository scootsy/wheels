using System.Linq;
using NUnit.Framework;
using Tabletop.Application;
using Tabletop.Domain;
using Tabletop.World;
using UnityEngine;

namespace Tabletop.Tests.EditMode
{
    /// <summary>World slice data: encounters, the world/match hand-off, and walkable areas (D-025).</summary>
    public class WorldDataTests
    {
        [SetUp]
        public void Reset() => GameFlow.Reset();

        [Test]
        public void Encounters_HaveUniqueIds_AndBuildValidMatches()
        {
            var ids = EncounterCatalog.All.Select(e => e.Id).ToList();
            CollectionAssert.AllItemsAreUnique(ids);
            Assert.AreEqual(1, EncounterCatalog.All.Count(e => e.IsChampion), "exactly one champion");
            foreach (var e in EncounterCatalog.All)
            {
                var cfg = MatchConfig.Standard(42, ReferenceContent.Striker, ReferenceContent.Caster, e.UnitA, e.UnitB, e.Tier, e.AiProfile);
                Assert.IsNotNull(Match.Start(cfg, TestKit.Catalog, out var rej), e.Id + ": " + rej);
                Assert.IsNotEmpty(e.Intro, e.Id);
                Assert.IsFalse(string.IsNullOrEmpty(e.WinLine) || string.IsNullOrEmpty(e.LoseLine) || string.IsNullOrEmpty(e.RematchLine), e.Id);
            }
        }

        [Test]
        public void Encounters_CanBePlayedToTheEnd_WithTheirAi()
        {
            foreach (var e in EncounterCatalog.All)
            {
                var m = TestKit.PlayFullMatch(7, ReferenceContent.Striker, ReferenceContent.Caster, e.UnitA, e.UnitB, e.Tier, ControllerIds.AiStandard, e.AiProfile);
                Assert.AreEqual(MatchPhase.Ended, m.Phase, e.Id);
            }
        }

        [Test]
        public void Session_UsesTheEncounterOpponent()
        {
            var s = new MatchSession(TestKit.Catalog, () => 99);
            s.Options.Encounter = EncounterCatalog.Get(EncounterCatalog.Champion);
            s.ContinueFromSetup();
            s.Selection.Toggle(ReferenceContent.Striker);
            s.Selection.Toggle(ReferenceContent.Caster);
            Assert.IsTrue(s.ConfirmUnits());
            var opp = s.Match.Config.Sides[1];
            Assert.AreEqual(ReferenceContent.Ranger, opp.UnitIds[0]);
            Assert.AreEqual(ReferenceContent.Striker, opp.UnitIds[1]);
            Assert.AreEqual(ControllerIds.AiExpert, opp.ControllerId);
            Assert.AreEqual("Corvin Vale", s.OpponentName);
        }

        [Test]
        public void GameFlow_RecordsWinsAndReturnsToTheSameSpot()
        {
            var mira = EncounterCatalog.Get(EncounterCatalog.Mira);
            GameFlow.BeginEncounter(mira, 3.5f, 125f);
            Assert.AreSame(mira, GameFlow.PendingEncounter);
            GameFlow.CompleteEncounter(Winner.Player);
            Assert.IsNull(GameFlow.PendingEncounter);
            Assert.IsTrue(GameFlow.HasDefeated(EncounterCatalog.Mira));
            Assert.AreEqual(1, GameFlow.Wins);
            Assert.IsTrue(GameFlow.HasReturnPoint);
            Assert.AreEqual(3.5f, GameFlow.ReturnX);
            var outcome = GameFlow.ConsumeOutcome();
            Assert.IsTrue(outcome.PlayerWon);
            Assert.IsNull(GameFlow.ConsumeOutcome(), "outcome shown once");

            GameFlow.BeginEncounter(EncounterCatalog.Get(EncounterCatalog.Tobin), 0, 0);
            GameFlow.CompleteEncounter(Winner.Opponent);
            Assert.IsFalse(GameFlow.HasDefeated(EncounterCatalog.Tobin));
            Assert.IsFalse(GameFlow.ConsumeOutcome().PlayerWon);

            GameFlow.BeginEncounter(EncounterCatalog.Get(EncounterCatalog.Tobin), 0, 0);
            GameFlow.CompleteEncounter(Winner.None);
            Assert.IsNull(GameFlow.LastOutcome, "leaving the table early records nothing");
        }

        [Test]
        public void TownVillagers_PlayTheStartingPair_AndOnlyTheChampionHoldsAPrize()
        {
            foreach (var e in EncounterCatalog.All)
            {
                if (e.IsChampion)
                {
                    Assert.AreEqual(ReferenceContent.Ranger, e.PrizeUnit, "Brindlecross's champion holds the Ranger");
                    Assert.IsTrue(e.UnitA == e.PrizeUnit || e.UnitB == e.PrizeUnit, "the champion plays the piece you can win");
                }
                else
                {
                    Assert.IsNull(e.PrizeUnit, e.Id);
                    CollectionAssert.AreEquivalent(EncounterCatalog.StartingUnits, new[] { e.UnitA, e.UnitB }, e.Id + " plays the town's practice pair");
                }
            }
        }

        [Test]
        public void BeatingTheChampion_WinsTheRanger_Once()
        {
            CollectionAssert.AreEqual(EncounterCatalog.StartingUnits, GameFlow.UnlockedUnits);
            var champ = EncounterCatalog.Get(EncounterCatalog.Champion);
            GameFlow.BeginEncounter(champ, 0, 0);
            GameFlow.CompleteEncounter(Winner.Opponent);
            Assert.IsFalse(GameFlow.IsUnlocked(ReferenceContent.Ranger), "losing wins nothing");
            GameFlow.BeginEncounter(champ, 0, 0);
            GameFlow.CompleteEncounter(Winner.Player);
            Assert.IsTrue(GameFlow.IsUnlocked(ReferenceContent.Ranger));
            Assert.AreEqual(ReferenceContent.Ranger, GameFlow.ConsumeOutcome().UnlockedUnit);
            GameFlow.BeginEncounter(champ, 0, 0);
            GameFlow.CompleteEncounter(Winner.Player);
            Assert.IsNull(GameFlow.ConsumeOutcome().UnlockedUnit, "a rematch win does not award it again");
            Assert.AreEqual(3, GameFlow.UnlockedUnits.Count);
        }

        [Test]
        public void UnitSelection_OffersOnlyThePiecesYouOwn()
        {
            var s = new MatchSession(TestKit.Catalog, () => 5);
            s.Options.UnlockedUnits = new[] { ReferenceContent.Striker, ReferenceContent.Caster, ReferenceContent.Ranger };
            s.ContinueFromSetup();
            CollectionAssert.AreEquivalent(new[] { ReferenceContent.Striker, ReferenceContent.Caster, ReferenceContent.Ranger },
                s.SelectableUnits(false).Select(u => u.Id).ToList());
            Assert.IsNull(s.Selection.Toggle(ReferenceContent.Ranger), "a won piece can be picked");
            Assert.IsNotNull(s.Selection.Toggle(ReferenceContent.Mason), "a piece you don't own cannot");
        }

        [Test]
        public void SaveGame_RoundTripsTheJourney()
        {
            var dir = System.IO.Path.Combine(UnityEngine.Application.temporaryCachePath, "TabletopEditSaves");
            if (System.IO.Directory.Exists(dir)) System.IO.Directory.Delete(dir, true);
            SaveGame.DirectoryOverride = dir;
            try
            {
                var champ = EncounterCatalog.Get(EncounterCatalog.Champion);
                GameFlow.BeginEncounter(champ, 0, 0);
                GameFlow.CompleteEncounter(Winner.Player);
                GameFlow.BeginEncounter(EncounterCatalog.Get(EncounterCatalog.Tobin), 0, 0);
                GameFlow.CompleteEncounter(Winner.Opponent);
                GameFlow.FirstPersonView = true;
                GameFlow.TitleShown = true;
                Assert.IsTrue(SaveGame.Save(new Vector3(3.5f, 0, 125f), 90f));
                Assert.IsTrue(SaveGame.Exists);

                GameFlow.Reset();
                Assert.IsTrue(SaveGame.TryLoad(out var data));
                SaveGame.Apply(data);
                Assert.IsTrue(GameFlow.HasDefeated(EncounterCatalog.Champion));
                Assert.IsTrue(GameFlow.IsUnlocked(ReferenceContent.Ranger));
                Assert.AreEqual(1, GameFlow.Losses[EncounterCatalog.Tobin]);
                Assert.IsTrue(GameFlow.FirstPersonView);
                Assert.IsTrue(GameFlow.TitleShown);
                Assert.AreEqual(3.5f, data.x);
                Assert.AreEqual(125f, data.z);
                Assert.AreEqual(90f, data.yaw);

                System.IO.File.WriteAllText(SaveGame.FilePath, "{ not json");
                Assert.IsFalse(SaveGame.TryLoad(out _), "a damaged save reads as no save, not a crash");
            }
            finally
            {
                SaveGame.DirectoryOverride = null;
                if (System.IO.Directory.Exists(dir)) System.IO.Directory.Delete(dir, true);
            }
        }

        [Test]
        public void WalkableArea_CoversVillagesRoadAndHall_ButNotTheWoods()
        {
            var area = new WorldBuilderProbe().Walkable;
            Assert.IsTrue(area.Contains(0, -7.5f), "Hearthmoor spawn");
            foreach (var p in WorldBuilder.RoadPoints) Assert.IsTrue(area.Contains(p.x, p.y), "road point " + p);
            Assert.IsTrue(area.Contains(-5.5f, 62f), "bridge");
            Assert.IsTrue(area.Contains(-10f, 44f), "Wren's camp");
            Assert.IsTrue(area.Contains(0, 134f), "Brindlecross plaza");
            Assert.IsTrue(area.Contains(WorldBuilder.InteriorX, 0), "inside the hall");
            Assert.IsFalse(area.Contains(-30f, 62f), "the stream away from the bridge");
            Assert.IsFalse(area.Contains(40f, 80f), "deep woods");
            Assert.IsFalse(area.Contains(0, 175f), "behind the hall");
        }

        /// <summary>Builds only the walkable-area description (no GameObjects) by replaying the layout rules.</summary>
        private sealed class WorldBuilderProbe
        {
            public readonly WalkableArea Walkable;

            public WorldBuilderProbe()
            {
                var go = new GameObject("Probe");
                try
                {
                    var layout = new WorldBuilder(new WorldKit(null), null).Build(go.transform);
                    Walkable = layout.Walkable;
                }
                finally
                {
                    Object.DestroyImmediate(go);
                }
            }
        }
    }
}
