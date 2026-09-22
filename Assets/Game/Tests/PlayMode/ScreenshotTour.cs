using System.Collections;
using System.IO;
using NUnit.Framework;
using Tabletop.Application;
using Tabletop.Domain;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tabletop.Tests.PlayMode
{
    /// <summary>
    /// Drives the real scene through the required UX states and captures review screenshots
    /// to Logs/Screens (copied to Docs/Screenshots for the creative director).
    /// </summary>
    [Category("Screenshots")]
    public class ScreenshotTour : MatchSceneFixture
    {
        private const string Dir = "Logs/Screens";

        private static IEnumerator Capture(string name)
        {
            Directory.CreateDirectory(Dir);
            yield return new WaitForEndOfFrame();
            var path = Path.Combine(Dir, name + ".png");
            if (File.Exists(path)) File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);
            for (int i = 0; i < 5; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator CaptureRequiredStates()
        {
            App.Settings.TimeScale = 0.05f;
            yield return Capture("00_setup");
            // Selection with both units assigned, opponent pair visible.
            yield return Tap(Kb.enterKey);
            yield return WaitForState(UxState.UnitSelect);
            yield return Tap(Kb.enterKey);
            yield return Tap(Kb.rightArrowKey);
            yield return Tap(Kb.enterKey);
            yield return Capture("01_setup_selection");
            Session.ConfirmUnits();
            yield return WaitForState(UxState.RoundReady);

            // Spin decision with mixed locks.
            yield return Tap(Kb.rKey);
            yield return WaitForState(UxState.SpinDecision);
            yield return Tap(Kb.digit1Key);
            yield return Tap(Kb.digit3Key);
            yield return Tap(Kb.rightArrowKey);
            yield return Capture("02_spin_decision_mixed_locks");

            // Resolution in progress (normal speed so an action is mid-flight).
            App.Settings.TimeScale = 1f;
            yield return Tap(Kb.rKey);
            yield return WaitForState(UxState.SpinDecision);
            yield return Tap(Kb.rKey);
            yield return WaitFor(() => Session.State == UxState.Resolving && App.Presenter.Current != null
                && (App.Presenter.Current.Type == MatchEventType.ProjectileResolved || App.Presenter.Current.Type == MatchEventType.EnergyGranted
                    || App.Presenter.Current.Type == MatchEventType.BarrierBuilt || App.Presenter.Current.Type == MatchEventType.RoundEnded), 30f, "resolution");
            if (App.Presenter.Current.Type != MatchEventType.RoundEnded)
                yield return WaitFor(() => App.Presenter.Current == null || App.Presenter.Progress > 0.4f, 5f, "mid event");
            yield return Capture("03_resolution");

            // Play on to a result.
            App.Settings.TimeScale = 0.02f;
            yield return PlayToResult(false);
            yield return Capture("04_match_result");
            Assert.IsTrue(App.ResultOverlay.activeSelf);
        }

        [UnityTest]
        public IEnumerator Capture150PercentAt1280x720()
        {
#if UNITY_EDITOR
            UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1280, 720, "720p");
            yield return null;
            yield return null;
            App.ApplyUiScale(1.5f);
            App.Settings.TimeScale = 0.05f;
            yield return KeyboardStartMatch();
            yield return Tap(Kb.rKey);
            yield return WaitForState(UxState.SpinDecision);
            yield return Tap(Kb.digit2Key);
            yield return Capture("05_ui150_1280x720");
            Assert.AreEqual(1280, Screen.width);
            UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1920, 1080, "1080p");
            yield return null;
#else
            Assert.Ignore("Editor-only resolution control");
            yield break;
#endif
        }
    }
}
