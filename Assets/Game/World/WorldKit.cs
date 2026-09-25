using System.Collections.Generic;
using Tabletop.Presentation;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.UI;

namespace Tabletop.World
{
    /// <summary>
    /// Building blocks for the placeholder world: primitives with real material assets (never the built-in
    /// default, which renders magenta in URP players), procedural cones/prisms, and billboard labels.
    /// </summary>
    public sealed class WorldKit
    {
        private readonly Material _base;
        private readonly Dictionary<Color32, Material> _materials = new Dictionary<Color32, Material>();
        private readonly List<Transform> _billboards = new List<Transform>();

        public WorldKit(Material baseMaterial)
        {
            _base = baseMaterial;
        }

        public IReadOnlyList<Transform> Billboards => _billboards;

        /// <summary>One material per color, derived from the referenced URP base material (keeps SRP batching).</summary>
        public Material Mat(Color c)
        {
            var key = (Color32)c;
            if (_materials.TryGetValue(key, out var m)) return m;
            m = _base != null ? new Material(_base) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.name = "World_" + key.r + "_" + key.g + "_" + key.b;
            m.SetColor("_BaseColor", c);
            m.color = c;
            _materials[key] = m;
            return m;
        }

        public GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Color color, bool solid = false, float rotY = 0)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (!solid && col != null)
            {
                if (UnityEngine.Application.isPlaying) Object.Destroy(col);
                else Object.DestroyImmediate(col);
            }
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0, rotY, 0);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = Mat(color);
            return go;
        }

        public GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Color color, bool solid = false, float rotY = 0) =>
            Prim(PrimitiveType.Cube, name, parent, pos, size, color, solid, rotY);

        public GameObject Cone(string name, Transform parent, Vector3 basePos, Vector3 scale, Color color)
        {
            var go = Prim(PrimitiveType.Cylinder, name, parent, basePos, scale, color);
            go.GetComponent<MeshFilter>().sharedMesh = MeshFactory.Cone;
            return go;
        }

        public GameObject Prism(string name, Transform parent, Vector3 basePos, Vector3 scale, Color color, float rotY = 0)
        {
            var go = Prim(PrimitiveType.Cube, name, parent, basePos, scale, color, false, rotY);
            go.GetComponent<MeshFilter>().sharedMesh = MeshFactory.Prism;
            return go;
        }

        /// <summary>
        /// Places an imported model (D-026) under <paramref name="parent"/>: holds its pose, turns it, scales it to
        /// <paramref name="height"/> (or to fit <paramref name="footprint"/> when that is non-zero), stands it on
        /// local y = 0 and centres it. Returns the container, whose local space is the fitted model's.
        /// </summary>
        public Transform PlaceModel(WorldArtSet.Slot slot, Transform parent, float height, Vector2 footprint, out Vector3 fittedSize)
        {
            var container = new GameObject("Model").transform;
            container.SetParent(parent, false);
            container.localRotation = Quaternion.Euler(0, slot.turn, 0);
            var go = Object.Instantiate(slot.model, container, false); // keeps the FBX root's own axis/scale fix-ups
            go.name = slot.model.name;
            foreach (var c in go.GetComponentsInChildren<Camera>(true)) c.enabled = false;
            foreach (var l in go.GetComponentsInChildren<Light>(true)) l.enabled = false;
            if (slot.pose != null) ModelPose.Apply(go, slot.pose, slot.poseTime, slot.loop);
            else foreach (var a in go.GetComponentsInChildren<Animator>(true)) a.enabled = false; // hold the rest pose
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true;

            var b = LocalBounds(go.transform, container);
            float s = footprint != Vector2.zero
                ? Mathf.Min(footprint.x / Mathf.Max(b.size.x, 0.001f), footprint.y / Mathf.Max(b.size.z, 0.001f))
                : height / Mathf.Max(b.size.y, 0.001f);
            go.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);
            container.localScale = Vector3.one * s;
            fittedSize = b.size * s;
            return container;
        }

        /// <summary>Tight bounds of every mesh under <paramref name="root"/> in <paramref name="space"/>'s local space (skinned meshes as posed).</summary>
        public static Bounds LocalBounds(Transform root, Transform space)
        {
            var b = new Bounds();
            bool any = false;
            var toSpace = space.worldToLocalMatrix;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null) Encapsulate(ref b, ref any, toSpace * mf.transform.localToWorldMatrix, mf.sharedMesh.bounds);
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var baked = new Mesh();
                // BakeMesh(useScale: true) yields the renderer's local space; useScale: false bakes in the lossy scale
                // (measured, not assumed: see WorldArtTests). Local space + the full transform handles every parent's scale.
                smr.BakeMesh(baked, true);
                Encapsulate(ref b, ref any, toSpace * smr.transform.localToWorldMatrix, baked.bounds);
                if (UnityEngine.Application.isPlaying) Object.Destroy(baked); else Object.DestroyImmediate(baked);
            }
            return b;
        }

        private static void Encapsulate(ref Bounds b, ref bool any, Matrix4x4 m, Bounds local)
        {
            for (int i = 0; i < 8; i++)
            {
                var corner = local.center + Vector3.Scale(local.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var p = m.MultiplyPoint3x4(corner);
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                else b.Encapsulate(p);
            }
        }

        /// <summary>Camera-facing world text (name tags, signs).</summary>
        public Text Label(string name, Transform parent, Vector3 localPos, string text, int size, Color color, float scale = 0.01f)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(700, 200);
            rt.localScale = Vector3.one * scale;
            var t = Ui.Label("Text", go.transform, text, size, TextAnchor.LowerCenter, color, FontStyle.Bold);
            t.rectTransform.Fill();
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var outline = t.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.85f);
            outline.effectDistance = new Vector2(3, -3);
            _billboards.Add(go.transform);
            return t;
        }

        /// <summary>Camera-facing icon (e.g. the "can challenge" marker over a person).</summary>
        public Image IconLabel(string name, Transform parent, Vector3 localPos, Sprite sprite, float worldSize)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(100, 100);
            rt.localScale = Vector3.one * (worldSize / 100f);
            var img = Ui.Icon("Icon", go.transform, sprite, 100, Theme.Crown);
            img.rectTransform.Fill();
            _billboards.Add(go.transform);
            return img;
        }

        public void FaceCamera(Camera cam)
        {
            if (cam == null) return;
            var rot = cam.transform.rotation;
            for (int i = _billboards.Count - 1; i >= 0; i--)
            {
                if (_billboards[i] == null) { _billboards.RemoveAt(i); continue; }
                _billboards[i].rotation = rot;
            }
        }
    }

    /// <summary>
    /// Drives an imported rig with one clip through the Playables API: held on a single frame (a standing pose) or
    /// looped (an idle). AnimationClip.SampleAnimation only works in the editor for non-legacy clips, so it would
    /// leave players' builds in the rest pose (found by the build self-check, D-026).
    /// </summary>
    public sealed class ModelPose : MonoBehaviour
    {
        private PlayableGraph _graph;

        public static ModelPose Apply(GameObject model, AnimationClip clip, float time, bool loop)
        {
            var animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.enabled = true;
            animator.applyRootMotion = false;
            var pose = model.AddComponent<ModelPose>();
            pose._graph = PlayableGraph.Create(model.name + " pose");
            pose._graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(pose._graph, "Pose", animator);
            var playable = AnimationClipPlayable.Create(pose._graph, clip);
            playable.SetTime(time);
            playable.SetSpeed(loop ? 1 : 0);
            playable.SetApplyFootIK(false);
            output.SetSourcePlayable(playable);
            pose._graph.Evaluate(); // pose the bones now so the model can be measured and fitted
            pose._graph.Play();
            return pose;
        }

        private void OnDestroy()
        {
            if (_graph.IsValid()) _graph.Destroy();
        }
    }

    /// <summary>Palette for the placeholder world (warm, readable, low-poly).</summary>
    public static class Palette
    {
        public static readonly Color Grass = new Color(0.42f, 0.62f, 0.32f);
        public static readonly Color GrassDark = new Color(0.33f, 0.52f, 0.27f);
        public static readonly Color Dirt = new Color(0.66f, 0.52f, 0.36f);
        public static readonly Color Stone = new Color(0.66f, 0.64f, 0.6f);
        public static readonly Color StoneDark = new Color(0.48f, 0.47f, 0.45f);
        public static readonly Color Wood = new Color(0.52f, 0.34f, 0.2f);
        public static readonly Color WoodDark = new Color(0.34f, 0.22f, 0.13f);
        public static readonly Color Plaster = new Color(0.93f, 0.88f, 0.76f);
        public static readonly Color PlasterWarm = new Color(0.95f, 0.8f, 0.62f);
        public static readonly Color RoofRed = new Color(0.72f, 0.3f, 0.24f);
        public static readonly Color RoofBlue = new Color(0.3f, 0.42f, 0.62f);
        public static readonly Color RoofGreen = new Color(0.32f, 0.5f, 0.34f);
        public static readonly Color RoofBrown = new Color(0.45f, 0.3f, 0.2f);
        public static readonly Color Leaves = new Color(0.25f, 0.5f, 0.25f);
        public static readonly Color LeavesLight = new Color(0.4f, 0.62f, 0.3f);
        public static readonly Color Trunk = new Color(0.4f, 0.27f, 0.16f);
        public static readonly Color Water = new Color(0.3f, 0.55f, 0.78f);
        public static readonly Color Window = new Color(0.25f, 0.3f, 0.4f);
        public static readonly Color WindowLit = new Color(1f, 0.85f, 0.45f);
        public static readonly Color Skin = new Color(0.94f, 0.78f, 0.62f);
        public static readonly Color SkinDark = new Color(0.62f, 0.44f, 0.3f);
        public static readonly Color Gold = new Color(0.95f, 0.78f, 0.25f);
        public static readonly Color Fire = new Color(1f, 0.55f, 0.15f);
        public static readonly Color Banner = new Color(0.62f, 0.18f, 0.22f);
    }
}
