using System.Collections;
using System.Linq;
using NUnit.Framework;
using Tabletop.Application;
using Tabletop.Domain;
using Tabletop.Presentation;
using Tabletop.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Tabletop.Tests.PlayMode
{
    /// <summary>D-038 in the real scenes: the new champions, the Grand Tournament at Crownhold, and wandering off the paths.</summary>
    public class RestOfJourneyWorldTests : MatchSceneFixture
    {
        private WorldApp World;

        protected override IEnumerator LoadInitialScene() => LoadWorld();

        private IEnumerator LoadWorld()
        {
            var op = SceneManager.LoadSceneAsync("World", LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            World = Object.FindAnyObjectByType<WorldApp>();
            Assert.IsNotNull(World, "WorldApp missing from World scene");
            yield return null;
        }

        private IEnumerator Begin()
        {
            World.SkipTitleForTests();
            yield return null;
            yield return null;
        }

        private IEnumerator TalkWithE(Npc npc)
        {
            var p = npc.transform.position + Quaternion.Euler(0, npc.HomeYaw, 0) * Vector3.forward * 1.4f;
            World.Player.Teleport(p, npc.HomeYaw + 180);
            World.SnapCamera();
            yield return null;
            yield return null;
            Assert.AreSame(npc, World.Nearest, npc.DisplayName + " is nearest");
            yield return Tap(Kb.eKey);
            Assert.IsTrue(World.DialogueOpen, "E talks to " + npc.DisplayName);
        }

        private IEnumerator AdvanceToChoices()
        {
            for (int i = 0; i < 10 && !World.Ui.Choices[0].gameObject.activeSelf; i++) yield return Tap(Kb.enterKey);
            Assert.IsTrue(World.Ui.Choices[0].gameObject.activeSelf, "choices shown on the last page");
        }

        private IEnumerator CloseDialogueWithEnter()
        {
            for (int i = 0; i < 12 && World.DialogueOpen; i++) yield return Tap(Kb.enterKey);
            Assert.IsFalse(World.DialogueOpen);
        }

        private static string Label(Button b) => b.GetComponentInChildren<Text>().text;

        private static void HoldEveryTitle() =>
            GameFlow.Restore(EncounterCatalog.TownChampions.Select(e => e.Id), null, null, false, null, 50);

        [UnityTest]
        public IEnumerator NewChampions_SitAtTheirTables()
        {
            yield return Begin();
            foreach (var (chair, id, area) in new[]
                     {
                         (World.Layout.LakeChair, EncounterCatalog.LakeChampion, Areas.Lanternmere),
                         (World.Layout.HollowChair, EncounterCatalog.HollowChampion, Areas.Duskhollow),
                         (World.Layout.BellChair, EncounterCatalog.BellChampion, Areas.Ironbell),
                     })
            {
                Assert.IsNotNull(chair, id);
                Assert.AreEqual(id, chair.Champion.Encounter.Id);
                World.Player.Teleport(chair.transform.position + new Vector3(0, 0, -1.2f), 0);
                yield return null;
                yield return null;
                Assert.AreEqual(area, World.CurrentArea);
                Assert.AreSame(chair, World.Nearest, "the chair at " + area + " is usable");
                yield return Tap(Kb.eKey);
                Assert.IsTrue(World.Seated);
                Assert.AreEqual(chair.Champion.DisplayName, World.Ui.DialogueSpeaker);
                yield return AdvanceToChoices();
                StringAssert.StartsWith("PLAY", Label(World.Ui.Choices[0]));
                yield return Tap(Kb.downArrowKey);
                yield return Tap(Kb.enterKey); // STAND UP
                Assert.IsFalse(World.Seated, "stood up at " + area);
            }
        }

        [UnityTest]
        public IEnumerator Herald_TurnsAwayTheUnqualified_AndEntersAChampionOfEveryTown()
        {
            yield return Begin();
            var herald = World.Layout.Herald;
            Assert.IsNotNull(herald);
            Assert.IsFalse(World.Layout.TournamentChair.Available, "the tournament table is closed to non-entrants");
            yield return TalkWithE(herald);
            for (int i = 0; i < 5 && !World.Ui.DialogueLine.Contains("Still to beat"); i++) yield return Tap(Kb.enterKey);
            StringAssert.Contains("Still to beat", World.Ui.DialogueLine);
            StringAssert.Contains("Mother Seraphine Vell", World.Ui.DialogueLine);
            yield return CloseDialogueWithEnter();
            Assert.AreEqual(0, GameFlow.TournamentRound);

            HoldEveryTitle();
            yield return TalkWithE(herald);
            yield return AdvanceToChoices();
            Assert.AreEqual("ENTER THE TOURNAMENT", Label(World.Ui.Choices[0]));
            yield return Tap(Kb.enterKey);
            Assert.AreEqual(1, GameFlow.TournamentRound);
            var chair = World.Layout.TournamentChair;
            Assert.AreEqual(EncounterCatalog.TourneyFirst, chair.Champion.Encounter.Id, "round one's opponent takes the seat");
            Assert.IsTrue(chair.Available);
            yield return CloseDialogueWithEnter();
            World.Player.Teleport(chair.transform.position + new Vector3(0, 0, -1.2f), 0);
            yield return null;
            yield return null;
            Assert.AreSame(chair, World.Nearest);
            yield return Tap(Kb.eKey);
            yield return AdvanceToChoices();
            Assert.AreEqual("PLAY ROUND 1", Label(World.Ui.Choices[0]));
        }

        [UnityTest]
        public IEnumerator Tournament_WinARound_AndTheNextOpponentTakesTheSeat()
        {
            yield return Begin();
            HoldEveryTitle();
            Assert.IsTrue(GameFlow.EnterTournament());
            World.RefreshTournament();
            var chair = World.Layout.TournamentChair;
            World.Player.Teleport(chair.transform.position + new Vector3(0, 0, -1.2f), 0);
            yield return null;
            yield return null;
            yield return Tap(Kb.eKey);
            yield return AdvanceToChoices();
            yield return Tap(Kb.enterKey); // PLAY ROUND 1

            yield return WaitFor(() => SceneManager.GetActiveScene().name == "MatchPrototype" && Object.FindAnyObjectByType<MatchApp>() != null, 10f, "match scene");
            yield return null;
            App = Object.FindAnyObjectByType<MatchApp>();
            App.Settings.TimeScale = 0.02f;
            Assert.AreEqual(EncounterCatalog.TourneyFirst, Session.Options.Encounter.Id);
            Session.Options.DeveloperMode = true;
            Session.Options.FixedSeed = 11;
            Session.Options.ScenarioName = ScenarioLibrary.Victory;
            yield return KeyboardSelectUnits();
            Assert.AreEqual(ReelTier.Diamond, Session.Match.Config.Sides[1].ReelTier, "the tournament opponent brings their wheel");
            yield return PlayToResult(false);
            Assert.AreEqual(Winner.Player, Session.Match.Winner);
            Assert.AreEqual(60, App.LastSettlement.CoinDelta, "round one's purse");
            StringAssert.Contains("round 1", App.ResultBodyText);

            App.ResultExitButton.onClick.Invoke();
            App = null;
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "World" && Object.FindAnyObjectByType<WorldApp>() != null, 10f, "world scene");
            yield return null;
            World = Object.FindAnyObjectByType<WorldApp>();
            Assert.AreEqual(2, GameFlow.TournamentRound);
            Assert.AreEqual(EncounterCatalog.TourneySecond, World.Layout.TournamentChair.Champion.Encounter.Id, "round two's opponent is seated");
            for (int i = 0; i < 6 && !World.Ui.DialogueLine.Contains("goes to the challenger"); i++) yield return Tap(Kb.enterKey);
            StringAssert.Contains("Lord Casimir Vane", World.Ui.DialogueLine, "the Herald announces the next round");
        }

        [UnityTest]
        public IEnumerator Wandering_WalksOffTheRoadIntoTheMeadow_ButNotIntoTheStream()
        {
            yield return Begin();
            World.Player.Teleport(new Vector3(-3, 0, 90), 270);
            yield return null;
            Press(Kb.aKey);
            for (int i = 0; i < 90; i++) yield return null;
            Release(Kb.aKey);
            yield return null;
            var p = World.Player.transform.position;
            Assert.Less(p.x, -9f, "walked west off the road into the open land (no invisible wall), at " + p);
            Assert.IsFalse(World.Layout.Walkable.Contains(p.x, p.z), "off the paths");

            World.Player.Teleport(new Vector3(-20, 0, 70), 180);
            yield return null;
            Press(Kb.sKey);
            for (int i = 0; i < 90; i++) yield return null;
            Release(Kb.sKey);
            yield return null;
            Assert.Greater(World.Player.transform.position.z, WorldGround.StreamZ + 2f, "the stream is a real barrier");
        }

        [UnityTest]
        public IEnumerator Trees_BetweenTheCameraAndThePlayer_KeepOnlyTheirShadow()
        {
            yield return Begin();
            Occluder tree = null;
            Vector3 spot = Vector3.zero;
            foreach (var o in World.Layout.Occluders)
            {
                var c = o.Bounds.center;
                var behind = new Vector3(c.x, 0, c.z + o.Bounds.extents.z + 1.2f);
                if (o.Bounds.size.y < 6f || !World.Layout.Roam.CanStand(behind.x, behind.z)) continue;
                tree = o;
                spot = behind;
                break;
            }
            Assert.IsNotNull(tree, "a tall occluder the player can stand behind");
            World.Player.Teleport(spot, 0);
            World.SnapCamera();
            yield return null;
            yield return null;
            Assert.IsTrue(tree.Hidden, "the tree in front of the camera keeps only its shadow");
            World.Player.Teleport(new Vector3(0, 0, 134f), 0); // the open plaza, far from the tree
            World.SnapCamera();
            yield return null;
            yield return null;
            Assert.IsFalse(tree.Hidden, "and comes back when the player moves on");
        }
    }
}
