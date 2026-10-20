using System.Collections;
using NUnit.Framework;
using Tabletop.Application;
using Tabletop.Presentation;
using Tabletop.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Tabletop.Tests.PlayMode
{
    /// <summary>
    /// A phone or tablet: a touchscreen and nothing else (D-037). Taps must reach menus, the on-screen stick must
    /// walk, the pad must give way to a real controller, and a match must start and spin by touch alone.
    /// </summary>
    public class TouchDeviceTests : InputTestFixture
    {
        private int _touchId;

        /// <summary>Deferred to [UnitySetUp], as in MatchSceneFixture: the virtual input runtime must exist before the scene loads.</summary>
        public override void Setup() { }

        [UnitySetUp]
        public IEnumerator SetupTouchOnly()
        {
            GameFlow.Reset();
            var saves = System.IO.Path.Combine(UnityEngine.Application.temporaryCachePath, "TabletopTestSaves");
            if (System.IO.Directory.Exists(saves)) System.IO.Directory.Delete(saves, true);
            SaveGame.DirectoryOverride = saves;
            base.Setup();
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.AddDevice<Touchscreen>();
            yield return null;
        }

        public override void TearDown()
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects()) Object.DestroyImmediate(root);
            base.TearDown();
        }

        private static IEnumerator Load(string scene)
        {
            var op = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            yield return null;
        }

        private static Vector2 ScreenPoint(Component c)
        {
            var rt = (RectTransform)c.transform;
            return RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));
        }

        private IEnumerator TapOn(Component c)
        {
            var p = ScreenPoint(c);
            int id = ++_touchId;
            BeginTouch(id, p);
            yield return null;
            yield return null;
            EndTouch(id, p);
            yield return null;
            yield return null;
        }

        private static IEnumerator WaitFor(System.Func<bool> condition, string what, float seconds = 20f)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out waiting for " + what);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator World_TapBegins_StickWalks_PadHidesForARealController()
        {
            yield return Load("World");
            var world = Object.FindAnyObjectByType<WorldApp>();
            Assert.IsNotNull(world.TouchPad, "a touchscreen gets the on-screen pad");
            Assert.IsTrue(world.Ui.Title.activeSelf, "title shown");

            yield return TapOn(world.Ui.TitleBegin);
            Assert.IsFalse(world.Ui.Title.activeSelf, "tapping BEGIN starts the journey");
            yield return null;
            Assert.IsTrue(world.TouchPad.Visible, "pad shown while exploring");

            // Drag the stick knob to the right and hold it.
            var start = world.Player.transform.position;
            var knob = ScreenPoint(world.TouchPad.StickKnob);
            var corners = new Vector3[4];
            world.TouchPad.StickKnob.GetWorldCorners(corners);
            float reach = corners[2].x - corners[0].x;
            int id = ++_touchId;
            BeginTouch(id, knob);
            yield return null;
            for (int i = 1; i <= 5; i++)
            {
                MoveTouch(id, knob + new Vector2(reach * i / 5f, 0));
                yield return null;
            }
            for (int i = 0; i < 30; i++) yield return null;
            var moved = world.Player.transform.position;
            EndTouch(id, knob + new Vector2(reach, 0));
            yield return null;
            Assert.Greater(moved.x, start.x + 0.2f, "dragging the stick right walks east");

            // A real controller replaces the pad, and the pad comes back when it is gone.
            var pad = InputSystem.AddDevice<Gamepad>();
            yield return null;
            yield return null;
            Assert.IsFalse(world.TouchPad.Visible, "a connected controller hides the on-screen pad");
            InputSystem.RemoveDevice(pad);
            yield return null;
            yield return null;
            Assert.IsTrue(world.TouchPad.Visible, "the pad returns when the controller is gone");
        }

        [UnityTest]
        public IEnumerator World_PauseMenuShowsPlaceCoinsAndControls_NoCornerPanels()
        {
            yield return Load("World");
            var world = Object.FindAnyObjectByType<WorldApp>();
            world.SkipTitleForTests();
            yield return null;
            yield return null;
            Assert.IsNull(world.Ui.Frame.Find("HudBg"), "no always-on place/coins panel");
            Assert.IsNull(world.Ui.Frame.Find("ControlsBg"), "no always-on controls panel");

            // The on-screen MENU button opens the pause menu.
            var menu = world.TouchPad.Root.transform.Find("PadMENU");
            Assert.IsNotNull(menu, "MENU button");
            yield return TapOn(menu);
            yield return WaitFor(() => world.Ui.Pause.activeSelf, "pause menu");
            StringAssert.Contains("HEARTHMOOR", world.Ui.HudText);
            StringAssert.Contains("Coins:", world.Ui.HudText);
            StringAssert.Contains("Walk:", world.Ui.ControlsText);
            yield return TapOn(world.Ui.PauseResume);
            Assert.IsFalse(world.Ui.Pause.activeSelf, "tapping RESUME closes the menu");
        }

        [UnityTest]
        public IEnumerator Match_ChooseUnitsConfirmAndSpin_ByTouchAlone()
        {
            yield return Load("MatchPrototype");
            var app = Object.FindAnyObjectByType<MatchApp>();
            app.Settings.TimeScale = 0.02f;
            yield return WaitFor(() => app.Session.State == UxState.MatchSetup, "setup");
            yield return TapOn(app.SetupContinueButton);
            yield return WaitFor(() => app.Session.State == UxState.UnitSelect, "unit select");
            yield return TapOn(app.UnitCardButtons[0]);
            yield return TapOn(app.UnitCardButtons[1]);
            Assert.IsTrue(app.Session.Selection.IsComplete, "two taps fill both slots");
            yield return null;
            Assert.IsTrue(app.ConfirmUnitsButton.interactable, "confirm enabled");
            yield return TapOn(app.ConfirmUnitsButton);
            yield return WaitFor(() => app.Session.State == UxState.RoundReady, "round ready");
            yield return TapOn(app.SpinButton);
            yield return WaitFor(() => app.Session.State != UxState.RoundReady, "spin by tap");
        }

        /// <summary>
        /// Drags <paramref name="knob"/> by <paramref name="push"/> (a fraction of the knob's width per axis), holds it
        /// still for <paramref name="seconds"/> of game time, calls <paramref name="atStart"/> and <paramref name="atEnd"/>
        /// around that hold, then lets go.
        /// </summary>
        private IEnumerator HoldStick(RectTransform knob, Vector2 push, float seconds, System.Action atStart = null, System.Action atEnd = null)
        {
            var at = ScreenPoint(knob);
            var corners = new Vector3[4];
            knob.GetWorldCorners(corners);
            var to = push * (corners[2].x - corners[0].x);
            int id = ++_touchId;
            BeginTouch(id, at);
            yield return null;
            for (int i = 1; i <= 5; i++) { MoveTouch(id, at + to * (i / 5f)); yield return null; }
            yield return null;
            atStart?.Invoke();
            float t0 = Time.time;
            while (Time.time - t0 < seconds) yield return null;
            atEnd?.Invoke();
            EndTouch(id, at + to);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator World_StickPushedAllTheWayRuns_AndThereIsNoRunButton()
        {
            yield return Load("World");
            var world = Object.FindAnyObjectByType<WorldApp>();
            world.SkipTitleForTests();
            yield return null;
            yield return null;
            Assert.IsNull(world.TouchPad.Root.transform.Find("PadRUN"), "running is a stick threshold now, not a button (D-040)");

            var speed = new float[2];
            var running = new bool[2];
            var pushes = new[] { 0.35f, 1f };
            for (int k = 0; k < 2; k++)
            {
                int kk = k;
                world.Player.Teleport(new Vector3(0, 0, -5f), 0);
                yield return null;
                Vector3 p0 = Vector3.zero;
                float t0 = 0;
                // North, as in the keyboard sprint test: open ground in Hearthmoor.
                yield return HoldStick(world.TouchPad.StickKnob, new Vector2(0, pushes[k]), 0.25f,
                    () => { p0 = world.Player.transform.position; t0 = Time.time; },
                    () =>
                    {
                        running[kk] = world.Input.SprintHeld;
                        speed[kk] = (world.Player.transform.position.z - p0.z) / Mathf.Max(Time.time - t0, 1e-4f);
                    });
            }
            Assert.Greater(speed[0], 0.3f, "a gentle push walks");
            Assert.IsFalse(running[0], "a gentle push does not run");
            Assert.IsTrue(running[1], "pushed to the rim: running");
            Assert.Greater(speed[1], speed[0] * 1.5f, "running is clearly faster (" + speed[0] + " vs " + speed[1] + " m/s)");
        }

        [UnityTest]
        public IEnumerator World_LookStick_ShowsOnlyInFirstPerson_AndTurnsTheView()
        {
            yield return Load("World");
            var world = Object.FindAnyObjectByType<WorldApp>();
            world.SkipTitleForTests();
            yield return null;
            yield return null;
            Assert.IsFalse(world.TouchPad.LookVisible, "overhead: no look stick");
            world.SetView(true);
            yield return null;
            yield return null;
            Assert.IsTrue(world.TouchPad.LookVisible, "first person: a second stick to look with (D-040)");
            float yaw0 = world.WorldCamera.transform.eulerAngles.y;
            var before = world.Player.transform.position;
            yield return HoldStick(world.TouchPad.LookKnob, new Vector2(1f, 0), 0.4f);
            float turned = Mathf.DeltaAngle(yaw0, world.WorldCamera.transform.eulerAngles.y);
            Assert.Greater(turned, 10f, "dragging the look stick right turns the view right");
            Assert.Less(Vector3.Distance(before, world.Player.transform.position), 0.05f, "looking does not walk");
            world.SetView(false);
            yield return null;
            yield return null;
            Assert.IsFalse(world.TouchPad.LookVisible, "back to overhead: the look stick goes away");
        }

        [UnityTest]
        public IEnumerator World_TapPausedFiveTimes_UnlocksDevTools_AndTheGodWheelsSwitch()
        {
            GameFlow.DevToolsUnlocked = false;
            yield return Load("World");
            var world = Object.FindAnyObjectByType<WorldApp>();
            world.SkipTitleForTests();
            yield return null;
            yield return null;
            yield return TapOn(world.TouchPad.Root.transform.Find("PadMENU"));
            yield return WaitFor(() => world.Ui.Pause.activeSelf, "pause menu");
            Assert.IsFalse(world.Ui.PauseDev.gameObject.activeSelf, "hidden until unlocked");
            for (int i = 0; i < WorldApp.DevUnlockTaps - 1; i++) yield return TapOn(world.Ui.PauseTitle);
            Assert.IsFalse(world.Ui.PauseDev.gameObject.activeSelf, "four taps are not enough");
            yield return TapOn(world.Ui.PauseTitle);
            Assert.IsTrue(world.Ui.PauseDev.gameObject.activeSelf, "five taps unlock DEV TOOLS");
            Assert.IsTrue(GameFlow.DevToolsUnlocked);

            yield return TapOn(world.Ui.PauseDev);
            yield return WaitFor(() => world.List.IsOpen, "dev tools");
            Assert.AreEqual(WorldApp.DevPage.Main, world.OpenDevPage);
            StringAssert.Contains("DEV TOOLS", world.List.Title);
            Assert.IsFalse(GameFlow.GodWheels);
            yield return TapOn(world.List.RowButtons[0]);
            Assert.IsTrue(GameFlow.GodWheels, "the first row switches the god wheels on");
            int coins = GameFlow.Coins;
            yield return TapOn(world.List.RowButtons[2]);
            Assert.AreEqual(coins + 1000, GameFlow.Coins, "+1000 coins");
            Assert.IsTrue(SaveGame.TryLoad(out var saved) && saved.godWheels && saved.coins == GameFlow.Coins, "dev changes are saved");

            yield return TapOn(world.List.RowButtons[6]); // Figurines
            Assert.AreEqual(WorldApp.DevPage.Figurines, world.OpenDevPage);
            yield return TapOn(world.List.RowButtons[0]); // the first figurine you don't start with
            Assert.AreEqual(3, GameFlow.UnlockedUnits.Count, "a figurine given");
            yield return TapOn(world.List.Close);
            Assert.AreEqual(WorldApp.DevPage.Main, world.OpenDevPage, "BACK returns to the main page");
            yield return TapOn(world.List.Close);
            Assert.IsFalse(world.List.IsOpen, "CLOSE leaves the dev tools");
            Assert.IsNull(world.OpenDevPage);
            GameFlow.DevToolsUnlocked = false;
        }

        private IEnumerator TapButton(UnityEngine.InputSystem.Controls.ButtonControl button)
        {
            Press(button);
            yield return null;
            Release(button);
            yield return null;
            yield return null;
        }

        private static GameObject Selected => UnityEngine.EventSystems.EventSystem.current != null ? UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject : null;

        [UnityTest]
        public IEnumerator Match_ControllerOnly_ReachesBackWhileConfirmIsGreyed_ThenStarts()
        {
            var pad = InputSystem.AddDevice<Gamepad>();
            yield return Load("MatchPrototype");
            var app = Object.FindAnyObjectByType<MatchApp>();
            app.Settings.TimeScale = 0.02f;
            yield return WaitFor(() => app.Session.State == UxState.MatchSetup, "setup");
            yield return TapButton(pad.buttonSouth);
            yield return WaitFor(() => app.Session.State == UxState.UnitSelect, "unit select");
            yield return null;
            yield return TapButton(pad.buttonSouth); // first figurine -> A
            yield return TapButton(pad.dpad.down);   // PUT IN A
            yield return TapButton(pad.dpad.down);   // slot A
            yield return TapButton(pad.dpad.down);   // SWAP
            yield return TapButton(pad.dpad.right);  // CONFIRM is greyed: straight to BACK
            Assert.IsFalse(app.ConfirmUnitsButton.interactable);
            Assert.AreEqual(app.SelectBackButton.gameObject, Selected, "BACK reachable while CONFIRM is greyed out");

            yield return TapButton(pad.dpad.left);   // SWAP
            yield return TapButton(pad.dpad.up);     // slot A
            yield return TapButton(pad.dpad.up);     // PUT IN A
            yield return TapButton(pad.dpad.up);     // first card
            yield return TapButton(pad.dpad.right);  // second card
            yield return TapButton(pad.buttonSouth); // -> B
            Assert.IsTrue(app.Session.Selection.IsComplete, "both slots filled");
            yield return TapButton(pad.dpad.down);
            yield return TapButton(pad.dpad.down);
            yield return TapButton(pad.dpad.down);
            yield return TapButton(pad.dpad.right);
            Assert.AreEqual(app.ConfirmUnitsButton.gameObject, Selected, "CONFIRM reachable once two are chosen");
            yield return TapButton(pad.buttonSouth);
            yield return WaitFor(() => app.Session.State == UxState.RoundReady, "round ready");
        }
    }
}
