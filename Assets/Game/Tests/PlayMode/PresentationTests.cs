using System.Collections;
using System.Linq;
using NUnit.Framework;
using Tabletop.Application;
using Tabletop.Domain;
using Tabletop.Presentation;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tabletop.Tests.PlayMode
{
    public class PresentationTests : MatchSceneFixture
    {
        private bool _sawAccelerated;

        /// <summary>Each round: spin once, lock all five (commits immediately).</summary>
        private IEnumerator LockAllEveryRound(bool holdAccelerate)
        {
            for (int round = 0; round < 80 && Session.State != UxState.MatchResult; round++)
            {
                yield return WaitFor(() => Session.State == UxState.RoundReady || Session.State == UxState.MatchResult, 30f, "round ready");
                if (Session.State == UxState.MatchResult) break;
                yield return Tap(Kb.rKey);
                yield return WaitForDecisionOrLater();
                if (Session.State == UxState.SpinDecision)
                {
                    yield return Tap(Kb.digit1Key);
                    yield return Tap(Kb.digit2Key);
                    yield return Tap(Kb.digit3Key);
                    yield return Tap(Kb.digit4Key);
                    yield return Tap(Kb.digit5Key);
                }
                if (holdAccelerate) Press(Kb.spaceKey);
                yield return WaitFor(() =>
                {
                    _sawAccelerated |= App.Presenter.Accelerated;
                    return Session.State == UxState.RoundReady || Session.State == UxState.MatchResult;
                }, 60f, "round resolved");
                if (holdAccelerate) { Release(Kb.spaceKey); yield return null; }
            }
            Assert.AreEqual(UxState.MatchResult, Session.State);
        }

        [UnityTest]
        public IEnumerator AcceleratedAndReducedMotion_ReachTheSameFinalState()
        {
            const long seed = 424242;
            App.Settings.TimeScale = 0.25f;
            yield return KeyboardStartMatch(developer: true, seed: seed);
            yield return LockAllEveryRound(false);
            Assert.IsFalse(_sawAccelerated);
            string normalHash = Session.Match.StateHash();
            string normalLog = Session.Match.EventLogHash();
            string normalReplay = Session.ReplayText;

            yield return LoadMatchScene();
            App.Settings.TimeScale = 0.25f;
            App.Settings.ReducedMotion = true;
            yield return KeyboardStartMatch(developer: true, seed: seed);
            yield return LockAllEveryRound(true);
            Assert.IsTrue(_sawAccelerated, "holding accelerate switched to x4");
            Assert.AreEqual(normalHash, Session.Match.StateHash());
            Assert.AreEqual(normalLog, Session.Match.EventLogHash());
            Assert.AreEqual(normalReplay, Session.ReplayText);
            Assert.IsNull(Session.FatalError, "visual state reconciled at every event");
        }

        [UnityTest]
        public IEnumerator PauseDuringReelSpin_ResumesTheSameSpin()
        {
            App.Settings.TimeScale = 1f;
            yield return KeyboardStartMatch();
            yield return Tap(Kb.rKey);
            yield return WaitFor(() => App.Presenter.Current != null && App.Presenter.Current.Type == MatchEventType.ReelsSpun, 5f, "reel spin");
            yield return Tap(Kb.escapeKey);
            Assert.IsTrue(Session.Paused);
            Assert.IsTrue(App.PauseOverlay.activeSelf);
            var current = App.Presenter.Current;
            float progress = App.Presenter.Progress;
            int presented = App.Presenter.EventsPresented;
            for (int i = 0; i < 30; i++) yield return null;
            Assert.AreSame(current, App.Presenter.Current, "same event while paused");
            Assert.AreEqual(progress, App.Presenter.Progress, 1e-4, "presentation frozen");
            Assert.AreEqual(presented, App.Presenter.EventsPresented);
            Assert.AreEqual(RejectionCode.WrongPhase, TrySpinWhilePaused());
            yield return Tap(Kb.escapeKey);
            Assert.IsFalse(Session.Paused);
            yield return WaitForState(UxState.SpinDecision);
            Assert.AreEqual(1, Session.Match.AcceptedCommands.Count, "no command slipped in during pause");
            Assert.IsNull(App.Presenter.Visual.Diff(Session.Match.Snapshot()));
        }

        private RejectionCode TrySpinWhilePaused()
        {
            Session.RequestSpin();
            return Session.LastRejection.Code;
        }

        [UnityTest]
        public IEnumerator PauseDuringAnAction_ResumesWithoutDoubleDamage()
        {
            yield return KeyboardStartMatch(developer: true, seed: 11, scenario: ScenarioLibrary.Victory);
            App.Settings.TimeScale = 1f;
            yield return Tap(Kb.rKey);
            yield return WaitForState(UxState.SpinDecision);
            for (int r = 0; r < 5; r++) yield return Tap(new[] { Kb.digit1Key, Kb.digit2Key, Kb.digit3Key, Kb.digit4Key, Kb.digit5Key }[r]);
            yield return WaitFor(() => App.Presenter.Current != null && App.Presenter.Current.Type == MatchEventType.CrownDamaged && App.Presenter.Current.TargetSide == 1, 20f, "crown hit");
            yield return Tap(Kb.escapeKey);
            Assert.IsTrue(Session.Paused);
            var hit = App.Presenter.Current;
            int crownDuring = App.Presenter.Visual.Crown[1];
            for (int i = 0; i < 30; i++) yield return null;
            Assert.AreSame(hit, App.Presenter.Current);
            Assert.AreEqual(crownDuring, App.Presenter.Visual.Crown[1]);
            yield return Tap(Kb.escapeKey);
            yield return WaitForState(UxState.MatchResult, 30f);
            Assert.AreEqual(Session.Match.Snapshot().Sides[1].CrownHp, App.Presenter.Visual.Crown[1], "damage applied exactly once");
            Assert.IsNull(Session.FatalError);
        }

        private IEnumerator PlayScenarioToResult(string scenario)
        {
            yield return KeyboardStartMatch(developer: true, seed: 77, scenario: scenario);
            yield return Tap(Kb.rKey);
            yield return WaitForState(UxState.SpinDecision);
            foreach (var key in new[] { Kb.digit1Key, Kb.digit2Key, Kb.digit3Key, Kb.digit4Key, Kb.digit5Key }) yield return Tap(key);
            bool resultBeforeFinalCheck = false;
            while (Session.State != UxState.MatchResult)
            {
                if (Session.State == UxState.Resolving && (App.Presenter.Visual.Crown[0] == 0 || App.Presenter.Visual.Crown[1] == 0)
                    && App.ResultOverlay.activeSelf) resultBeforeFinalCheck = true;
                if (Session.FatalError != null) Assert.Fail(Session.FatalError);
                yield return null;
            }
            Assert.IsFalse(resultBeforeFinalCheck, "no result overlay before MatchEnded");
            yield return null;
            Assert.IsTrue(App.ResultOverlay.activeSelf);
        }

        [UnityTest]
        public IEnumerator Victory_ThenRematchUsesANewSeed()
        {
            yield return PlayScenarioToResult(ScenarioLibrary.Victory);
            StringAssert.StartsWith("VICTORY", App.ResultTitle.text);
            long oldSeed = Session.Match.Seed;
            yield return Tap(Kb.enterKey); // Rematch is focused
            yield return WaitForState(UxState.RoundReady);
            Assert.AreNotEqual(oldSeed, Session.Match.Seed);
            Assert.AreEqual(1, Session.Match.Round);
            Assert.IsFalse(App.ResultOverlay.activeSelf);
            Assert.AreEqual(App.SpinButton.gameObject, Selected);
        }

        [UnityTest]
        public IEnumerator Defeat_ThenCopyReplayVerifies()
        {
            yield return PlayScenarioToResult(ScenarioLibrary.Defeat);
            StringAssert.StartsWith("DEFEAT", App.ResultTitle.text);
            App.CopyReplay();
            var rec = ReplayRecord.Decode(App.LastCopiedReplay);
            Assert.IsNull(rec.Verify(Session.Catalog, out var replayed));
            Assert.AreEqual(Session.Match.StateHash(), replayed.StateHash());
            Assert.AreEqual(Session.Match.EventLogHash(), replayed.EventLogHash());
        }

        [UnityTest]
        public IEnumerator Tie_ThenChangeUnitsAndExit()
        {
            yield return PlayScenarioToResult(ScenarioLibrary.Tie);
            StringAssert.StartsWith("TIE", App.ResultTitle.text);
            Assert.AreEqual(Winner.Tie, Session.Winner);
            App.ResultChangeUnitsButton.onClick.Invoke();
            yield return null;
            Assert.AreEqual(UxState.UnitSelect, Session.State);
            Assert.IsTrue(App.SelectScreen.activeSelf);
            yield return Tap(Kb.escapeKey); // back to setup
            Assert.AreEqual(UxState.MatchSetup, Session.State);
        }

        [UnityTest]
        public IEnumerator ForcedPresenterMismatch_StopsAndPreservesReplay()
        {
            yield return KeyboardStartMatch();
            App.Presenter.InjectDesyncForTest = true;
            LogAssert.ignoreFailingMessages = true;
            yield return Tap(Kb.rKey);
            yield return new WaitUntil(() => Session.FatalError != null);
            yield return null;
            LogAssert.ignoreFailingMessages = false;
            Assert.IsTrue(App.ErrorOverlay.activeSelf);
            StringAssert.Contains("desynchronization", Session.FatalError);
            StringAssert.Contains("Replay: TTR1;", Session.FatalError);
            var s = Session.State;
            for (int i = 0; i < 10; i++) yield return null;
            Assert.AreEqual(s, Session.State, "does not continue from an inconsistent state");
        }

        /// <summary>Regression: a command accepted in the same frame a match starts must not desynchronize the presenter.</summary>
        [UnityTest]
        public IEnumerator CommandInTheSameFrameAsMatchStart_StaysSynchronized()
        {
            Session.ContinueFromSetup();
            Session.Selection.Toggle(ReferenceContent.Striker);
            Session.Selection.Toggle(ReferenceContent.Caster);
            Assert.IsTrue(Session.ConfirmUnits());
            Assert.IsTrue(Session.RequestSpin());
            yield return WaitForState(UxState.SpinDecision);
            Assert.IsNull(Session.FatalError);
            // Same for a rematch followed immediately by a spin.
            Session.Rematch();
            Assert.IsTrue(Session.RequestSpin());
            yield return WaitForState(UxState.SpinDecision);
            Assert.IsNull(Session.FatalError);
            Assert.IsNull(App.Presenter.Visual.Diff(Session.Match.Snapshot()));
        }

        [UnityTest]
        public IEnumerator HelpInspectAndPause_AreOverlaysThatDoNotAdvanceState()
        {
            yield return KeyboardStartMatch();
            var hash = Session.Match.StateHash();
            yield return Tap(Kb.hKey);
            Assert.IsTrue(App.HelpOverlay.activeSelf);
            yield return Tap(Kb.hKey);
            Assert.IsFalse(App.HelpOverlay.activeSelf);
            Assert.AreEqual(App.SpinButton.gameObject, Selected, "closing help returns focus to its opener");
            yield return Tap(Kb.tabKey);
            yield return Tap(Kb.tabKey);
            yield return Tap(Kb.tabKey);
            // Walk the focus ring to a unit plaque and inspect it.
            for (int i = 0; i < 12 && !App.UnitPanels.Any(p => p.Button.gameObject == Selected); i++) yield return Tap(Kb.tabKey);
            var plaque = Selected;
            yield return Tap(Kb.iKey);
            Assert.IsTrue(App.InspectOverlay.activeSelf);
            yield return Tap(Kb.escapeKey);
            Assert.IsFalse(App.InspectOverlay.activeSelf);
            Assert.AreEqual(plaque, Selected);
            Assert.AreEqual(hash, Session.Match.StateHash());
            Assert.AreEqual(UxState.RoundReady, Session.State);
        }
    }
}
