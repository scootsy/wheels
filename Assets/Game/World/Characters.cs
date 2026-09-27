using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Tabletop.World
{
    /// <summary>
    /// How one person looks (D-033): which KayKit body (Knight, Barbarian, Mage, Rogue, RogueHooded), a clothing
    /// recolour, hat and cape on or off, what they hold, and how tall they stand.
    /// </summary>
    public sealed class CharacterLook
    {
        public string Body = "Rogue";
        /// <summary>Degrees to rotate the hue of clothing (skin, leather and metal keep their colour).</summary>
        public float Hue;
        public float Saturation = 1f;
        public float Brightness = 1f;
        public bool Hat = true;
        public bool Cape = true;
        /// <summary>Name of a hand-slot item to keep (e.g. "Mug", "Spellbook_open", "2H_Staff"); all others are hidden.</summary>
        public string Prop;
        public string Prop2;
        public float Height = 1.75f;
        /// <summary>Idle animation ("Idle", "Unarmed_Idle", "2H_Melee_Idle", "Sit_Chair_Idle"...).</summary>
        public string Idle = "Idle";

        public CharacterLook() { }

        public CharacterLook(string body, float hue = 0f, bool hat = true, bool cape = true, string prop = null, float height = 1.75f,
            float saturation = 1f, float brightness = 1f, string idle = "Idle")
        {
            Body = body;
            Hue = hue;
            Hat = hat;
            Cape = cape;
            Prop = prop;
            Height = height;
            Saturation = saturation;
            Brightness = brightness;
            Idle = idle;
        }

        public string TextureKey => Body == "RogueHooded" ? "rogue" : Body.ToLowerInvariant();
    }

    /// <summary>
    /// Animates a KayKit rig through the Playables API (works in players, unlike SampleAnimation): idle ↔ run by
    /// speed, a jump pose while airborne, sitting, and one-shot gestures. Presentation only.
    /// </summary>
    public sealed class CharacterRig : MonoBehaviour
    {
        private PlayableGraph _graph;
        private AnimationMixerPlayable _root;
        private AnimationMixerPlayable _loco;
        private AnimationClipPlayable _action;
        private WorldLook _look;
        private string _idle = "Idle";
        private float _speed;
        private float _actionWeight;
        private float _actionTarget;
        private float _actionEnds = -1f;
        private bool _sitting;
        private bool _airborne;

        public bool Sitting => _sitting;
        public string CurrentAction { get; private set; }

        public static CharacterRig Attach(GameObject model, WorldLook look, string idle)
        {
            var animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.enabled = true;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            var rig = model.AddComponent<CharacterRig>();
            rig._look = look;
            rig._idle = look.Clip(idle) != null ? idle : "Idle";
            rig._graph = PlayableGraph.Create(model.name + " rig");
            rig._graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(rig._graph, "Body", animator);
            rig._root = AnimationMixerPlayable.Create(rig._graph, 2);
            rig._loco = AnimationMixerPlayable.Create(rig._graph, 2);
            rig._loco.ConnectInput(0, rig.ClipPlayable(rig._idle), 0, 1f);
            rig._loco.ConnectInput(1, rig.ClipPlayable("Running_A"), 0, 0f);
            rig._root.ConnectInput(0, rig._loco, 0, 1f);
            rig._action = rig.ClipPlayable(rig._idle);
            rig._root.ConnectInput(1, rig._action, 0, 0f);
            output.SetSourcePlayable(rig._root);
            rig._graph.Evaluate(); // the first frame of the idle, for measuring
            rig._graph.Play();
            return rig;
        }

        /// <summary>Stagger idles so a crowd does not breathe in unison.</summary>
        public void RandomisePhase()
        {
            if (_graph.IsValid()) _loco.GetInput(0).SetTime(Random.Range(0f, 1f));
        }

        private AnimationClipPlayable ClipPlayable(string key)
        {
            var clip = _look.Clip(key) ?? _look.Clip("Idle");
            var p = AnimationClipPlayable.Create(_graph, clip);
            p.SetApplyFootIK(false);
            return p;
        }

        private void SetAction(string key)
        {
            if (CurrentAction == key) return;
            CurrentAction = key;
            _root.DisconnectInput(1);
            _action.Destroy();
            _action = ClipPlayable(key);
            _root.ConnectInput(1, _action, 0, _actionWeight);
            _action.SetTime(0);
        }

        /// <summary>Walking pace: 0 standing, 1 walking speed, ~1.8 sprinting.</summary>
        public void SetMove(float speed, bool airborne)
        {
            _speed = speed;
            if (airborne != _airborne)
            {
                _airborne = airborne;
                if (airborne) { SetAction("Jump_Idle"); _actionTarget = 1f; _actionEnds = -1f; }
                else if (!_sitting) _actionTarget = 0f;
            }
        }

        public void Sit(bool on)
        {
            _sitting = on;
            if (on) { SetAction("Sit_Chair_Idle"); _actionTarget = 1f; _actionEnds = -1f; _actionWeight = 1f; }
            else _actionTarget = 0f;
        }

        /// <summary>Plays a gesture once ("Cheer", "Interact", "PickUp"...), then returns to the idle or run.</summary>
        public void Play(string key)
        {
            if (_sitting || _look.Clip(key) == null) return;
            SetAction(key);
            _actionTarget = 1f;
            _actionEnds = Time.time + _look.Clip(key).length * 0.95f;
        }

        private void Update()
        {
            if (!_graph.IsValid()) return;
            float dt = Time.deltaTime;
            if (_actionEnds > 0f && Time.time >= _actionEnds && !_sitting && !_airborne) { _actionTarget = 0f; _actionEnds = -1f; }
            _actionWeight = Mathf.MoveTowards(_actionWeight, _actionTarget, dt * 6f);
            float run = Mathf.Clamp01(_speed);
            float w = _loco.GetInputWeight(1);
            w = Mathf.MoveTowards(w, run, dt * 8f);
            _loco.SetInputWeight(0, 1f - w);
            _loco.SetInputWeight(1, w);
            _loco.GetInput(1).SetSpeed(0.85f + 0.4f * Mathf.Max(0f, _speed - 1f));
            _root.SetInputWeight(0, 1f - _actionWeight);
            _root.SetInputWeight(1, _actionWeight);
        }

        private void OnDestroy()
        {
            if (_graph.IsValid()) _graph.Destroy();
        }
    }

    /// <summary>Builds a dressed, recoloured, animated KayKit character (D-033).</summary>
    public static class CharacterFactory
    {
        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

        /// <summary>
        /// Adds the character under <paramref name="body"/> (the transform that walks/bobs). Returns the rig, or null
        /// when the characters pack is missing (the caller keeps its placeholder).
        /// </summary>
        public static CharacterRig Build(WorldLook look, Material baseMaterial, CharacterLook cl, Transform body)
        {
            if (look == null) return null;
            var prefab = look.Prefab(cl.Body);
            if (prefab == null || look.Clip("Idle") == null) return null;
            var container = new GameObject("Model").transform;
            container.SetParent(body, false);
            var go = Object.Instantiate(prefab, container, false);
            go.name = cl.Body;

            foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                string n = mr.name;
                bool keep;
                if (n.Contains("Helmet") || n.Contains("Hat")) keep = cl.Hat;
                else if (n.Contains("Cape")) keep = cl.Cape;
                else keep = n == cl.Prop || n == cl.Prop2;
                mr.gameObject.SetActive(keep);
            }

            var mat = Material(look, baseMaterial, cl);
            if (mat != null)
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                    r.sharedMaterials = mats;
                }
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = false;

            var rig = CharacterRig.Attach(go, look, cl.Idle);
            // Fit on the body alone (not hats or held items), in the first frame of the idle: stand on y = 0, centred,
            // cl.Height tall.
            var b = BodyBounds(go.transform, container);
            float s = cl.Height / Mathf.Max(0.001f, b.size.y);
            go.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);
            container.localScale = Vector3.one * s;
            rig.RandomisePhase();
            return rig;
        }

        private static Bounds BodyBounds(Transform root, Transform space)
        {
            var b = new Bounds();
            bool any = false;
            var toSpace = space.worldToLocalMatrix;
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(false))
            {
                var baked = new Mesh();
                smr.BakeMesh(baked, true);
                var m = toSpace * smr.transform.localToWorldMatrix;
                var lb = baked.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var c = lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = m.MultiplyPoint3x4(c);
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                    else b.Encapsulate(p);
                }
                if (UnityEngine.Application.isPlaying) Object.Destroy(baked); else Object.DestroyImmediate(baked);
            }
            return any ? b : WorldKit.LocalBounds(root, space);
        }

        private static Material Material(WorldLook look, Material baseMaterial, CharacterLook cl)
        {
            var src = look.Texture(cl.TextureKey);
            if (src == null) return null;
            string key = cl.TextureKey + "|" + Mathf.RoundToInt(cl.Hue) + "|" + cl.Saturation.ToString("F2") + "|" + cl.Brightness.ToString("F2");
            if (Materials.TryGetValue(key, out var m) && m != null) return m;
            var tex = Recolour(src, cl.Hue / 360f, cl.Saturation, cl.Brightness);
            m = baseMaterial != null ? new Material(baseMaterial) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.name = "Character_" + key;
            m.SetTexture("_BaseMap", tex);
            m.mainTexture = tex;
            m.SetColor("_BaseColor", Color.white);
            m.color = Color.white;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
            Materials[key] = m;
            return m;
        }

        /// <summary>
        /// KayKit textures are palettes of flat swatches. Rotate the hue of strongly coloured swatches (clothes)
        /// and leave skin, leather, metal and greys alone. Built at 256 px to keep memory small on phones.
        /// </summary>
        public static Texture2D Recolour(Texture2D src, float hueShift, float sat, float val)
        {
            const int size = 256;
            var dst = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = src.name + "_variant", filterMode = FilterMode.Bilinear };
            Color[] px;
            try { px = src.GetPixels(); }
            catch (UnityException) { return src; } // not readable: keep the original colours
            int sw = src.width, sh = src.height;
            var outPx = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var c = px[(y * sh / size) * sw + (x * sw / size)];
                    Color.RGBToHSV(c, out float h, out float s, out float v);
                    bool skinOrLeather = h > 0.02f && h < 0.12f && s < 0.72f;
                    if (s > 0.3f && v > 0.12f && !skinOrLeather)
                    {
                        h = Mathf.Repeat(h + hueShift, 1f);
                        s = Mathf.Clamp01(s * sat);
                        v = Mathf.Clamp01(v * val);
                        var n = Color.HSVToRGB(h, s, v);
                        n.a = c.a;
                        c = n;
                    }
                    outPx[y * size + x] = c;
                }
            dst.SetPixels(outPx);
            dst.Apply(false, true);
            return dst;
        }
    }
}
