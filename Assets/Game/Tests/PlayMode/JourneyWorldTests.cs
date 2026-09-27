using System.Collections;
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
    /// <summary>D-033 in the real scenes: errands, stalls, charms, stakes and the Stonemasons' Outpost.</summary>
    public class JourneyWorldTests : MatchSceneFixture
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

        private IEnumerator StandBefore(Transform target, float yaw, float distance)
        {
            var p = target.position + Quaternion.Euler(0, yaw, 0) * Vector3.forward * distance;
            World.Player.Teleport(p, yaw + 180);
            World.SnapCamera();
            yield return null;
            yield return null;
        }

        private IEnumerator TalkWithE(Npc npc)
        {
            yield return StandBefore(npc.transform, npc.HomeYaw, 1.4f);
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
            for (int i = 0; i < 10 && World.DialogueOpen; i++) yield return Tap(Kb.enterKey);
            Assert.IsFalse(World.DialogueOpen);
        }

        private static string Label(Button b) => b.GetComponentInChildren<Text>().text;

        [UnityTest]
        public IEnumerator Errands_DeliveryAndFetch_PayCoins_WithTheKeyboard()
        {
            yield return Begin();
            int coins = GameFlow.Coins;

            // Delivery: Hollis's loaf to Wren.
            yield return TalkWithE(World.Layout.FindByName("Hollis"));
            yield return AdvanceToChoices();
            StringAssert.StartsWith("I'LL DO IT", Label(World.Ui.Choices[0]));
            yield return Tap(Kb.enterKey);
            Assert.AreEqual(ErrandStage.Active, GameFlow.ErrandState("hollis_loaf"));
            yield return CloseDialogueWithEnter();
            var wren = World.Layout.Find(EncounterCatalog.Wren);
            yield return TalkWithE(wren);
            Assert.AreEqual(ErrandCatalog.Find("hollis_loaf").DeliverLine, World.Ui.DialogueLine, "Wren takes the bread first");
            Assert.AreEqual(ErrandStage.Done, GameFlow.ErrandState("hollis_loaf"));
            Assert.AreEqual(coins + 6, GameFlow.Coins);
            yield return CloseDialogueWithEnter();

            // Fetch: Kit's Warrior, found by the barn.
            var kit = World.Layout.FindByName("Kit");
            yield return TalkWithE(kit);
            yield return AdvanceToChoices();
            yield return Tap(Kb.enterKey);
            yield return CloseDialogueWithEnter();
            Pickup toy = null;
            foreach (var p in World.Layout.Pickups) if (p.PickupId == "kit_toy") toy = p;
            Assert.IsTrue(toy.Available && toy.Visual.activeSelf, "the toy appears once the errand is under way");
            World.Player.Teleport(toy.transform.position + new Vector3(0, 0, -1f), 0);
            yield return null;
            yield return null;
            Assert.AreSame(toy, World.Nearest);
            yield return Tap(Kb.eKey);
            Assert.AreEqual(ErrandStage.Found, GameFlow.ErrandState("kit_figurine"));
            yield return CloseDialogueWithEnter();
            yield return null;
            Assert.IsFalse(toy.Available, "picked up");
            yield return TalkWithE(kit);
            Assert.AreEqual(ErrandStage.Done, GameFlow.ErrandState("kit_figurine"));
            Assert.AreEqual(coins + 6 + 4, GameFlow.Coins);
            yield return CloseDialogueWithEnter();

            // The journal lists nothing under way now.
            World.OpenJournal();
            yield return null;
            StringAssert.Contains("2 of " + ErrandCatalog.All.Count, World.List.Subtitle);
            yield return Tap(Kb.escapeKey);
            Assert.IsFalse(World.List.IsOpen, "Esc closes the journal");
        }

        [UnityTest]
        public IEnumerator Stall_CharmAtTheTable_AndTheStakeIsSettled()
        {
            yield return Begin();
            GameFlow.AddCoins(20);
            int coins = GameFlow.Coins;

            // Buy a Crown Tonic at Ada's stall with the keyboard.
            yield return TalkWithE(World.Layout.FindByName("Ada Pell"));
            yield return AdvanceToChoices();
            Assert.AreEqual("BROWSE", Label(World.Ui.Choices[0]));
            yield return Tap(Kb.enterKey);
            Assert.IsTrue(World.List.IsOpen, "the stall opens");
            Assert.AreEqual(World.List.RowButtons[0].gameObject, Selected, "first item focused");
            yield return Tap(Kb.enterKey);
            Assert.AreEqual(1, GameFlow.ItemCount(ItemCatalog.Tonic));
            Assert.AreEqual(coins - 8, GameFlow.Coins);
            yield return Tap(Kb.escapeKey);
            Assert.IsFalse(World.List.IsOpen);

            // Challenge Mira and bring it.
            var mira = World.Layout.Find(EncounterCatalog.Mira);
            yield return TalkWithE(mira);
            yield return AdvanceToChoices();
            Assert.AreEqual("PLAY FOR " + mira.Encounter.Stake, Label(World.Ui.Choices[0]));
            yield return Tap(Kb.enterKey);
            Assert.IsTrue(World.List.IsOpen, "owning a charm offers it before sitting down");
            yield return Tap(Kb.enterKey); // BRING IT (Crown Tonic)

            yield return WaitFor(() => SceneManager.GetActiveScene().name == "MatchPrototype" && Object.FindAnyObjectByType<MatchApp>() != null, 10f, "match scene");
            yield return null;
            App = Object.FindAnyObjectByType<MatchApp>();
            App.Settings.TimeScale = 0.02f;
            Assert.AreEqual(2, Session.Options.PlayerBoons.CrownBonus, "the tonic comes to the table");
            Session.Options.DeveloperMode = true;
            Session.Options.FixedSeed = 11;
            Session.Options.ScenarioName = ScenarioLibrary.Victory;
            yield return KeyboardSelectUnits();
            Assert.AreEqual(2, Session.Match.Config.Sides[0].Boons.CrownBonus);
            yield return PlayToResult(false);
            Assert.AreEqual(Winner.Player, Session.Match.Winner);
            Assert.AreEqual(mira.Encounter.Stake, App.LastSettlement.CoinDelta, "the winner takes the stake");
            Assert.AreEqual(ItemCatalog.Tonic, App.LastSettlement.CharmUsed);
            Assert.AreEqual(0, GameFlow.ItemCount(ItemCatalog.Tonic), "the charm is used up");
            Assert.IsNull(Session.Options.PlayerBoons, "a rematch would have no charm");
            StringAssert.Contains("coins", App.ResultBodyText);

            App.ResultExitButton.onClick.Invoke();
            App = null;
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "World" && Object.FindAnyObjectByType<WorldApp>() != null, 10f, "world scene");
            yield return null;
            World = Object.FindAnyObjectByType<WorldApp>();
            Assert.AreEqual(coins - 8 + mira.Encounter.Stake, GameFlow.Coins);
            Assert.AreEqual(mira.Encounter.WinLine, World.Ui.DialogueLine);
            yield return Tap(Kb.enterKey);
            StringAssert.Contains("You won " + mira.Encounter.Stake + " coins", World.Ui.DialogueLine);
        }

        [UnityTest]
        public IEnumerator Outpost_LedgeTable_SitDown_AndBrokePlayersPlayForAFavour()
        {
            yield return Begin();
            GameFlow.AddCoins(-1000);
            var chair = World.Layout.OutpostChair;
            Assert.IsNotNull(chair);
            World.Player.Teleport(chair.transform.position + new Vector3(0, 0, -1.2f), 0);
            yield return null;
            yield return null;
            Assert.AreEqual(Areas.Outpost, World.CurrentArea);
            Assert.AreSame(chair, World.Nearest, "the ledge chair is usable");
            yield return Tap(Kb.eKey);
            Assert.IsTrue(World.Seated);
            Assert.AreEqual("Master Dorran Hale", World.Ui.DialogueSpeaker);
            yield return AdvanceToChoices();
            Assert.AreEqual("PLAY FOR A FAVOUR", Label(World.Ui.Choices[0]), "no coins: he plays you for a favour");
            yield return Tap(Kb.downArrowKey);
            yield return Tap(Kb.enterKey);
            Assert.IsFalse(World.Seated, "stood up");
            Assert.Greater(World.Player.transform.position.y, WorldGround.OutpostLevel - 1f, "up on the mountain");
        }
    }
}
