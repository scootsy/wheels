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
    /// <summary>A person's floating nameplate; sizes itself to its text.</summary>
    public sealed class NameTagView
    {
        public Canvas Canvas;
        public Image Card;
        public Image Emblem;
        public Text Name;
        public Text Sub;
        public RectTransform Shadow;
        private string _shown;

        public void Set(string name, string caption, Sprite emblem, Color accent)
        {
            string key = name + "|" + caption + "|" + (emblem != null ? emblem.name : "") + "|" + accent;
            if (key == _shown) return;
            _shown = key;
            Name.text = name;
            Sub.text = caption;
            Sub.color = accent;
            Emblem.sprite = emblem;
            Emblem.gameObject.SetActive(emblem != null);
            Emblem.color = accent;
            const float pad = 24f, h = 120f;
            float icon = emblem != null ? 62f : 0f;
            float textW = Mathf.Max(Name.preferredWidth, Sub.preferredWidth);
            float w = pad * 2f + icon + textW;
            var root = (RectTransform)Canvas.transform;
            float x = (root.sizeDelta.x - w) / 2f, y = (root.sizeDelta.y - h) / 2f;
            Card.rectTransform.Place(x, y, w, h);
            Shadow.Place(x - 18, y - 12, w + 36, h + 36);
            Emblem.rectTransform.Place(pad, (h - 48f) / 2f, 48, 48);
            bool hasSub = !string.IsNullOrEmpty(caption);
            Name.rectTransform.Place(pad + icon, hasSub ? 6 : 0, textW + 4, hasSub ? 70 : h);
            Sub.rectTransform.Place(pad + icon, 78, textW + 4, 34);
        }
    }

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
        /// <summary>The URP Lit material every generated material is derived from.</summary>
        public Material BaseMaterial => _base;

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

        private readonly Dictionary<Material, Material> _repaired = new Dictionary<Material, Material>();

        /// <summary>
        /// Places a pack prefab (D-033) at a local position, turned and uniformly scaled. Pack cameras/lights are
        /// switched off, and any material whose shader cannot render in this pipeline is swapped for URP Lit with
        /// the same texture and colour, so nothing ever shows up magenta.
        /// </summary>
        public GameObject PlacePrefab(GameObject prefab, Transform parent, Vector3 localPos, float yaw, float scale)
        {
            var go = Object.Instantiate(prefab, parent, false);
            go.name = prefab.name;
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0) * prefab.transform.localRotation;
            go.transform.localScale = prefab.transform.localScale * scale;
            foreach (var c in go.GetComponentsInChildren<Camera>(true)) c.enabled = false;
            foreach (var l in go.GetComponentsInChildren<Light>(true)) l.enabled = false;
            foreach (var a in go.GetComponentsInChildren<AudioSource>(true)) a.enabled = false;
            RepairMaterials(go);
            return go;
        }

        private readonly Dictionary<string, Material> _restyled = new Dictionary<string, Material>();

        /// <summary>
        /// Swaps every material under <paramref name="go"/> for URP Lit with the same base texture, tinted: used to
        /// bring a pack's stylised shader (e.g. glowing rune rocks) in line with the rest of the world.
        /// </summary>
        public void Restyle(GameObject go, Color tint)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var src = mats[i];
                    if (src == null) continue;
                    string key = src.name + "|" + ColorUtility.ToHtmlStringRGB(tint);
                    if (!_restyled.TryGetValue(key, out var m))
                    {
                        m = _base != null ? new Material(_base) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                        m.name = src.name + "_Restyled";
                        Texture tex = null;
                        foreach (var prop in new[] { "_BaseTexture", "_BaseMap", "_MainTex" })
                            if (src.HasProperty(prop) && src.GetTexture(prop) != null) { tex = src.GetTexture(prop); break; }
                        m.SetTexture("_BaseMap", tex);
                        m.mainTexture = tex;
                        m.SetColor("_BaseColor", tint);
                        m.color = tint;
                        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
                        _restyled[key] = m;
                    }
                    mats[i] = m;
                }
                r.sharedMaterials = mats;
            }
        }

        public void RepairMaterials(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r.GetType().Name == "ParticleSystemRenderer") continue; // module not enabled in this project
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m != null && m.shader != null && m.shader.isSupported && !m.shader.name.StartsWith("Hidden/InternalErrorShader")) continue;
                    mats[i] = Repaired(m);
                    changed = true;
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        private Material Repaired(Material broken)
        {
            if (broken == null) return Mat(Palette.Stone);
            if (_repaired.TryGetValue(broken, out var m)) return m;
            m = _base != null ? new Material(_base) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.name = broken.name + "_URP";
            Texture tex = null;
            foreach (var prop in new[] { "_BaseMap", "_MainTex", "_BaseColorMap", "_Albedo", "_Texture" })
                if (broken.HasProperty(prop) && broken.GetTexture(prop) != null) { tex = broken.GetTexture(prop); break; }
            var color = broken.HasProperty("_BaseColor") ? broken.GetColor("_BaseColor") : broken.HasProperty("_Color") ? broken.GetColor("_Color") : Color.white;
            m.SetTexture("_BaseMap", tex);
            m.mainTexture = tex;
            m.SetColor("_BaseColor", tex != null ? Color.white : color);
            if (broken.HasProperty("_Cutoff") || broken.name.ToLowerInvariant().Contains("leaf") || broken.name.ToLowerInvariant().Contains("foliage"))
            {
                m.SetFloat("_AlphaClip", 1f);
                m.EnableKeyword("_ALPHATEST_ON");
                m.SetFloat("_Cutoff", 0.4f);
            }
            _repaired[broken] = m;
            return m;
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

        /// <summary>A camera-facing nameplate (D-028): dark rounded card, gilt hairline, name and a small caption.</summary>
        public NameTagView NameTag(string name, Transform parent, Vector3 localPos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(760, 150);
            rt.localScale = Vector3.one * 0.0075f;
            var tag = new NameTagView { Canvas = canvas };
            var shadow = Ui.Panel("Shadow", go.transform, new Color(0, 0, 0, 0.45f));
            if (Ui.Kit != null && Ui.Kit.uiShadow != null) { shadow.sprite = Ui.Kit.uiShadow; shadow.type = Image.Type.Sliced; }
            shadow.raycastTarget = false;
            tag.Shadow = shadow.rectTransform;
            tag.Card = Ui.Card("Card", go.transform, new Color(0.09f, 0.05f, 0.035f, 0.9f), true);
            tag.Emblem = Ui.Icon("Emblem", tag.Card.transform, null, 48, Theme.Gilt);
            tag.Name = Ui.Label("Name", tag.Card.transform, "", 52, TextAnchor.MiddleLeft, Theme.Text, FontStyle.Bold);
            tag.Sub = Ui.Label("Caption", tag.Card.transform, "", 26, TextAnchor.MiddleLeft, Theme.TextDim, FontStyle.Bold);
            foreach (var t in new[] { tag.Name, tag.Sub })
            {
                // Never clip a line: uGUI drops a line entirely when it is taller than its box.
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.verticalOverflow = VerticalWrapMode.Overflow;
            }
            _billboards.Add(go.transform);
            return tag;
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
