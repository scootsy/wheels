using System.IO;
using Tabletop.Domain;
using Tabletop.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tabletop.EditorTools
{
    /// <summary>
    /// Renders the match table in an isolated preview scene with a representative mid-match state, through the
    /// real game camera (Tabletop/Art/Render Table Preview -> Logs/TablePreview.png). For iterating on the table's
    /// look without building; the Windows self-check remains the proof that the build renders it.
    /// </summary>
    public static class TablePreview
    {
        public const string OutputPath = "Logs/TablePreview.png";

        [MenuItem("Tabletop/Art/Render Table Preview")]
        public static void Render() => Render(OutputPath, 1920, 1080);

        public static void Render(string path, int width, int height)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            try
            {
                var root = new GameObject("PreviewRoot");
                SceneManager.MoveGameObjectToScene(root, scene);
                var camGo = new GameObject("Camera");
                camGo.transform.SetParent(root.transform, false);
                var cam = camGo.AddComponent<Camera>();
                cam.scene = scene;
                cam.targetTexture = rt; // first, so the camera frames the table for this image's shape
                MechanicalTable.ConfigureCamera(cam);
                var lightGo = new GameObject("Sun");
                lightGo.transform.SetParent(root.transform, false);
                lightGo.transform.rotation = Quaternion.Euler(55, -30, 0);
                var sun = lightGo.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.1f;
                sun.color = new Color(1f, 0.93f, 0.85f);
                sun.shadows = LightShadows.Soft;

                var tableGo = new GameObject("Board");
                tableGo.transform.SetParent(root.transform, false);
                var table = tableGo.AddComponent<MechanicalTable>();
                table.Camera = cam;
                table.Material = AssetDatabase.LoadAssetAtPath<Material>(BoardMaterialSetup.MaterialPath);
                table.Icons = AssetDatabase.LoadAssetAtPath<IconSet>(IconImport.IconSetPath);
                table.Build();

                var match = Match.Start(MatchConfig.Standard(7, ReferenceContent.Striker, ReferenceContent.Caster, ReferenceContent.Ranger, ReferenceContent.Striker),
                    ReferenceContent.Catalog);
                for (int s = 0; s < 2; s++)
                {
                    table.SetReels(s, match.ReelDefinitions((SideId)s));
                    for (int u = 0; u < 2; u++) table.SetUnitShape(s, u, match.UnitDefinition((SideId)s, u));
                }
                // A realistic mid-decision state: one real spin, two reels locked, some energy and wall stored.
                match.Execute(MatchCommand.Spin(SideId.Player));
                match.Execute(MatchCommand.SetReelLock(SideId.Player, 1, true));
                match.Execute(MatchCommand.SetReelLock(SideId.Player, 3, true));
                var v = new VisualState();
                v.CopyFrom(match.Snapshot());
                v.Crown[0] = 7; v.Crown[1] = 4;
                v.Barrier[0] = 5; v.Barrier[1] = 1; // a full wall: the worst case for hiding the plaza
                v.Energy[0, 0] = 2; v.Energy[0, 1] = 4; v.Energy[1, 0] = 1; v.Energy[1, 1] = 3;
                v.Xp[0, 0] = 3; v.Xp[1, 1] = 5;
                v.Rank[0, 1] = Rank.Silver; v.Rank[1, 1] = Rank.Gold;
                for (int r = 0; r < 5; r++) v.Face[1, r] = (r * 5 + 1) % 8;
                var side0 = match.Snapshot().Side(SideId.Player);
                var frame = new TableFrame
                {
                    Visual = v, Match = match, Progress = 1f, Applied = true, ReducedMotion = true, OpponentRevealed = true,
                    Phase = BoardPhase.Idle, State = Tabletop.Application.UxState.SpinDecision, SpinsUsed = 1, Round = 2, CanSpin = true,
                    Preview = Tabletop.Application.OutcomePreview.Compute(side0, match.ReelDefinitions(SideId.Player), ReferenceContent.Catalog, lockedOnly: true),
                    FocusSide = 0, FocusSlot = 1,
                };
                for (int i = 0; i < 6; i++) table.Render(frame);

                cam.Render();
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                cam.targetTexture = null;
                Debug.Log("[Tabletop] Table preview written to " + Path.GetFullPath(path));
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                rt.Release();
                Object.DestroyImmediate(rt);
            }
        }
    }
}
