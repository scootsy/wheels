using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Tabletop.Application;
using Tabletop.Domain;
using Tabletop.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Tabletop.Tests.PlayMode
{
    /// <summary>Loads MatchPrototype with virtual Input System devices and provides driving helpers.</summary>
    public abstract class MatchSceneFixture : InputTestFixture
    {
        protected Keyboard Kb;
        protected Gamepad Pad;
        protected Mouse MouseDevice;
        protected MatchApp App;

        protected MatchSession Session => App.Session;
        protected GameObject Selected => EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

        /// <summary>
        /// Deferred: Unity runs [UnitySetUp] before [SetUp], but the virtual input runtime must exist
        /// before the scene creates its actions. The real setup happens at the start of LoadScene.
        /// </summary>
        public override void Setup() { }

        private void SetupInput()
        {
            base.Setup();
            // Virtual devices must reach the game even when the editor Game view is not focused.
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            Kb = InputSystem.AddDevice<Keyboard>();
            Pad = InputSystem.AddDevice<Gamepad>();
            MouseDevice = InputSystem.AddDevice<Mouse>();
        }

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            SetupInput();
            yield return LoadMatchScene();
        }

        protected IEnumerator LoadMatchScene()
        {
            var op = SceneManager.LoadSceneAsync("MatchPrototype", LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            App = UnityEngine.Object.FindAnyObjectByType<MatchApp>();
            Assert.IsNotNull(App, "MatchApp missing from scene");
            // Fast, deterministic presentation for tests (timing never changes results).
            App.Settings.TimeScale = 0.02f;
            yield return null;
        }

        /// <summary>Destroy the scene's objects while the virtual input runtime still exists, then restore input.</summary>
        public override void TearDown()
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects()) UnityEngine.Object.DestroyImmediate(root);
            App = null;
            base.TearDown();
        }

        // ------------------------------------------------------------ input helpers

        protected IEnumerator Tap(ButtonControl button, int framesHeld = 1)
        {
            Press(button);
            for (int i = 0; i < framesHeld; i++) yield return null;
            Release(button);
            yield return null;
            yield return null;
        }

        protected IEnumerator WaitFor(Func<bool> condition, float timeoutSeconds = 20f, string what = "condition")
        {
            float end = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out waiting for " + what + " (state " + Session.State + ", status '" + Session.LastStatus + "')");
                if (Session.FatalError != null) Assert.Fail("Fatal error: " + Session.FatalError);
                yield return null;
            }
        }

        protected IEnumerator WaitForState(UxState state, float timeout = 20f) => WaitFor(() => Session.State == state, timeout, state.ToString());

        protected IEnumerator WaitForDecisionOrLater()
        {
            yield return WaitFor(() => Session.State != UxState.Spinning && Session.State != UxState.RoundReady, 20f, "leave spinning");
        }

        /// <summary>Keyboard: Continue on setup, pick Striker (A) and Caster (B), confirm.</summary>
        protected IEnumerator KeyboardStartMatch(bool developer = false, long? seed = null, string scenario = null)
        {
            yield return WaitForState(UxState.MatchSetup);
            if (developer)
            {
                App.ConfigureDeveloperForTests(seed, scenario);
                yield return null;
            }
            Assert.AreEqual(App.SetupContinueButton.gameObject, Selected, "Continue focused on setup");
            yield return Tap(Kb.enterKey);
            yield return WaitForState(UxState.UnitSelect);
            Assert.AreEqual(App.UnitCardButtons[0].gameObject, Selected, "first unit card focused");
            yield return Tap(Kb.enterKey); // Striker -> A
            yield return Tap(Kb.rightArrowKey);
            yield return Tap(Kb.enterKey); // Caster -> B
            Assert.AreEqual(ReferenceContent.Striker, Session.Selection.Slots[0]);
            Assert.AreEqual(ReferenceContent.Caster, Session.Selection.Slots[1]);
            // Caster card -> PUT IN A -> Slot A -> Swap -> Confirm
            yield return Tap(Kb.downArrowKey);
            yield return Tap(Kb.downArrowKey);
            yield return Tap(Kb.downArrowKey);
            yield return Tap(Kb.rightArrowKey);
            Assert.AreEqual(App.ConfirmUnitsButton.gameObject, Selected, "Confirm reachable by keyboard");
            yield return Tap(Kb.enterKey);
            yield return WaitForState(UxState.RoundReady);
        }

        /// <summary>Gamepad: same selection flow with south button and d-pad.</summary>
        protected IEnumerator GamepadStartMatch()
        {
            yield return WaitForState(UxState.MatchSetup);
            yield return Tap(Pad.buttonSouth);
            yield return WaitForState(UxState.UnitSelect);
            yield return Tap(Pad.buttonSouth);
            yield return Tap(Pad.dpad.right);
            yield return Tap(Pad.buttonSouth);
            yield return Tap(Pad.dpad.down);
            yield return Tap(Pad.dpad.down);
            yield return Tap(Pad.dpad.down);
            yield return Tap(Pad.dpad.right);
            Assert.AreEqual(App.ConfirmUnitsButton.gameObject, Selected, "Confirm reachable by gamepad");
            yield return Tap(Pad.buttonSouth);
            yield return WaitForState(UxState.RoundReady);
        }

        /// <summary>Plays rounds until the match ends: spin, keep XP/matching reels via Submit, spin again.</summary>
        protected IEnumerator PlayToResult(bool gamepad, int maxRounds = 60)
        {
            for (int round = 0; round < maxRounds && Session.State != UxState.MatchResult; round++)
            {
                yield return WaitFor(() => Session.State == UxState.RoundReady || Session.State == UxState.MatchResult, 30f, "round ready");
                if (Session.State == UxState.MatchResult) break;
                Assert.AreEqual(App.SpinButton.gameObject, Selected, "SPIN focused at round start");
                // Spin 1 via Submit on the focused SPIN button.
                yield return Tap(gamepad ? Pad.buttonSouth : Kb.enterKey);
                yield return WaitForDecisionOrLater();
                if (Session.State == UxState.SpinDecision)
                {
                    // Toggle reel 1 via Submit on the focused reel, then spin twice with the Spin action.
                    Assert.AreEqual(App.PlayerReels[0].Button.gameObject, Selected, "first unlocked reel focused");
                    yield return Tap(gamepad ? Pad.buttonSouth : Kb.enterKey);
                    yield return Tap(gamepad ? Pad.buttonNorth : Kb.rKey);
                    yield return WaitForDecisionOrLater();
                    if (Session.State == UxState.SpinDecision)
                    {
                        yield return Tap(gamepad ? Pad.buttonNorth : Kb.rKey);
                    }
                }
                yield return WaitFor(() => Session.State == UxState.RoundReady || Session.State == UxState.MatchResult, 30f, "round resolved");
            }
            Assert.AreEqual(UxState.MatchResult, Session.State, "match should end");
        }
    }
}
