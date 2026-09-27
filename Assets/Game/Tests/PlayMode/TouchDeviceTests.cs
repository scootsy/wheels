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
