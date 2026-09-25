using System;
using System.Collections;
using System.IO;
using Tabletop.Application;
using Tabletop.Domain;
using UnityEngine;

namespace Tabletop.Presentation
{
    /// <summary>
    /// Build verification: launching the player with <c>-tabletopSelfCheck &lt;folder&gt;</c> starts a match
    /// automatically, saves screenshots of the real player's rendering, logs render diagnostics, and quits.
    /// Never active in normal play.
    /// </summary>
    public sealed partial class MatchApp
    {
        private void Start()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-tabletopSelfCheck") StartCoroutine(SelfCheck(args[i + 1]));
        }

        private IEnumerator SelfCheck(string folder)
        {
            Directory.CreateDirectory(folder);
            Debug.Log("[Tabletop] SelfCheck start: screen " + UnityEngine.Screen.width + "x" + UnityEngine.Screen.height + ", pipeline "
                      + (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null ? UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.name : "none"));
            yield return new WaitForSeconds(1f);
            yield return SelfCheckCapture(folder, "selfcheck_1_setup");

            Session.ContinueFromSetup();
            Session.Selection.Toggle(ReferenceContent.Striker);
            Session.Selection.Toggle(ReferenceContent.Caster);
            Session.ConfirmUnits();
            yield return new WaitForSeconds(0.5f);
            Session.RequestSpin();
            float end = Time.realtimeSinceStartup + 10f;
            while (Session.State != UxState.SpinDecision && Time.realtimeSinceStartup < end) yield return null;
            Session.RequestToggleLock(0);
            yield return new WaitForSeconds(0.5f);
            LogRenderDiagnostics();
            yield return SelfCheckCapture(folder, "selfcheck_2_board");

            Session.RequestSpin();
            end = Time.realtimeSinceStartup + 10f;
            while (Session.State != UxState.SpinDecision && Time.realtimeSinceStartup < end) yield return null;
            Session.RequestSpin();
            end = Time.realtimeSinceStartup + 20f;
            while (Session.State != UxState.Resolving && Time.realtimeSinceStartup < end) yield return null;
            yield return new WaitForSeconds(1.2f);
            yield return SelfCheckCapture(folder, "selfcheck_3_resolving");

            // Keep playing until a piece is caught mid-attack (pieces travel out along their groove to strike).
            end = Time.realtimeSinceStartup + 70f;
            bool captured = false;
            while (!captured && Session.State != UxState.MatchResult && Time.realtimeSinceStartup < end)
            {
                var cur = Presenter.Current;
                if (cur != null && cur.Type == MatchEventType.ProjectileResolved && Presenter.Progress > 0.3f && Presenter.Progress < 0.7f)
                {
                    yield return SelfCheckCapture(folder, "selfcheck_4_attack");
                    captured = true;
                    break;
                }
                if (Session.State == UxState.RoundReady || Session.State == UxState.SpinDecision) Session.RequestSpin();
                yield return null;
            }
            Debug.Log("[Tabletop] SelfCheck attack captured=" + captured);

            Debug.Log("[Tabletop] SelfCheck done: state " + Session.State + ", fatal " + (Session.FatalError ?? "none"));
            UnityEngine.Application.Quit();
        }

        private static IEnumerator SelfCheckCapture(string folder, string name)
        {
            yield return new WaitForEndOfFrame();
            var path = Path.Combine(folder, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            yield return null;
            yield return null;
            Debug.Log("[Tabletop] SelfCheck captured " + path);
        }

        private void LogRenderDiagnostics()
        {
            int total = 0, unsupported = 0, wrongMaterial = 0;
            foreach (var r in _table.GetComponentsInChildren<Renderer>(true))
            {
                total++;
                var m = r.sharedMaterial;
                if (m == null || m.shader == null || !m.shader.isSupported) unsupported++;
                if (m != boardMaterial && (m == null || !m.name.StartsWith("Table_") || m.shader != boardMaterial.shader)) wrongMaterial++;
            }
            Debug.Log("[Tabletop] SelfCheck renderers=" + total + " unsupportedShader=" + unsupported + " notBoardMaterial=" + wrongMaterial
                      + " boardShader=" + (boardMaterial != null ? boardMaterial.shader.name + " supported=" + boardMaterial.shader.isSupported : "none"));
        }
    }
}
