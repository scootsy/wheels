using System.Collections;
using System.Linq;
using NUnit.Framework;
using Tabletop.Application;
using Tabletop.Domain;
using Tabletop.Input;
using Tabletop.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;

namespace Tabletop.Tests.PlayMode
{
    public class MatchFlowTests : MatchSceneFixture
    {
        [UnityTest]
        public IEnumerator SceneBootstrap_ReachesSetupWithValidFocus()
        {
            Assert.AreEqual(UxState.MatchSetup, Session.State);
            Assert.IsNull(App.ConfigError, "content must validate");
            Assert.IsNotNull(App.Icons, "icon set assigned in the scene");
            Assert.IsTrue(App.Icons.IsComplete, "every required icon sprite is present");
            yield return null;
            Assert.AreEqual(App.SetupContinueButton.gameObject, Selected);
            Assert.IsTrue(App.SetupScreen.activeInHierarchy);
            Assert.IsFalse(App.BoardScreen.activeInHierarchy);
            var module = EventSystem.current.GetComponent<InputSystemUIInputModule>();
            Assert.IsNotNull(module, "EventSystem uses InputSystemUIInputModule");
            Assert.AreEqual(App.Input.Asset, module.actionsAsset);
        }

        [UnityTest]
        public IEnumerator InputAsset_HasRequiredActionsAndSchemes_AndSchemeSwitchKeepsFocus()
        {
            var asset = App.Input.Asset;
            foreach (var a in new[] { "Navigate", "Submit", "Cancel", "Point", "Click", "ScrollWheel" }) Assert.IsNotNull(asset.FindAction("UI/" + a), a);
            foreach (var a in new[] { "Spin", "LockSlot1", "LockSlot2", "LockSlot3", "LockSlot4", "LockSlot5", "FocusNext", "FocusPrevious", "Inspect", "Help", "AcceleratePresentation", "Pause" })
                Assert.IsNotNull(asset.FindAction("Match/" + a), a);
            Assert.IsNotNull(asset.FindAction("WorldReserved/Move"));
            Assert.IsNotNull(asset.FindAction("WorldReserved/Interact"));
            CollectionAssert.AreEquivalent(new[] { "KeyboardMouse", "Gamepad" }, asset.controlSchemes.Select(s => s.name));

            var focused = Selected;
            yield return Tap(Pad.dpad.up);
            Assert.AreEqual(ControlScheme.Gamepad, App.Input.ActiveScheme);
            Assert.IsNotNull(Selected, "focus survives switching to gamepad");
            StringAssert.Contains("GAMEPAD", App.HintsText.text);
            yield return Tap(Kb.leftArrowKey);
            Assert.AreEqual(ControlScheme.KeyboardMouse, App.Input.ActiveScheme);
            Assert.IsNotNull(Selected, "focus survives switching back to keyboard");
            Assert.AreEqual(focused, Selected, "device switches never move or drop focus");
        }

        [UnityTest]
        public IEnumerator KeyboardOnly_CompletesAFullMatch()
        {
            yield return KeyboardStartMatch();
            Assert.IsTrue(App.BoardScreen.activeInHierarchy);
            yield return PlayToResult(gamepad: false);
            Assert.IsTrue(App.ResultOverlay.activeInHierarchy);
            Assert.AreEqual(App.ResultRematchButton.gameObject, Selected);
            Assert.AreNotEqual(Winner.None, Session.Winner);
            // COPY REPLAY via keyboard: Rematch -> down -> Copy Replay.
            yield return Tap(Kb.downArrowKey);
            Assert.AreEqual(App.ResultCopyButton.gameObject, Selected);
            yield return Tap(Kb.enterKey);
            var rec = ReplayRecord.Decode(App.LastCopiedReplay);
            Assert.IsNull(rec.Verify(Session.Catalog, out var replayed), "copied replay reproduces the match");
            Assert.AreEqual(Session.Match.EventLogHash(), replayed.EventLogHash());
        }

        [UnityTest]
        public IEnumerator GamepadOnly_CompletesAFullMatch()
        {
            yield return GamepadStartMatch();
            yield return PlayToResult(gamepad: true);
            Assert.IsTrue(App.ResultOverlay.activeInHierarchy);
            Assert.AreEqual(ControlScheme.Gamepad, App.Input.ActiveScheme);
        }

        [UnityTest]
        public IEnumerator MouseClicks_LockAReelAndSpin()
        {
            yield return KeyboardStartMatch();
            yield return Click(App.SpinButton.transform as RectTransform);
            yield return WaitForState(UxState.SpinDecision);
            var faceBefore = Session.PlayerSide.Reels[2].FaceIndex;
            yield return Click(App.PlayerReels[2].Button.transform as RectTransform);
            Assert.IsTrue(Session.PlayerSide.Reels[2].Locked, "click locks reel 3");
            Assert.AreEqual(App.PlayerReels[2].Button.gameObject, Selected, "focus follows the clicked reel");
            yield return Click(App.SpinButton.transform as RectTransform);
            yield return WaitForDecisionOrLater();
            Assert.AreEqual(faceBefore, Session.Match.Snapshot().Side(SideId.Player).Reels[2].FaceIndex);
        }

        private IEnumerator Click(RectTransform target)
        {
            Vector2 pos = RectTransformUtility.WorldToScreenPoint(null, target.TransformPoint(target.rect.center));
            Set(MouseDevice.position, pos);
            yield return null;
            Press(MouseDevice.leftButton);
            yield return null;
            Release(MouseDevice.leftButton);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator ReelLockingUnlocking_AndThirdSpinFinalization()
        {
            yield return KeyboardStartMatch();
            yield return Tap(Kb.rKey);
            yield return WaitForState(UxState.SpinDecision);
            var snap1 = Session.Match.Snapshot().Side(SideId.Player);
            yield return Tap(Kb.digit1Key);
            yield return Tap(Kb.digit4Key);
            Assert.IsTrue(Session.PlayerSide.Reels[0].Locked);
            Assert.IsTrue(Session.PlayerSide.Reels[3].Locked);
            Assert.IsTrue(App.Presenter.Visual.Locked[0, 0], "lock cue shown after acceptance");
            yield return Tap(Kb.rKey);
            yield return WaitForState(UxState.SpinDecision);
            var snap2 = Session.Match.Snapshot().Side(SideId.Player);
            Assert.AreEqual(snap1.Reels[0].FaceIndex, snap2.Reels[0].FaceIndex);
            Assert.AreEqual(snap1.Reels[3].FaceIndex, snap2.Reels[3].FaceIndex);
            yield return Tap(Kb.digit1Key); // unlock after spin 2
            Assert.IsFalse(Session.PlayerSide.Reels[0].Locked);
            Assert.AreEqual(2, Session.PlayerSide.SpinsUsed);
            yield return Tap(Kb.rKey);
            yield return WaitFor(() => Session.State != UxState.Spinning && Session.State != UxState.SpinDecision, 20f, "third spin commit");
            var log = Session.Match.EventLog;
            var fin = log.First(e => e.Type == MatchEventType.SpinFinalized && e.Side == 0);
            Assert.AreEqual(3, fin.SpinNumber, "third spin finalized automatically");
            Assert.AreEqual(3, Session.Match.AcceptedCommands.Count(c => c.Encode() == "S0"));
            Assert.IsFalse(Session.Match.AcceptedCommands.Any(c => c.Encode() == "F0"), "no explicit finalize needed on spin 3");
        }

        [UnityTest]
        public IEnumerator LockingAllFive_FinalizesEarly()
        {
            yield return KeyboardStartMatch();
            yield return Tap(Kb.rKey);
            yield return WaitForState(UxState.SpinDecision);
            yield return Tap(Kb.digit1Key);
            yield return Tap(Kb.digit2Key);
            yield return Tap(Kb.digit3Key);
            yield return Tap(Kb.digit4Key);
            Assert.AreEqual(UxState.SpinDecision, Session.State);
            yield return Tap(Kb.digit5Key);
            Assert.AreNotEqual(UxState.SpinDecision, Session.State);
            var accepted = Session.Match.AcceptedCommands.Select(c => c.Encode()).ToList();
            Assert.Contains("F0", accepted, "FinalizeSpin issued after the fifth lock");
            Assert.AreEqual(1, accepted.Count(c => c == "S0"), "only one player spin was used");
        }

        [UnityTest]
        public IEnumerator AiResults_StayHiddenUntilBothSidesCommit()
        {
            yield return KeyboardStartMatch();
            yield return Tap(Kb.rKey);
            yield return WaitForState(UxState.SpinDecision);
            Assert.AreEqual(0, Session.Match.Snapshot().Side(SideId.Opponent).SpinsUsed, "AI has not rolled while the player decides");
            Assert.IsFalse(App.Presenter.OpponentRevealed);
            Assert.IsTrue(App.EnemyReels.All(r => r.Button.transform.Find("Cover").gameObject.activeSelf), "enemy reels covered");
            App.Settings.TimeScale = 0.2f;
            yield return Tap(Kb.rKey);
            yield return WaitForState(UxState.SpinDecision);
            yield return Tap(Kb.rKey);
            Assert.IsFalse(App.Presenter.OpponentRevealed, "still hidden right after the player commits");
            yield return WaitFor(() => Session.Match.EventLog.Any(e => e.Type == MatchEventType.SpinFinalized && e.Side == 1), 10f, "AI commit");
            // Opponent commands are only accepted after the player's result is final.
            var log = Session.Match.EventLog;
            int playerFinal = log.ToList().FindIndex(e => e.Type == MatchEventType.SpinFinalized && e.Side == 0);
            int firstAiSpin = log.ToList().FindIndex(e => e.Type == MatchEventType.ReelsSpun && e.Side == 1);
            Assert.GreaterOrEqual(playerFinal, 0);
            Assert.Greater(firstAiSpin, playerFinal);
            yield return WaitFor(() => App.Presenter.OpponentRevealed, 10f, "reveal");
        }

        [UnityTest]
        public IEnumerator RecoverableRejection_DoesNotMutateStateOrLoseFocus()
        {
            yield return KeyboardStartMatch();
            var hash = Session.Match.StateHash();
            Assert.AreEqual(App.SpinButton.gameObject, Selected);
            yield return Tap(Kb.digit2Key);
            Assert.AreEqual(RejectionCode.FirstSpinRequired, Session.LastRejection.Code);
            Assert.AreEqual(hash, Session.Match.StateHash());
            Assert.AreEqual(App.SpinButton.gameObject, Selected, "focus kept on attempted control");
            Assert.AreEqual(UxState.RoundReady, Session.State);
            StringAssert.Contains("Spin all reels before locking", App.RoundInfoText.text);
        }

        [UnityTest]
        public IEnumerator SceneReload_DoesNotDuplicateSubscriptions()
        {
            yield return LoadMatchScene();
            yield return LoadMatchScene();
            yield return KeyboardStartMatch();
            yield return Tap(Kb.rKey);
            yield return WaitForDecisionOrLater();
            Assert.AreEqual(1, Session.Match.Snapshot().Side(SideId.Player).SpinsUsed, "one key press = one spin");
            Assert.AreEqual(1, Session.Match.AcceptedCommands.Count);
        }
    }
}
