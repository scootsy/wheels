using System.Collections;
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
    /// <summary>D-027 world features: sprint/jump, name fade, first person, deck, save/continue, won pieces at the table.</summary>
    public class WorldFeatureTests : MatchSceneFixture
    {
        private WorldApp World;
        private string Diag = "";

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

        /// <summary>
        /// Holds keys for a stretch of game time and reports the ground speed (m/s). Keys go down on separate frames:
        /// the test fixture builds each key event from the last applied state, so two presses queued in one frame
        /// would overwrite each other (a real keyboard sends complete state).
        /// </summary>
        private IEnumerator HoldAndMeasure(float[] result, float seconds, params UnityEngine.InputSystem.Controls.ButtonControl[] keys)
        {
            var start = World.Player.transform.position;
            foreach (var k in keys) { Press(k); yield return null; }
            yield return null;
            float t0 = Time.time;
            var p0 = World.Player.transform.position;
            while (Time.time - t0 < seconds) yield return null;
            var p1 = World.Player.transform.position;
            float elapsed = Time.time - t0;
            Diag = "move=" + World.Input.Move + " sprint=" + World.Input.SprintHeld + " busy=" + World.Busy + " p0=" + p0 + " p1=" + p1 + " t=" + elapsed;
            foreach (var k in keys) Release(k);
            yield return null;
            p0.y = p1.y = 0;
            result[0] = Vector3.Distance(p0, p1) / Mathf.Max(elapsed, 1e-4f);
            World.Player.Teleport(start, 0);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Sprint_IsFaster_AndJump_LeavesTheGroundAndLands()
        {
            yield return Begin();
            World.Player.Teleport(new Vector3(0, 0, -5f), 0);
            yield return null;
            var walk = new float[1];
            var sprint = new float[1];
            yield return HoldAndMeasure(walk, 0.25f, Kb.wKey);
            yield return HoldAndMeasure(sprint, 0.25f, Kb.leftShiftKey, Kb.wKey);
            Assert.Greater(walk[0], 1f, "walking moves");
            Assert.Greater(sprint[0], walk[0] * 1.5f, "sprinting is clearly faster (" + walk[0] + " vs " + sprint[0] + " m/s) " + Diag);

            yield return Tap(Kb.spaceKey);
            float peak = 0;
            float jumpStart = Time.time;
            while (Time.time - jumpStart < 2f && (peak == 0 || !World.Player.Grounded))
            {
                peak = Mathf.Max(peak, World.Player.Height);
                yield return null;
            }
            Assert.Greater(peak, 0.4f, "space jumps");
            Assert.IsTrue(World.Player.Grounded, "and lands again");
            Assert.AreEqual(0f, World.Player.transform.position.y, 1e-4f);
        }

        [UnityTest]
        public IEnumerator NameTags_FadeInOnlyWhenYouAreClose()
        {
            yield return Begin();
            var gran = World.Layout.Find(EncounterCatalog.Gran);
            World.Player.Teleport(gran.transform.position + new Vector3(0, 0, -2.5f), 0);
            yield return null;
            yield return null;
            Assert.Greater(gran.TagAlpha, 0.95f, "close: name visible");
            World.Player.Teleport(gran.transform.position + new Vector3(0, 0, -16f), 0);
            yield return null;
            yield return null;
            Assert.Less(gran.TagAlpha, 0.05f, "far: name hidden");
        }

        [UnityTest]
        public IEnumerator NameTags_ShowTheWholeName_AndFloatClearOfTheHead()
        {
            yield return Begin();
            yield return null;
            foreach (var npc in World.Layout.Npcs)
            {
                var tag = npc.Tag;
                Assert.AreEqual(npc.DisplayName, tag.Name.text, npc.DisplayName);
                // Regression (D-028): uGUI drops a line taller than its box, which hid every name.
                Assert.GreaterOrEqual(tag.Name.rectTransform.rect.height + 0.5f, tag.Name.preferredHeight, npc.DisplayName + ": name line clipped");
                Assert.AreEqual(VerticalWrapMode.Overflow, tag.Name.verticalOverflow);
                float top = float.MinValue;
                foreach (var r in npc.GetComponentsInChildren<Renderer>()) top = Mathf.Max(top, r.bounds.max.y);
                var corners = new Vector3[4];
                tag.Card.rectTransform.GetWorldCorners(corners);
                float plateBottom = Mathf.Min(corners[0].y, corners[1].y, corners[2].y, corners[3].y);
                Assert.Greater(plateBottom, top + 0.2f, npc.DisplayName + ": the nameplate must not cover the person");
            }
        }

        [UnityTest]
        public IEnumerator FirstPerson_TogglesWithV_AndWalksWhereYouLook()
        {
            yield return Begin();
            Assert.IsFalse(World.FirstPerson);
            World.Player.Teleport(new Vector3(0, 0, -5f), 0);
            yield return Tap(Kb.vKey);
            Assert.IsTrue(World.FirstPerson, "V switches to first person");
            Assert.AreEqual(WorldApp.FirstPersonFov, World.WorldCamera.fieldOfView, 0.01f);
            World.SetFirstPersonYaw(90f); // look east
            yield return null;
            var start = World.Player.transform.position;
            Press(Kb.wKey);
            float t0 = Time.time;
            while (Time.time - t0 < 0.3f) yield return null;
            Release(Kb.wKey);
            yield return null;
            var moved = World.Player.transform.position - start;
            Assert.Greater(moved.x, 0.3f, "forward is where you look (east)");
            Assert.Less(Mathf.Abs(moved.z), 0.2f);
            Assert.Less(Vector3.Distance(World.WorldCamera.transform.position, World.Player.transform.position + Vector3.up * WorldApp.EyeHeight), 0.01f, "camera at eye height");

            yield return Tap(Kb.vKey);
            Assert.IsFalse(World.FirstPerson, "V switches back");
            Assert.AreEqual(WorldApp.OverheadFov, World.WorldCamera.fieldOfView, 0.01f);
        }

        [UnityTest]
        public IEnumerator Deck_OpensWithI_ShowsStats_AndWhereToWinLockedPieces()
        {
            yield return Begin();
            yield return Tap(Kb.iKey);
            Assert.IsTrue(World.Deck.IsOpen, "I opens the deck");
            Assert.AreEqual(World.Deck.Cards[0].gameObject, Selected, "first piece focused");
            // Every piece must fit on screen (there are 7; an early layout pushed the first card off the left edge).
            var frame = new Vector3[4];
            ((RectTransform)World.Ui.Frame).GetWorldCorners(frame);
            foreach (var card in World.Deck.Cards)
            {
                var c = new Vector3[4];
                ((RectTransform)card.transform).GetWorldCorners(c);
                Assert.GreaterOrEqual(c[0].x, frame[0].x - 0.5f, card.name + " off the left edge");
                Assert.LessOrEqual(c[2].x, frame[2].x + 0.5f, card.name + " off the right edge");
            }
            StringAssert.Contains("WARRIOR", World.Deck.DetailText); // the source's names (D-033)
            StringAssert.Contains("BRONZE", World.Deck.DetailText);
            int ranger = -1;
            for (int i = 0; i < World.Deck.UnitOrder.Count; i++) if (World.Deck.UnitOrder[i] == ReferenceContent.Ranger) ranger = i;
            Assert.GreaterOrEqual(ranger, 0);
            for (int i = 0; i < ranger; i++) yield return Tap(Kb.rightArrowKey);
            StringAssert.Contains("ARCHER", World.Deck.DetailText);
            StringAssert.Contains("LOCKED", World.Deck.DetailText);
            StringAssert.Contains("Corvin Vale", World.Deck.DetailText, "says who to beat");
            yield return Tap(Kb.escapeKey);
            Assert.IsFalse(World.Deck.IsOpen, "Esc closes the deck");
            Assert.IsFalse(World.Ui.Pause.activeSelf, "without also opening the menu");
        }

        [UnityTest]
        public IEnumerator Continue_ResumesTheSavedJourney()
        {
            yield return Begin();
            Assert.IsTrue(SaveGame.Exists, "a new journey saves immediately");
            GameFlow.BeginEncounter(EncounterCatalog.Get(EncounterCatalog.Champion), 0, 0);
            GameFlow.CompleteEncounter(Winner.Player);
            GameFlow.ConsumeOutcome();
            GameFlow.ConsumeReturnPoint();
            World.Player.Teleport(new Vector3(3f, 0, 125f), 0);
            Assert.IsTrue(World.SaveNow());

            GameFlow.Reset(); // as if the game were closed and opened again
            yield return LoadWorld();
            Assert.IsTrue(World.Ui.Title.activeSelf);
            Assert.AreEqual(World.Ui.TitleContinue.gameObject, Selected, "CONTINUE offered and focused");
            yield return Tap(Kb.enterKey);
            Assert.IsFalse(World.Ui.Title.activeSelf);
            Assert.IsTrue(GameFlow.HasDefeated(EncounterCatalog.Champion), "wins restored");
            Assert.IsTrue(GameFlow.IsUnlocked(ReferenceContent.Ranger), "won pieces restored");
            var at = World.Player.transform.position;
            Assert.Less(Vector2.Distance(new Vector2(3f, 125f), new Vector2(at.x, at.z)), 0.1f, "back where you were");
            Assert.AreEqual(WorldGround.Walk(3f, 125f), at.y, 0.01f, "standing on the ground");
            Assert.AreEqual("Brindlecross", World.CurrentArea);
        }

        [UnityTest]
        public IEnumerator NewJourney_OverASave_AsksForASecondPress()
        {
            yield return Begin();
            GameFlow.Reset();
            yield return LoadWorld();
            Assert.IsTrue(World.Ui.TitleContinue.gameObject.activeSelf);
            World.Ui.TitleBegin.onClick.Invoke();
            Assert.IsTrue(World.Ui.Title.activeSelf, "first press only asks");
            World.Ui.TitleBegin.onClick.Invoke();
            Assert.IsFalse(World.Ui.Title.activeSelf, "second press starts over");
            Assert.AreEqual(0, GameFlow.Wins);
        }

        [UnityTest]
        public IEnumerator WonPieces_AppearAtTheTable()
        {
            yield return Begin();
            GameFlow.BeginEncounter(EncounterCatalog.Get(EncounterCatalog.Champion), 0, 0);
            GameFlow.CompleteEncounter(Winner.Player);
            GameFlow.ConsumeOutcome();
            GameFlow.BeginEncounter(EncounterCatalog.Get(EncounterCatalog.Gran), 0, -7.5f);
            var op = SceneManager.LoadSceneAsync("MatchPrototype", LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            App = Object.FindAnyObjectByType<MatchApp>();
            yield return null;
            Assert.AreEqual(UxState.UnitSelect, Session.State);
            Assert.AreEqual(3, App.UnitCardButtons.Count, "Striker, Caster and the won Ranger");
            Assert.IsNull(Session.Selection.Toggle(ReferenceContent.Ranger), "the Ranger can be picked");
        }

        [UnityTest]
        public IEnumerator Map_OpensWithM_ShowsWhereYouAreAndWhoPlays_AndEscClosesIt()
        {
            yield return Begin();
            World.Player.Teleport(new Vector3(0, 0, 134f), 0); // Brindlecross plaza
            yield return null;
            yield return null;
            Assert.IsTrue(World.Map.MinimapShown, "the minimap shows while exploring (D-041)");
            Assert.IsNotNull(World.Map.Texture);
            Assert.AreEqual(WorldMap.TextureWidth, World.Map.Texture.width);

            yield return Tap(Kb.mKey);
            Assert.IsTrue(World.Map.IsOpen, "M opens the map");
            Assert.IsTrue(World.Busy, "the map stops the player walking");
            Assert.IsFalse(World.Map.MinimapShown, "no minimap over the full map");
            Assert.AreEqual(World.Map.Close.gameObject, Selected, "CLOSE has focus");
            var ext = WorldGround.Extent;
            Assert.AreEqual((0f - ext.xMin) / ext.width, World.Map.PlayerUv.x, 0.01f);
            Assert.AreEqual((134f - ext.yMin) / ext.height, World.Map.PlayerUv.y, 0.01f);
            Assert.Greater(World.Map.Count(WorldMap.MarkerKind.Challenger), 3, "challengers are marked");
            Assert.Greater(World.Map.Count(WorldMap.MarkerKind.Champion), 0, "champions are marked");
            Assert.Greater(World.Map.Count(WorldMap.MarkerKind.Stall), 0, "stalls are marked");

            yield return Tap(Kb.escapeKey);
            Assert.IsFalse(World.Map.IsOpen, "Esc closes the map");
            Assert.IsFalse(World.Ui.Pause.activeSelf, "without also opening the menu");

            // A beaten player's marker changes; inside the hall you show at its door.
            GameFlow.DevSetDefeated(EncounterCatalog.Gran, true);
            World.Player.Teleport(World.Layout.HallExit.transform.position, 0);
            yield return Tap(Kb.mKey);
            Assert.Greater(World.Map.Count(WorldMap.MarkerKind.Beaten), 0, "Gran shows as beaten");
            var door = World.Map.UvOf(World.Layout.HallDoor.transform.position);
            Assert.Less(Vector2.Distance(door, World.Map.PlayerUv), 0.001f, "inside the Champion's Hall you show at its door");
            yield return Tap(Kb.mKey);
            Assert.IsFalse(World.Map.IsOpen, "M closes it again");
        }

        [UnityTest]
        public IEnumerator Minimap_HidesForMenus_AndItsSwitchIsSaved()
        {
            yield return Begin();
            yield return null;
            Assert.IsTrue(World.Map.MinimapShown);
            yield return Tap(Kb.escapeKey);
            Assert.IsTrue(World.Ui.Pause.activeSelf);
            yield return null;
            Assert.IsFalse(World.Map.MinimapShown, "hidden under the pause menu");
            World.Ui.PauseMap.onClick.Invoke();
            yield return null;
            Assert.IsTrue(World.Map.IsOpen, "the pause menu's MAP opens the map");
            World.Map.MinimapToggle.onClick.Invoke();
            Assert.IsTrue(GameFlow.MinimapHidden, "MINIMAP: OFF");
            Assert.IsTrue(SaveGame.TryLoad(out var saved) && saved.hideMinimap, "the choice is saved");
            World.Map.Close.onClick.Invoke();
            yield return null;
            yield return null;
            Assert.IsFalse(World.Map.MinimapShown, "switched off: no minimap while exploring");
            GameFlow.MinimapHidden = false;
            yield return null;
            Assert.IsTrue(World.Map.MinimapShown);
        }

        [UnityTest]
        public IEnumerator GodWheels_FromTheDevTools_ReachTheTable_ForThePlayerOnly()
        {
            yield return Begin();
            GameFlow.GodWheels = true; // as the DEV TOOLS switch sets it (D-040)
            GameFlow.BeginEncounter(EncounterCatalog.Get(EncounterCatalog.Champion), 0, 0);
            var op = SceneManager.LoadSceneAsync("MatchPrototype", LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            App = Object.FindAnyObjectByType<MatchApp>();
            App.Settings.TimeScale = 0.02f;
            yield return null;
            Assert.AreEqual(UxState.UnitSelect, Session.State);
            Session.Selection.Toggle(ReferenceContent.Striker);
            Session.Selection.Toggle(ReferenceContent.Caster);
            Assert.IsTrue(Session.ConfirmUnits(), Session.LastStatus);
            Assert.AreEqual(ReelTier.God, Session.Match.Config.Sides[0].ReelTier, "you bring the god wheels");
            Assert.AreEqual(ReelTier.Bronze, Session.Match.Config.Sides[1].ReelTier, "Corvin keeps his Bronze wheel");
            var godFaces = ReferenceContent.GodReelCodes.Split(',');
            for (int r = 0; r < 5; r++)
                Assert.AreEqual(godFaces.Length, Session.Match.ReelDefinitions(SideId.Player)[r].Faces.Count);
            Assert.AreEqual("SSS+", Session.Match.ReelDefinitions(SideId.Player)[0].Faces[0].Code, "the table spins the god faces");
            GameFlow.GodWheels = false;
        }
    }
}
