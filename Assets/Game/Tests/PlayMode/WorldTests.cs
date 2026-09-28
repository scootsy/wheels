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

namespace Tabletop.Tests.PlayMode
{
    /// <summary>The explorable world slice (D-025): title, walking, talking, challenging, and returning from the table.</summary>
    public class WorldTests : MatchSceneFixture
    {
        private WorldApp World;

        protected override IEnumerator LoadInitialScene() => LoadWorldScene();

        private IEnumerator LoadWorldScene()
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

        /// <summary>Stand in front of something (in the direction it faces) and let a frame pick it as nearest.</summary>
        private IEnumerator StandBefore(Transform target, float yaw, float distance)
        {
            var p = target.position + Quaternion.Euler(0, yaw, 0) * Vector3.forward * distance;
            p.y = 0;
            World.Player.Teleport(p, yaw + 180);
            World.SnapCamera();
            yield return null;
            yield return null;
        }

        private IEnumerator AdvanceToChoices()
        {
            for (int i = 0; i < 10 && !World.Ui.Choices[0].gameObject.activeSelf; i++)
            {
                Assert.IsTrue(World.DialogueOpen, "dialogue should stay open while paging");
                yield return Tap(Kb.enterKey);
            }
            Assert.IsTrue(World.Ui.Choices[0].gameObject.activeSelf, "choices shown on the last page");
            Assert.AreEqual(World.Ui.Choices[0].gameObject, Selected, "first choice focused");
        }

        [UnityTest]
        public IEnumerator Title_BeginsWithKeyboard_AndPlayerWalksWithKeyboardAndGamepad()
        {
            Assert.IsTrue(World.Ui.Title.activeSelf, "title shown on first load");
            Assert.AreEqual(World.Ui.TitleBegin.gameObject, Selected, "BEGIN focused");
            yield return Tap(Kb.enterKey);
            Assert.IsFalse(World.Ui.Title.activeSelf, "Enter begins the journey");
            Assert.IsTrue(GameFlow.TitleShown);
            Assert.AreEqual("Hearthmoor", World.CurrentArea);

            var start = World.Player.transform.position;
            Press(Kb.wKey);
            for (int i = 0; i < 30; i++) yield return null;
            string diag = "move=" + World.Input.Move + " busy=" + World.Busy + " scheme=" + World.Input.ActiveScheme
                          + " dt=" + Time.deltaTime + " pos=" + World.Player.transform.position;
            Release(Kb.wKey);
            yield return null;
            Assert.Greater(World.Player.transform.position.z, start.z + 0.2f, "W walks north: " + diag);

            var mid = World.Player.transform.position;
            Set(Pad.leftStick, new Vector2(1, 0));
            for (int i = 0; i < 30; i++) yield return null;
            Set(Pad.leftStick, Vector2.zero);
            yield return null;
            Assert.Greater(World.Player.transform.position.x, mid.x + 0.2f, "left stick walks east");

            yield return Tap(Pad.startButton);
            Assert.IsTrue(World.Ui.Pause.activeSelf, "Start opens the menu");
            Assert.AreEqual(World.Ui.PauseResume.gameObject, Selected, "RESUME focused");
            yield return Tap(Pad.buttonEast);
            Assert.IsFalse(World.Ui.Pause.activeSelf, "B closes the menu");
        }

        [UnityTest]
        public IEnumerator Player_IsStoppedByTheVillageFence()
        {
            yield return Begin();
            World.Player.Teleport(new Vector3(-19.5f, 0, -7.5f), 270);
            yield return null;
            Press(Kb.aKey);
            for (int i = 0; i < 90; i++) yield return null;
            Release(Kb.aKey);
            yield return null;
            var p = World.Player.transform.position;
            Assert.IsTrue(World.Layout.Roam.CanStand(p.x, p.z), "player stays on walkable ground, at " + p);
            Assert.Greater(p.x, -22f, "the fence around Hearthmoor holds (a real barrier, D-038)");
        }

        [UnityTest]
        public IEnumerator Villager_TalksWithGamepad_AndClosesWithCancel()
        {
            yield return Begin();
            Npc villager = null;
            foreach (var n in World.Layout.Npcs) if (n.Encounter == null && n.Radius > 0) { villager = n; break; }
            Assert.IsNotNull(villager, "a non-challenger villager exists");
            yield return StandBefore(villager.transform, villager.HomeYaw, 1.4f);
            Assert.AreSame(villager, World.Nearest, "villager is the nearest interactable");
            StringAssert.Contains(villager.DisplayName, World.Ui.PromptText);
            yield return Tap(Pad.buttonSouth);
            Assert.IsTrue(World.DialogueOpen, "A talks");
            Assert.AreEqual(villager.DisplayName, World.Ui.DialogueSpeaker);
            Assert.IsFalse(World.Ui.Choices[0].gameObject.activeSelf, "villagers offer no challenge");
            yield return Tap(Pad.buttonEast);
            Assert.IsFalse(World.DialogueOpen, "B closes the conversation");
        }

        [UnityTest]
        public IEnumerator Challenge_Gran_PlaysAtTheTable_AndAWinReturnsToTheVillage()
        {
            yield return Begin();
            var gran = World.Layout.Find(EncounterCatalog.Gran);
            Assert.IsNotNull(gran);
            yield return StandBefore(gran.transform, gran.HomeYaw, 1.4f);
            Assert.AreSame(gran, World.Nearest);
            yield return Tap(Kb.eKey);
            Assert.IsTrue(World.DialogueOpen, "E talks");
            Assert.AreEqual(gran.DisplayName, World.Ui.DialogueSpeaker);
            yield return AdvanceToChoices();
            var returnSpot = World.Player.transform.position;
            yield return Tap(Kb.enterKey); // CHALLENGE

            yield return WaitFor(() => SceneManager.GetActiveScene().name == "MatchPrototype" && Object.FindAnyObjectByType<MatchApp>() != null, 10f, "match scene");
            yield return null;
            App = Object.FindAnyObjectByType<MatchApp>();
            App.Settings.TimeScale = 0.02f;
            Assert.IsTrue(App.InEncounter);
            Assert.AreEqual(EncounterCatalog.Gran, Session.Options.Encounter.Id);
            Assert.AreEqual(UxState.UnitSelect, Session.State, "a challenge skips the practice setup screen");

            Session.Options.DeveloperMode = true;
            Session.Options.FixedSeed = 11;
            Session.Options.ScenarioName = ScenarioLibrary.Victory;
            yield return KeyboardSelectUnits();
            Assert.AreEqual(ControllerIds.AiLearner, Session.Match.Config.Sides[1].ControllerId, "Gran plays the Learner AI");
            yield return PlayToResult(false);
            Assert.AreEqual(Winner.Player, Session.Match.Winner);
            StringAssert.Contains("VILLAGE", App.ResultExitButton.GetComponentInChildren<UnityEngine.UI.Text>().text);

            App.ResultExitButton.onClick.Invoke();
            App = null;
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "World" && Object.FindAnyObjectByType<WorldApp>() != null, 10f, "world scene");
            yield return null;
            World = Object.FindAnyObjectByType<WorldApp>();
            Assert.IsTrue(GameFlow.HasDefeated(EncounterCatalog.Gran), "win recorded");
            Assert.IsFalse(World.Ui.Title.activeSelf, "no title on return");
            Assert.IsTrue(World.DialogueOpen, "the opponent reacts to the result");
            Assert.AreEqual(gran.DisplayName, World.Ui.DialogueSpeaker);
            Assert.AreEqual(EncounterCatalog.Get(EncounterCatalog.Gran).WinLine, World.Ui.DialogueLine);
            Assert.Less(Vector3.Distance(returnSpot, World.Player.transform.position), 0.5f, "back where we stood");
        }

        [UnityTest]
        public IEnumerator People_AreAnimatedCharacters_StandOnTheGround_AndNothingRendersMagenta()
        {
            yield return Begin();
            Assert.IsNotNull(World.Art, "World Art Set assigned in the scene");
            bool characters = World.LookAsset != null && World.LookAsset.HasCharacters;
            Assert.AreEqual(characters, World.Player.Rig != null, "the player is an animated character when the characters pack is present");
            foreach (var npc in World.Layout.Npcs)
            {
                var slot = World.Art.Person(npc.DisplayName);
                var model = npc.Figure.Find("Model");
                if (slot != null)
                {
                    Assert.IsNotNull(model, npc.DisplayName + ": the art set's model is used (D-026)");
                    continue;
                }
                if (!characters) continue;
                Assert.IsNotNull(npc.Rig, npc.DisplayName + " is an animated KayKit character (D-033)");
                Assert.IsNotNull(model, npc.DisplayName);
                if (npc.Rig.Sitting) continue;
                var b = WorldKit.LocalBounds(model, npc.transform);
                // The idle animation breathes, so allow a little either way.
                Assert.AreEqual(0f, b.min.y, 0.15f, npc.DisplayName + " stands on the ground");
                Assert.AreEqual(npc.transform.position.y, WorldGround.Walk(npc.transform.position.x, npc.transform.position.z), 0.01f, npc.DisplayName + " is placed on the land");
                Assert.That(b.size.y, Is.InRange(1.1f, 2.3f), npc.DisplayName + " height");
            }
            foreach (var slot in World.Art.buildings.Where(s => s.model != null))
                Assert.IsNotNull(World.Layout.Root.Find("Models/" + slot.who.Substring(slot.who.IndexOf('/') + 1)), slot.who + " placed");
            // Pack shaders are fine as long as they can draw; an unsupported one would show magenta in the build.
            foreach (var r in World.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                    if (m != null)
                    {
                        Assert.IsTrue(m.shader.isSupported, r.name + ": " + m.shader.name + " would render magenta in the build");
                        StringAssert.DoesNotStartWith("Hidden/InternalErrorShader", m.shader.name, r.name);
                    }
        }

        [UnityTest]
        public IEnumerator ChampionsHall_EnterSitDown_AndStandUp()
        {
            yield return Begin();
            var door = World.Layout.HallDoor;
            World.Player.Teleport(door.transform.position + new Vector3(0, 0, -1.4f), 0);
            yield return null;
            yield return null;
            Assert.AreSame(door, World.Nearest, "the hall door is usable");
            yield return Tap(Kb.eKey);
            yield return WaitFor(() => World.Player.transform.position.x > 150 && !World.Busy, 5f, "enter the hall");
            Assert.AreEqual(WorldLayout.AreaAt(new Vector3(WorldBuilder.InteriorX, 0, 0)), World.CurrentArea, "inside the hall");

            var chair = World.Layout.ChampionChair;
            World.Player.Teleport(chair.transform.position + new Vector3(0, 0, -1.2f), 0);
            yield return null;
            yield return null;
            Assert.AreSame(chair, World.Nearest, "the empty chair is usable");
            yield return Tap(Kb.eKey);
            Assert.IsTrue(World.Seated, "sat down");
            Assert.AreEqual(chair.Champion.DisplayName, World.Ui.DialogueSpeaker, "the champion speaks");
            yield return AdvanceToChoices();
            yield return Tap(Kb.downArrowKey);
            Assert.AreEqual(World.Ui.Choices[1].gameObject, Selected, "STAND UP reachable");
            yield return Tap(Kb.enterKey);
            Assert.IsFalse(World.Seated, "stood up");
            Assert.IsFalse(World.DialogueOpen);
        }
    }
}
