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
            s.Options.Encounter = EncounterCatalog.Get(EncounterCatalog.Halvey);
            s.ContinueFromSetup();
            s.Selection.Toggle(ReferenceContent.Striker);
            s.Selection.Toggle(ReferenceContent.Caster);
            Assert.IsTrue(s.ConfirmUnits());
            var opp = s.Match.Config.Sides[1];
            Assert.AreEqual(ReferenceContent.Mender, opp.UnitIds[0]);
            Assert.AreEqual(ReferenceContent.Striker, opp.UnitIds[1]);
            Assert.AreEqual(ControllerIds.AiStandard, opp.ControllerId);
            Assert.AreEqual("Sister Halvey", s.OpponentName);
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
