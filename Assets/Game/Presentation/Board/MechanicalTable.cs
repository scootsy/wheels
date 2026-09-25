using System.Collections.Generic;
using Tabletop.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace Tabletop.Presentation
{
    /// <summary>
    /// The production table (D-027): a carved bronze-and-stone board in the source game's style, rendered in 3D.
    /// Reels are real eight-sided drums that spin and clamp shut when locked. Crowns have flip-digit counters,
    /// walls crank up out of the table brick by brick, energy lights segments around each podium, and pieces
    /// travel out along their groove to attack and slide home again.
    /// Purely visual: it reads the presenter's visual state and the current event, and never changes rules state.
    /// </summary>
    public sealed class MechanicalTable : MonoBehaviour
    {
        // ------------------------------------------------------------------ palette (warm carved stone, bronze, gold)
        public static readonly Color StoneDeep = new Color(0.10f, 0.035f, 0.025f);
        public static readonly Color Stone = new Color(0.24f, 0.085f, 0.055f);
        public static readonly Color StoneLight = new Color(0.34f, 0.13f, 0.075f);
        public static readonly Color StoneEdge = new Color(0.46f, 0.19f, 0.10f);
        public static readonly Color Bronze = new Color(0.66f, 0.38f, 0.19f);
        public static readonly Color BronzeDark = new Color(0.36f, 0.18f, 0.09f);
        public static readonly Color Gold = new Color(0.98f, 0.74f, 0.30f);
        public static readonly Color Ruby = new Color(0.90f, 0.13f, 0.16f);
        public static readonly Color BrickColor = new Color(0.55f, 0.55f, 0.58f);
        public static readonly Color MeterOff = new Color(0.20f, 0.09f, 0.06f);
        public static readonly Color FaceBack = new Color(0.16f, 0.09f, 0.07f, 1f);
        public static readonly Color FaceXp = new Color(0.10f, 0.10f, 0.32f, 1f);

        // ------------------------------------------------------------------ layout (player = side 0 at -z)
        public const float HalfWidth = 8.7f;
        public const float HalfDepth = 5.7f;
        public const float SegmentHeight = 0.30f;
        public const float DrumApothem = 0.95f;
        public const float DrumLength = 1.18f;
        public const float DrumSpacing = 1.36f;
        public const float DrumAxisY = -0.45f;
        public const float WallRadius = 1.5f;
        public const float WallSpanDeg = 58f;
        public const int BricksPerLayer = 7;
        private const float ClampDown = -0.12f;
        private const float ClampUp = 0.2f;

        public static Vector3 UnitPos(int side, int slot) => new Vector3(slot == 0 ? -5.0f : 5.0f, 0f, side == 0 ? -2.45f : 2.45f);
        public static Vector3 CrownPos(int side) => new Vector3(0, 0, side == 0 ? -2.75f : 2.75f);
        public static float ReelZ(int side) => side == 0 ? -4.55f : 4.55f;
        public static float ReelX(int reel) => (reel - 2) * DrumSpacing;
        /// <summary>Z of the wall's front (the face a projectile hits).</summary>
        public static float WallZ(int side) => CrownPos(side).z + (side == 0 ? WallRadius : -WallRadius);
        /// <summary>World Y of an attack height (height 1 = first wall layer, 6 = above a full wall).</summary>
        public static float HeightY(int height) => 0.05f + (height - 0.5f) * SegmentHeight;
        /// <summary>Where a piece stands when it attacks: out along its groove toward the middle of the table.</summary>
        public static Vector3 AttackPos(int side, int slot) => Vector3.Lerp(UnitPos(side, slot), new Vector3(slot == 0 ? -1.9f : 1.9f, 0, side == 0 ? -0.55f : 0.55f), 0.72f);

        /// <summary>Frames the whole table on a full-screen camera (also used by the editor preview).</summary>
        public static void ConfigureCamera(Camera cam)
        {
            cam.rect = new Rect(0, 0, 1, 1);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.09f, 0.035f, 0.025f);
            cam.fieldOfView = 30f;
            cam.transform.position = new Vector3(0, 20.6f, -9.6f);
            cam.transform.LookAt(new Vector3(0, 0, -0.25f));
        }

        // ------------------------------------------------------------------ state
        private sealed class FloatingText
        {
            public Transform Root;
            public Text Label;
            public Vector3 Start;
            public float Age;
        }

        private sealed class Debris
        {
            public Transform T;
            public Vector3 Velocity;
            public Vector3 Spin;
            public float Age;
        }

        private readonly Transform[,] _pieces = new Transform[2, 2];
        private readonly Vector3[,] _pieceOffset = new Vector3[2, 2];
        private readonly List<Renderer>[,] _rankParts = new List<Renderer>[2, 2];
        private readonly List<Transform>[,] _meter = new List<Transform>[2, 2];
        private readonly int[,] _meterCount = new int[2, 2];
        private readonly Renderer[,] _pillarGems = new Renderer[2, 2];
        private readonly Renderer[,] _podiumRims = new Renderer[2, 2];
        private readonly Text[,] _plaques = new Text[2, 2];
        private readonly string[,] _shapeFor = new string[2, 2];

        private readonly Transform[] _crowns = new Transform[2];
        private readonly List<Renderer>[] _crownParts = { new List<Renderer>(), new List<Renderer>() };
        private readonly Text[,] _digits = new Text[2, 2];
        private readonly Transform[,] _digitTiles = new Transform[2, 2];
        private readonly int[] _shownHp = { -99, -99 };
        private readonly float[] _flip = { 1f, 1f };
        private readonly int[] _pendingHp = new int[2];

        private readonly List<Transform>[,] _bricks = new List<Transform>[2, 5];
        private readonly float[,] _layerRise = new float[2, 5];
        private readonly Text[] _wallLabels = new Text[2];

        private readonly Transform[,] _drums = new Transform[2, 5];
        private readonly float[,] _drumAngle = new float[2, 5];
        private readonly Transform[,] _clamps = new Transform[2, 5];
        private readonly Renderer[,] _clampBars = new Renderer[2, 5];
        private readonly Transform[,] _shutters = new Transform[2, 5];
        private readonly GameObject[,,] _faceRoots = new GameObject[2, 5, 8];
        private readonly string[,] _reelKey = new string[2, 5];

        private readonly List<FloatingText> _floating = new List<FloatingText>();
        private readonly List<Debris> _debris = new List<Debris>();
        private readonly List<Transform> _billboards = new List<Transform>();
        private Transform _projectile;
        private Renderer _projectileRenderer;
        private Transform _root;
        private Camera _camera;
        private Material _material;
        private Material _bronze;
        private Material _gold;
        private Material _gloss;
        private Material _satin;
        private IconSet _icons;
        private Vector3 _shake;

        public Camera Camera { get => _camera; set => _camera = value; }
        public IconSet Icons { get => _icons; set => _icons = value; }

        /// <summary>
        /// URP material asset every part derives from. Required in players: runtime CreatePrimitive falls back to the
        /// built-in Standard material there, which URP cannot draw (magenta, D-024). Bronze and gold are copies of it
        /// with different metal settings (no shader keywords, so builds always contain the variant).
        /// </summary>
        public Material Material { get => _material; set => _material = value; }

        private static Color SideColor(int side) => side == 0 ? Theme.Player : Theme.Enemy;

        /// <summary>Miniature finish per rank: dark bronze, bright silver, gold.</summary>
        public static Color FigureFinish(Rank rank) =>
            rank == Rank.Gold ? new Color(0.98f, 0.76f, 0.28f) : rank == Rank.Silver ? new Color(0.80f, 0.83f, 0.90f) : new Color(0.55f, 0.31f, 0.15f);

        // ------------------------------------------------------------------ build

        public void Build()
        {
            _root = new GameObject("Table").transform;
            _root.SetParent(transform, false);
            _bronze = Variant("Table_Bronze", 0.75f, 0.5f);
            _gold = Variant("Table_Gold", 0.85f, 0.65f);
            _gloss = Variant("Table_Gloss", 0.1f, 0.85f);
            _satin = Variant("Table_Satin", 0.35f, 0.5f);

            BuildBody();
            BuildCarvings();
            for (int side = 0; side < 2; side++)
            {
                BuildCrown(side);
                BuildWall(side);
                BuildReelTray(side);
                for (int slot = 0; slot < 2; slot++) BuildPodium(side, slot);
            }
            BuildLights();
            _projectile = Prim(PrimitiveType.Sphere, "Projectile", _root, new Vector3(0, -5, 0), Vector3.one * 0.4f, Color.white, _gloss).transform;
            _projectileRenderer = _projectile.GetComponent<Renderer>();
            _projectile.gameObject.SetActive(false);
        }

        private Material Variant(string name, float metallic, float smoothness)
        {
            if (_material == null) return null;
            var m = new Material(_material) { name = name };
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smoothness);
            return m;
        }

        /// <summary>A rounded slab: two crossed boxes plus corner cylinders, top at <paramref name="top"/>.</summary>
        private void RoundedSlab(string name, float halfW, float halfD, float radius, float top, float thickness, Color color, Material mat = null)
        {
            float y = top - thickness / 2f;
            Prim(PrimitiveType.Cube, name + "_W", _root, new Vector3(0, y, 0), new Vector3(2 * halfW - 2 * radius, thickness, 2 * halfD), color, mat);
            Prim(PrimitiveType.Cube, name + "_D", _root, new Vector3(0, y, 0), new Vector3(2 * halfW, thickness, 2 * halfD - 2 * radius), color, mat);
            foreach (var sx in new[] { -1, 1 })
                foreach (var sz in new[] { -1, 1 })
                    Prim(PrimitiveType.Cylinder, name + "_C", _root, new Vector3(sx * (halfW - radius), y, sz * (halfD - radius)),
                        new Vector3(2 * radius, thickness / 2f, 2 * radius), color, mat);
        }

        private void BuildBody()
        {
            RoundedSlab("Base", HalfWidth + 0.35f, HalfDepth + 0.35f, 1.2f, -0.12f, 0.9f, StoneDeep);
            RoundedSlab("Top", HalfWidth, HalfDepth, 1.0f, 0f, 0.14f, Stone);
            // Bronze band just inside the edge, like a metal-shod board.
            RoundedSlab("Band", HalfWidth - 0.28f, HalfDepth - 0.28f, 0.8f, 0.035f, 0.05f, BronzeDark, _bronze);
            RoundedSlab("Field", HalfWidth - 0.42f, HalfDepth - 0.42f, 0.7f, 0.045f, 0.05f, Stone);
        }

        private void BuildCarvings()
        {
            // Centre plaza: a sunken oval where the pieces meet.
            var plaza = Prim(PrimitiveType.Cylinder, "Plaza", _root, new Vector3(0, 0.03f, 0), new Vector3(5.4f, 0.03f, 2.3f), StoneDeep);
            MeshPart("PlazaRim", _root, MeshFactory.Arc(0.46f, 0.5f, 0, 360, 0.09f, 48), new Vector3(0, 0.03f, 0), new Vector3(5.4f, 1, 2.3f), StoneEdge);
            // Long carved channels framing the plaza (the "X" the reference board is built around).
            for (int sx = -1; sx <= 1; sx += 2)
                for (int side = 0; side < 2; side++)
                {
                    var from = UnitPos(side, sx < 0 ? 0 : 1);
                    var to = new Vector3(sx * 2.2f, 0, side == 0 ? -0.6f : 0.6f);
                    Groove(side, sx < 0 ? 0 : 1, from, to);
                }
            // Great arches around each crown and the carved ribs across the middle.
            for (int side = 0; side < 2; side++)
            {
                var c = CrownPos(side);
                float face = side == 0 ? 0 : 180;
                MeshPart("Arch" + side, _root, MeshFactory.Arc(2.15f, 2.55f, face - 88, face + 88, 0.16f, 40), new Vector3(c.x, 0.05f, c.z), Vector3.one, StoneLight);
                MeshPart("ArchTrim" + side, _root, MeshFactory.Arc(2.55f, 2.63f, face - 88, face + 88, 0.19f, 40), new Vector3(c.x, 0.05f, c.z), Vector3.one, BronzeDark, _bronze);
                MeshPart("OuterArch" + side, _root, MeshFactory.Arc(2.95f, 3.2f, face - 70, face + 70, 0.1f, 40), new Vector3(c.x, 0.05f, c.z), Vector3.one, StoneLight);
                // The wall stands in a slot: a dark curved trench it rises out of.
                MeshPart("WallSlot" + side, _root, MeshFactory.Arc(WallRadius - 0.2f, WallRadius + 0.2f, face - WallSpanDeg - 4, face + WallSpanDeg + 4, 0.012f, 32),
                    new Vector3(c.x, 0.05f, c.z), Vector3.one, StoneDeep);
                // Sockets at the ends of the arches.
                foreach (var a in new[] { face - 88f, face + 88f })
                {
                    float r = a * Mathf.Deg2Rad;
                    Socket(new Vector3(c.x + Mathf.Sin(r) * 2.35f, 0.05f, c.z + Mathf.Cos(r) * 2.35f), 0.42f);
                }
            }
            // Side panels behind the podiums.
            for (int sx = -1; sx <= 1; sx += 2)
            {
                Prim(PrimitiveType.Cube, "SidePanel", _root, new Vector3(sx * 7.55f, 0.06f, 0), new Vector3(1.4f, 0.05f, 6.2f), StoneLight);
                Prim(PrimitiveType.Cube, "SidePanelInset", _root, new Vector3(sx * 7.55f, 0.08f, 0), new Vector3(1.05f, 0.05f, 5.8f), StoneDeep);
                for (int i = -1; i <= 1; i++) Socket(new Vector3(sx * 7.55f, 0.08f, i * 1.7f), 0.3f);
            }
        }

        private void Groove(int side, int slot, Vector3 from, Vector3 to)
        {
            var d = to - from;
            float len = d.magnitude;
            var mid = (from + to) / 2f;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            Prim(PrimitiveType.Cube, "Groove" + side + slot, _root, new Vector3(mid.x, 0.05f, mid.z), new Vector3(0.55f, 0.05f, len), StoneDeep, null, yaw);
            foreach (var off in new[] { -0.33f, 0.33f })
            {
                var o = Quaternion.Euler(0, yaw, 0) * new Vector3(off, 0, 0);
                Prim(PrimitiveType.Cube, "Rail", _root, new Vector3(mid.x + o.x, 0.09f, mid.z + o.z), new Vector3(0.09f, 0.06f, len), BronzeDark, _bronze, yaw);
            }
            Socket(new Vector3(mid.x, 0.07f, mid.z), 0.34f);
        }

        private void Socket(Vector3 at, float radius)
        {
            MeshPart("Socket", _root, MeshFactory.Arc(radius * 0.62f, radius, 0, 360, 0.1f, 24), at, Vector3.one, Bronze, _bronze);
            Prim(PrimitiveType.Cylinder, "SocketHole", _root, at + new Vector3(0, 0.02f, 0), new Vector3(radius * 1.25f, 0.02f, radius * 1.25f), StoneDeep);
        }

        private void BuildLights()
        {
            for (int side = 0; side < 2; side++)
            {
                var go = new GameObject("CrownGlow" + side);
                go.transform.SetParent(_root, false);
                go.transform.localPosition = CrownPos(side) + new Vector3(0, 1.4f, 0);
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1f, 0.62f, 0.32f);
                l.range = 5f;
                l.intensity = 1.6f;
                l.shadows = LightShadows.None;
            }
        }

        private void BuildCrown(int side)
        {
            var crown = new GameObject("Crown" + side).transform;
            crown.SetParent(_root, false);
            crown.localPosition = CrownPos(side);
            crown.localScale = Vector3.one * 1.35f;
            _crowns[side] = crown;
            // Plinth with a gold rim.
            Prim(PrimitiveType.Cylinder, "Plinth", crown, new Vector3(0, 0.1f, 0), new Vector3(1.9f, 0.1f, 1.9f), StoneDeep);
            MeshPart("PlinthRim", crown, MeshFactory.Arc(0.86f, 0.98f, 0, 360, 0.24f, 40), Vector3.zero, Vector3.one, Gold, _gold);
            // The crown itself stands at the back of the plinth (toward its own side).
            float back = side == 0 ? -0.35f : 0.35f;
            var ring = MeshPart("Band", crown, MeshFactory.Arc(0.34f, 0.44f, 0, 360, 0.22f, 32), new Vector3(0, 0.22f, back), Vector3.one, Gold, _gold);
            _crownParts[side].Add(ring.GetComponent<Renderer>());
            for (int i = 0; i < 5; i++)
            {
                float a = i * 72f * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Sin(a) * 0.39f, 0.42f, back + Mathf.Cos(a) * 0.39f);
                _crownParts[side].Add(Cone("Spike" + i, crown, p, new Vector3(0.2f, 0.42f, 0.2f), Gold, _gold).GetComponent<Renderer>());
                Prim(PrimitiveType.Sphere, "Gem" + i, crown, p + new Vector3(0, 0.44f, 0), Vector3.one * 0.13f, Ruby, _gloss);
            }
            Prim(PrimitiveType.Sphere, "HeartGem", crown, new Vector3(0, 0.33f, back + (side == 0 ? -0.45f : 0.45f)), new Vector3(0.24f, 0.24f, 0.12f), Ruby, _gloss);
            // Flip-digit counter in front of the crown (toward the middle), tilted up at the camera.
            var counter = new GameObject("Counter").transform;
            counter.SetParent(crown, false);
            counter.localPosition = new Vector3(0, 0.34f, side == 0 ? 0.42f : -0.42f);
            counter.localRotation = Quaternion.Euler(35, 0, 0); // tip the digits up toward the camera
            Prim(PrimitiveType.Cube, "CounterFrame", counter, Vector3.zero, new Vector3(0.95f, 0.62f, 0.12f), Gold, _gold);
            for (int d = 0; d < 2; d++)
            {
                var tile = Prim(PrimitiveType.Cube, "Digit" + d, counter, new Vector3(d == 0 ? -0.21f : 0.21f, 0, -0.05f), new Vector3(0.36f, 0.5f, 0.06f), new Color(0.12f, 0.06f, 0.05f)).transform;
                _digitTiles[side, d] = tile;
                var go = new GameObject("DigitText", typeof(RectTransform));
                go.transform.SetParent(tile, false);
                go.transform.localPosition = new Vector3(0, 0, -0.55f);
                var canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = _camera;
                var rt = (RectTransform)go.transform;
                rt.sizeDelta = new Vector2(100, 140);
                rt.localScale = new Vector3(1f / 100f, 1f / 140f, 1f);
                var text = Ui.Label("Text", go.transform, "0", 110, TextAnchor.MiddleCenter, new Color(1f, 0.93f, 0.75f), FontStyle.Bold);
                text.rectTransform.Fill();
                _digits[side, d] = text;
            }
            _wallLabels[side] = WorldLabel("WallLabel" + side, crown, new Vector3(2.0f, 0.5f, side == 0 ? 1.1f : -1.1f), 30, new Color(0.88f, 0.9f, 0.96f));
        }

        private void BuildWall(int side)
        {
            var c = CrownPos(side);
            float face = side == 0 ? 0 : 180;
            var wall = new GameObject("Wall" + side).transform;
            wall.SetParent(_root, false);
            wall.localPosition = new Vector3(c.x, 0.05f, c.z);
            for (int layer = 0; layer < 5; layer++)
            {
                var list = new List<Transform>();
                int n = BricksPerLayer - (layer % 2);
                float span = WallSpanDeg * 2f * (n - 0.02f) / BricksPerLayer;
                for (int b = 0; b < n; b++)
                {
                    float a = face - span / 2f + span * (b + 0.5f) / n;
                    float r = a * Mathf.Deg2Rad;
                    float shade = 0.9f + 0.1f * ((layer * 3 + b) % 3) / 2f;
                    var brick = Prim(PrimitiveType.Cube, "Brick", wall, new Vector3(Mathf.Sin(r) * WallRadius, 0, Mathf.Cos(r) * WallRadius),
                        new Vector3(WallRadius * 2f * WallSpanDeg * Mathf.Deg2Rad / BricksPerLayer * 0.94f, SegmentHeight * 0.9f, 0.3f), BrickColor * shade, null, a).transform;
                    list.Add(brick);
                }
                _bricks[side, layer] = list;
                _layerRise[side, layer] = 0f;
            }
        }

        private void BuildReelTray(int side)
        {
            float z = ReelZ(side);
            float outward = side == 0 ? -1f : 1f;
            float trayW = 5 * DrumSpacing + 0.5f;
            // Housing: a dark well framed by bronze, with a tall cap behind each drum (the reel "towers").
            Prim(PrimitiveType.Cube, "TrayWell" + side, _root, new Vector3(0, 0.02f, z), new Vector3(trayW, 0.1f, 1.95f), StoneDeep);
            foreach (var zz in new[] { z - 1.0f, z + 1.0f })
                Prim(PrimitiveType.Cube, "TrayLip" + side, _root, new Vector3(0, 0.14f, zz), new Vector3(trayW + 0.2f, 0.22f, 0.14f), Bronze, _bronze);
            for (int r = 0; r < 5; r++)
            {
                float x = ReelX(r);
                // Divider posts.
                foreach (var dx in new[] { -DrumSpacing / 2f, DrumSpacing / 2f })
                    Prim(PrimitiveType.Cube, "Post", _root, new Vector3(x + dx, 0.32f, z), new Vector3(0.12f, 0.6f, 1.95f), BronzeDark, _bronze);
                // Tower cap on the outer edge with a round window, like the housings in the reference.
                var cap = Prim(PrimitiveType.Cube, "Cap", _root, new Vector3(x, 0.5f, z + outward * 1.2f), new Vector3(1.06f, 0.95f, 0.45f), Bronze, _bronze);
                Prim(PrimitiveType.Cylinder, "CapWindow", _root, new Vector3(x, 0.99f, z + outward * 1.2f), new Vector3(0.42f, 0.02f, 0.42f), StoneDeep);
                MeshPart("CapRing", _root, MeshFactory.Arc(0.2f, 0.27f, 0, 360, 0.04f, 20), new Vector3(x, 0.97f, z + outward * 1.2f), Vector3.one, Gold, _gold);

                // The drum itself.
                var pivot = new GameObject("Drum" + side + r).transform;
                pivot.SetParent(_root, false);
                pivot.localPosition = new Vector3(x, DrumAxisY, z);
                _drums[side, r] = pivot;
                var body = MeshPart("DrumBody", pivot, MeshFactory.Drum, Vector3.zero, new Vector3(DrumLength, DrumApothem, DrumApothem), BronzeDark, _bronze);
                body.transform.localRotation = Quaternion.identity;
                foreach (var ex in new[] { -DrumLength / 2f - 0.02f, DrumLength / 2f + 0.02f })
                {
                    var hub = Prim(PrimitiveType.Cylinder, "Hub", pivot, new Vector3(ex, 0, 0), new Vector3(0.5f, 0.03f, 0.5f), Gold, _gold);
                    hub.transform.localRotation = Quaternion.Euler(0, 0, 90);
                }
                _drumAngle[side, r] = DisplayAngle(side, r, 0);

                // Lock clamp: lies flush along the window's near edge and snaps up across it when the reel is locked.
                float tilt = DisplayAngle(side, r, 0) * Mathf.Deg2Rad;
                float half = DrumApothem * Mathf.Tan(22.5f * Mathf.Deg2Rad);
                float nearZ = z + Mathf.Sin(tilt) * DrumApothem - Mathf.Cos(tilt) * half;
                var clamp = new GameObject("Clamp").transform;
                clamp.SetParent(_root, false);
                clamp.localPosition = new Vector3(x, ClampDown, nearZ - 0.1f);
                _clamps[side, r] = clamp;
                _clampBars[side, r] = Prim(PrimitiveType.Cube, "Bar", clamp, Vector3.zero, new Vector3(1.0f, 0.1f, 0.16f), BronzeDark, _bronze).GetComponent<Renderer>();
                foreach (var dx in new[] { -0.48f, 0.48f })
                    Prim(PrimitiveType.Cube, "Arm", clamp, new Vector3(dx, -0.15f, 0), new Vector3(0.08f, 0.3f, 0.08f), BronzeDark, _bronze);

                // Shutter that hides the opponent's result until the reveal.
                var shutter = Prim(PrimitiveType.Cube, "Shutter", _root, new Vector3(x, 0.62f, z), new Vector3(1.16f, 0.06f, 1.7f), Bronze, _bronze).transform;
                Prim(PrimitiveType.Cylinder, "ShutterBoss", shutter, new Vector3(0, 0.8f, 0), new Vector3(0.25f, 0.6f, 0.15f), Gold, _gold);
                _shutters[side, r] = shutter;
                shutter.gameObject.SetActive(false);
            }
        }

        private void BuildPodium(int side, int slot)
        {
            var pos = UnitPos(side, slot);
            var podium = new GameObject("Podium" + side + slot).transform;
            podium.SetParent(_root, false);
            podium.localPosition = pos;
            Prim(PrimitiveType.Cylinder, "Base", podium, new Vector3(0, 0.12f, 0), new Vector3(2.5f, 0.12f, 2.5f), StoneDeep);
            _podiumRims[side, slot] = MeshPart("Rim", podium, MeshFactory.Arc(1.02f, 1.18f, 0, 360, 0.34f, 40), Vector3.zero, Vector3.one, Bronze, _bronze).GetComponent<Renderer>();
            Prim(PrimitiveType.Cylinder, "Top", podium, new Vector3(0, 0.22f, 0), new Vector3(1.75f, 0.12f, 1.75f), StoneLight);
            Prim(PrimitiveType.Cylinder, "Dais", podium, new Vector3(0, 0.36f, 0), new Vector3(1.05f, 0.04f, 1.05f), new Color(0.13f, 0.07f, 0.06f));
            // Energy meter: segments set into the rim; how many depends on the unit's cost (placed in Render).
            _meter[side, slot] = new List<Transform>();
            for (int i = 0; i < 5; i++)
            {
                var seg = Prim(PrimitiveType.Cube, "Meter" + i, podium, Vector3.zero, new Vector3(0.32f, 0.12f, 0.2f), MeterOff, _gloss).transform;
                _meter[side, slot].Add(seg);
            }
            // Pillar with the unit's energy gem (square = A, diamond = B), on the table's outer side.
            float px = slot == 0 ? -1.75f : 1.75f;
            var pillar = new GameObject("Pillar").transform;
            pillar.SetParent(podium, false);
            pillar.localPosition = new Vector3(px, 0, side == 0 ? 0.6f : -0.6f);
            Prim(PrimitiveType.Cylinder, "Column", pillar, new Vector3(0, 0.5f, 0), new Vector3(0.26f, 0.5f, 0.26f), BronzeDark, _bronze);
            Prim(PrimitiveType.Cylinder, "Cup", pillar, new Vector3(0, 1.02f, 0), new Vector3(0.44f, 0.05f, 0.44f), Gold, _gold);
            var gem = Prim(PrimitiveType.Cube, "Gem", pillar, new Vector3(0, 1.25f, 0), Vector3.one * 0.34f, slot == 0 ? Theme.ChannelA : Theme.ChannelB, _gloss);
            gem.transform.localRotation = slot == 0 ? Quaternion.Euler(0, 15, 0) : Quaternion.Euler(45, 0, 45);
            _pillarGems[side, slot] = gem.GetComponent<Renderer>();

            // Stat plaque on the table in front of the podium (toward the table edge).
            // Always on the camera side of the podium so the figure never hides it.
            var plaque = Prim(PrimitiveType.Cube, "Plaque", podium, new Vector3(0, 0.08f, -1.62f), new Vector3(1.8f, 0.08f, 0.62f), BronzeDark, _bronze);
            var go = new GameObject("PlaqueText", typeof(RectTransform));
            go.transform.SetParent(podium, false);
            go.transform.localPosition = new Vector3(0, 0.14f, -1.62f);
            go.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = _camera;
            var prt = (RectTransform)go.transform;
            prt.sizeDelta = new Vector2(340, 100);
            prt.localScale = Vector3.one * (1.6f / 340f);
            var ptext = Ui.Label("Text", go.transform, "", 34, TextAnchor.MiddleCenter, new Color(1f, 0.88f, 0.62f), FontStyle.Bold);
            ptext.supportRichText = true;
            ptext.rectTransform.Fill();
            _plaques[side, slot] = ptext;

            // The piece: its root travels along the groove; the figure is rebuilt when the unit changes.
            var piece = new GameObject("Unit" + side + slot).transform;
            piece.SetParent(_root, false);
            piece.localPosition = pos;
            piece.localRotation = Quaternion.LookRotation(AttackPos(side, slot) - pos);
            _pieces[side, slot] = piece;
            _rankParts[side, slot] = new List<Renderer>();
        }

        /// <summary>Builds the unit figure (a metal miniature whose finish shows its rank).</summary>
        public void SetUnitShape(int side, int slot, UnitDefinition def)
        {
            if (_shapeFor[side, slot] == def.Id) return;
            _shapeFor[side, slot] = def.Id;
            var unit = _pieces[side, slot];
            var old = unit.Find("Figure");
            if (old != null) Kill(old.gameObject);
            var fig = new GameObject("Figure").transform;
            fig.SetParent(unit, false);
            fig.localPosition = new Vector3(0, 0.38f, 0);
            fig.localScale = Vector3.one * 1.25f;
            var parts = _rankParts[side, slot];
            parts.Clear();
            var trim = SideColor(side);
            Color rank = FigureFinish(Rank.Bronze);
            Renderer R(GameObject g) { parts.Add(g.GetComponent<Renderer>()); return g.GetComponent<Renderer>(); }
            R(Prim(PrimitiveType.Cylinder, "Base", fig, new Vector3(0, 0.04f, 0), new Vector3(0.95f, 0.04f, 0.95f), rank, _satin));
            if (def.Id == ReferenceContent.Striker)
            {
                R(Prim(PrimitiveType.Capsule, "Body", fig, new Vector3(0, 0.72f, 0), new Vector3(0.62f, 0.55f, 0.5f), rank, _satin));
                R(Prim(PrimitiveType.Sphere, "Head", fig, new Vector3(0, 1.42f, 0), Vector3.one * 0.42f, rank, _satin));
                Prim(PrimitiveType.Cube, "Visor", fig, new Vector3(0, 1.42f, 0.2f), new Vector3(0.3f, 0.06f, 0.06f), new Color(0.1f, 0.1f, 0.12f));
                R(Cone("Plume", fig, new Vector3(0, 1.58f, 0), new Vector3(0.16f, 0.35f, 0.16f), rank, _satin));
                var shield = R(Prim(PrimitiveType.Cylinder, "Shield", fig, new Vector3(-0.42f, 0.85f, 0.2f), new Vector3(0.62f, 0.04f, 0.62f), rank, _satin));
                shield.transform.localRotation = Quaternion.Euler(90, 0, 0);
                Prim(PrimitiveType.Cylinder, "ShieldBoss", fig, new Vector3(-0.45f, 0.85f, 0.25f), new Vector3(0.2f, 0.02f, 0.2f), trim, _gloss).transform.localRotation = Quaternion.Euler(90, 0, 0);
                var sword = new GameObject("Weapon").transform;
                sword.SetParent(fig, false);
                sword.localPosition = new Vector3(0.45f, 0.9f, 0.15f);
                sword.localRotation = Quaternion.Euler(0, 0, -20);
                R(Prim(PrimitiveType.Cube, "Blade", sword, new Vector3(0, 0.45f, 0), new Vector3(0.1f, 0.95f, 0.04f), rank, _satin));
                Prim(PrimitiveType.Cube, "Guard", sword, new Vector3(0, -0.02f, 0), new Vector3(0.36f, 0.07f, 0.08f), trim, _gloss);
            }
            else if (def.Id == ReferenceContent.Caster)
            {
                R(Cone("Robe", fig, new Vector3(0, 0.08f, 0), new Vector3(0.95f, 1.25f, 0.95f), rank, _satin));
                R(Prim(PrimitiveType.Sphere, "Head", fig, new Vector3(0, 1.3f, 0), Vector3.one * 0.38f, rank, _satin));
                R(Prim(PrimitiveType.Cylinder, "Brim", fig, new Vector3(0, 1.46f, 0), new Vector3(0.78f, 0.025f, 0.78f), rank, _satin));
                R(Cone("Hat", fig, new Vector3(0, 1.47f, 0), new Vector3(0.48f, 0.85f, 0.48f), rank, _satin));
                var staff = new GameObject("Weapon").transform;
                staff.SetParent(fig, false);
                staff.localPosition = new Vector3(0.5f, 0.1f, 0.1f);
                R(Prim(PrimitiveType.Cylinder, "Staff", staff, new Vector3(0, 0.85f, 0), new Vector3(0.07f, 0.85f, 0.07f), rank, _satin));
                Prim(PrimitiveType.Sphere, "Orb", staff, new Vector3(0, 1.78f, 0), Vector3.one * 0.26f, trim, _gloss);
            }
            else if (def.Id == ReferenceContent.Ranger)
            {
                R(Prim(PrimitiveType.Capsule, "Body", fig, new Vector3(0, 0.72f, 0), new Vector3(0.5f, 0.55f, 0.45f), rank, _satin));
                R(Prim(PrimitiveType.Sphere, "Head", fig, new Vector3(0, 1.38f, 0), Vector3.one * 0.36f, rank, _satin));
                R(Cone("Hood", fig, new Vector3(0, 1.4f, -0.04f), new Vector3(0.44f, 0.5f, 0.44f), rank, _satin));
                var bow = new GameObject("Weapon").transform;
                bow.SetParent(fig, false);
                bow.localPosition = new Vector3(0.42f, 0.95f, 0.2f);
                bow.localRotation = Quaternion.Euler(0, 90, 0);
                var arc = MeshPart("Bow", bow, MeshFactory.Arc(0.52f, 0.6f, -70, 70, 0.06f, 16), Vector3.zero, Vector3.one, rank, _satin);
                arc.transform.localRotation = Quaternion.Euler(0, 0, 90);
                parts.Add(arc.GetComponent<Renderer>());
                Prim(PrimitiveType.Cube, "String", bow, new Vector3(0, 0, 0.02f), new Vector3(0.015f, 1.0f, 0.015f), trim, _gloss);
                R(Prim(PrimitiveType.Cylinder, "Quiver", fig, new Vector3(-0.15f, 1.0f, -0.25f), new Vector3(0.18f, 0.3f, 0.18f), rank, _satin)).transform.localRotation = Quaternion.Euler(20, 0, 0);
            }
            else
            {
                R(Prim(PrimitiveType.Capsule, "Body", fig, new Vector3(0, 0.75f, 0), new Vector3(0.6f, 0.6f, 0.6f), rank, _satin));
                R(Prim(PrimitiveType.Sphere, "Head", fig, new Vector3(0, 1.5f, 0), Vector3.one * 0.4f, rank, _satin));
                Prim(PrimitiveType.Cube, "Badge", fig, new Vector3(0, 0.9f, 0.3f), new Vector3(0.3f, 0.3f, 0.05f), trim, _gloss);
                var hand = new GameObject("Weapon").transform;
                hand.SetParent(fig, false);
                hand.localPosition = new Vector3(0.4f, 0.8f, 0.15f);
            }
        }

        /// <summary>Paints the faces of each reel drum from its definition (called when a match starts).</summary>
        public void SetReels(int side, IReadOnlyList<ReelDefinition> reels)
        {
            for (int r = 0; r < 5 && r < reels.Count; r++)
            {
                var def = reels[r];
                string key = string.Join(",", System.Linq.Enumerable.Select(def.Faces, f => f.Code));
                if (_reelKey[side, r] == key) continue;
                _reelKey[side, r] = key;
                for (int k = 0; k < 8; k++)
                {
                    if (_faceRoots[side, r, k] != null) Kill(_faceRoots[side, r, k]);
                    _faceRoots[side, r, k] = k < def.Faces.Count ? BuildFace(_drums[side, r], k, def.Faces[k]) : null;
                }
            }
        }

        private GameObject BuildFace(Transform drum, int k, ReelFace face)
        {
            float t = k * 45f * Mathf.Deg2Rad;
            var normal = new Vector3(0, Mathf.Cos(t), Mathf.Sin(t));
            var tangent = new Vector3(0, -Mathf.Sin(t), Mathf.Cos(t));
            float faceH = 2f * DrumApothem * Mathf.Tan(22.5f * Mathf.Deg2Rad);
            var go = new GameObject("Face" + k, typeof(RectTransform));
            go.transform.SetParent(drum, false);
            go.transform.localPosition = normal * (DrumApothem + 0.004f);
            go.transform.localRotation = Quaternion.LookRotation(-normal, tangent);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = _camera;
            var rt = (RectTransform)go.transform;
            float pxW = 200f, pxH = pxW * faceH / (DrumLength - 0.06f);
            rt.sizeDelta = new Vector2(pxW, pxH);
            rt.localScale = Vector3.one * ((DrumLength - 0.06f) / pxW);
            var bg = Ui.Panel("Back", go.transform, face.XpChannel.HasValue ? FaceXp : FaceBack);
            bg.rectTransform.Fill(3);
            if (face.XpChannel.HasValue)
                for (int s = 0; s < 7; s++)
                {
                    var star = Ui.Panel("Speck", bg.transform, new Color(0.75f, 0.75f, 1f, 0.55f));
                    star.rectTransform.Place(12 + (s * 53) % 176, 8 + (s * 37) % (pxH - 20), 4, 4);
                }
            Sprite sprite; int count; Color fallback;
            if (face.ChannelA > 0) { sprite = _icons != null ? _icons.energyA : null; count = face.ChannelA; fallback = Theme.ChannelA; }
            else if (face.ChannelB > 0) { sprite = _icons != null ? _icons.energyB : null; count = face.ChannelB; fallback = Theme.ChannelB; }
            else if (face.Hammer > 0) { sprite = _icons != null ? _icons.hammer : null; count = face.Hammer; fallback = Theme.Hammer; }
            else { sprite = null; count = 0; fallback = Color.clear; }
            float size = Mathf.Min(pxH - 16f, count <= 1 ? 110f : count == 2 ? 80f : 60f);
            float totalW = count * size + (count - 1) * 4f;
            for (int i = 0; i < count; i++)
            {
                var icon = Ui.Icon("Symbol", bg.transform, sprite, size, fallback);
                icon.rectTransform.Place(100 - totalW / 2f + i * (size + 4f) - 3, (pxH - size) / 2f - 3, size, size);
            }
            if (face.XpChannel.HasValue && _icons != null)
                Ui.Icon("Star", bg.transform, _icons.xp, 34, Theme.Xp).rectTransform.Place(200 - 44, 4, 34, 34);
            return go;
        }

        // ------------------------------------------------------------------ geometry queries

        /// <summary>The angle (about the drum axis) that turns face <paramref name="k"/> to look at the camera.</summary>
        private float DisplayAngle(int side, int reel, int k)
        {
            float target = -30f;
            if (_camera != null && _drums[side, reel] != null)
            {
                var d = _camera.transform.position - _drums[side, reel].position;
                target = Mathf.Atan2(d.z, d.y) * Mathf.Rad2Deg;
            }
            return target - k * 45f;
        }

        /// <summary>True while a drum's shutter is closed (the opponent's result before the reveal).</summary>
        public bool ReelShuttered(int side, int reel) => _shutters[side, reel].gameObject.activeSelf;

        /// <summary>World corners of the reel face currently shown (for laying the reel buttons over the drum).</summary>
        public void ReelFaceCorners(int side, int reel, Vector3[] corners)
        {
            var drum = _drums[side, reel];
            // Whatever face is showing sits in the display pose (face 0's display angle is the pose's normal).
            float a = DisplayAngle(side, reel, 0) * Mathf.Deg2Rad;
            var n = new Vector3(0, Mathf.Cos(a), Mathf.Sin(a));
            var tan = new Vector3(0, -Mathf.Sin(a), Mathf.Cos(a));
            float half = DrumApothem * Mathf.Tan(22.5f * Mathf.Deg2Rad);
            var c = drum.position + n * DrumApothem;
            var x = Vector3.right * (DrumLength / 2f);
            corners[0] = c - x - tan * half;
            corners[1] = c - x + tan * half;
            corners[2] = c + x + tan * half;
            corners[3] = c + x - tan * half;
        }

        // ------------------------------------------------------------------ per frame

        public void Render(VisualState v, Match match, MatchEvent current, float progress, bool applied, bool reducedMotion, bool opponentRevealed)
        {
            if (_root == null || match == null) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            for (int side = 0; side < 2; side++)
            {
                RenderCrown(side, v.Crown[side], dt, reducedMotion);
                RenderWall(side, v.Barrier[side], dt, reducedMotion);
                RenderReels(side, v, current, progress, dt, reducedMotion, side == 1 && !opponentRevealed);
                for (int slot = 0; slot < 2; slot++) RenderUnit(side, slot, v, match, current, progress, dt, reducedMotion);
            }
            RenderProjectile(current, progress, reducedMotion);
            UpdateFloating(reducedMotion);
            UpdateDebris(dt);
            if (_camera != null)
                foreach (var b in _billboards) b.rotation = _camera.transform.rotation;
            transform.localPosition = _shake;
        }

        private void RenderCrown(int side, int hp, float dt, bool reducedMotion)
        {
            bool broken = hp <= 0;
            foreach (var r in _crownParts[side]) SetColor(r, broken ? new Color(0.35f, 0.33f, 0.3f) : Gold);
            _crowns[side].localRotation = Quaternion.identity;
            if (_shownHp[side] == -99) { _shownHp[side] = hp; SetDigits(side, hp); }
            if (hp != _shownHp[side] && _flip[side] >= 1f) { _pendingHp[side] = hp; _flip[side] = reducedMotion ? 1f : 0f; if (reducedMotion) { _shownHp[side] = hp; SetDigits(side, hp); } }
            if (_flip[side] < 1f)
            {
                // Split-flap: the tiles fold down, the number changes, and they fold back up.
                _flip[side] = Mathf.Min(1f, _flip[side] + dt * 5f);
                if (_flip[side] >= 0.5f && _shownHp[side] != _pendingHp[side]) { _shownHp[side] = _pendingHp[side]; SetDigits(side, _shownHp[side]); }
                float fold = Mathf.Abs(Mathf.Cos(_flip[side] * Mathf.PI));
                for (int d = 0; d < 2; d++) _digitTiles[side, d].localScale = new Vector3(0.36f, 0.5f * Mathf.Max(0.05f, fold), 0.06f);
            }
            else for (int d = 0; d < 2; d++) _digitTiles[side, d].localScale = new Vector3(0.36f, 0.5f, 0.06f);
        }

        private void SetDigits(int side, int hp)
        {
            int n = Mathf.Clamp(hp, 0, 99);
            _digits[side, 0].text = (n / 10).ToString();
            _digits[side, 1].text = (n % 10).ToString();
            var c = hp <= 0 ? Theme.Damage : hp > RulesConstants.NormalCrownCap ? Theme.Heal : new Color(1f, 0.93f, 0.75f);
            _digits[side, 0].color = c;
            _digits[side, 1].color = c;
        }

        private void RenderWall(int side, int barrier, float dt, bool reducedMotion)
        {
            for (int layer = 0; layer < 5; layer++)
            {
                float target = layer < barrier ? 1f : 0f;
                float rise = reducedMotion ? target : Mathf.MoveTowards(_layerRise[side, layer], target, dt * 3.2f);
                _layerRise[side, layer] = rise;
                float y = (layer + 0.5f) * SegmentHeight - (1f - rise) * (layer + 1.2f) * SegmentHeight;
                foreach (var b in _bricks[side, layer])
                {
                    var p = b.localPosition;
                    b.localPosition = new Vector3(p.x, y, p.z);
                    b.gameObject.SetActive(rise > 0.001f);
                }
            }
            _wallLabels[side].text = barrier > 0 ? "WALL " + barrier : "";
        }

        private void RenderReels(int side, VisualState v, MatchEvent current, float progress, float dt, bool reducedMotion, bool hidden)
        {
            bool spinning = current != null && current.Type == MatchEventType.ReelsSpun && current.Side == side;
            for (int r = 0; r < 5; r++)
            {
                int face = v.Face[side, r];
                float target = DisplayAngle(side, r, face >= 0 ? face : 0);
                float angle;
                if (spinning && current.Changed != null && current.Changed[r] && !reducedMotion && progress < 1f)
                {
                    // Two and a bit turns that slow into place, each drum a little behind its neighbour.
                    float t = Mathf.Clamp01(progress * 1.15f - r * 0.03f);
                    float ease = 1f - Mathf.Pow(1f - t, 3f);
                    angle = target - (1f - ease) * 720f;
                }
                else angle = reducedMotion ? target : Mathf.MoveTowardsAngle(_drumAngle[side, r], target, dt * 900f);
                _drumAngle[side, r] = angle;
                _drums[side, r].localRotation = Quaternion.Euler(angle, 0, 0);
                // At rest, only the face in the window is painted; its neighbours would read as extra results.
                bool settled = Mathf.Abs(Mathf.DeltaAngle(angle, target)) < 2f;
                for (int k = 0; k < 8; k++)
                {
                    var fr = _faceRoots[side, r, k];
                    if (fr != null) fr.SetActive(!settled || k == face);
                }

                bool locked = v.Locked[side, r];
                var clamp = _clamps[side, r];
                float y = locked ? ClampUp : ClampDown;
                var cp = clamp.localPosition;
                clamp.localPosition = new Vector3(cp.x, reducedMotion ? y : Mathf.MoveTowards(cp.y, y, dt * 4f), cp.z);
                SetColor(_clampBars[side, r], locked ? Theme.Locked : BronzeDark);
                _shutters[side, r].gameObject.SetActive(hidden);
            }
        }

        private void RenderUnit(int side, int slot, VisualState v, Match match, MatchEvent current, float progress, float dt, bool reducedMotion)
        {
            var def = match.UnitDefinition((SideId)side, slot);
            var rank = v.Rank[side, slot];
            var rankColor = FigureFinish(rank);
            foreach (var r in _rankParts[side, slot]) if (r != null) SetColor(r, rankColor);
            int cost = def.Stats(rank).EnergyCost;
            int energy = v.Energy[side, slot];
            var channel = slot == 0 ? Theme.ChannelA : Theme.ChannelB;
            PlaceMeter(side, slot, cost);
            for (int i = 0; i < _meter[side, slot].Count; i++)
                SetColor(_meter[side, slot][i].GetComponent<Renderer>(), i < energy ? channel : MeterOff);
            bool ready = energy >= cost;
            float pulse = ready ? 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f) : 0.35f;
            SetColor(_pillarGems[side, slot], Color.Lerp(Color.black, channel, pulse));
            var s = def.Stats(rank);
            string stats = def.Action == ActionKind.PriestBlessing ? "HEAL " + s.Heal
                : def.Action == ActionKind.AssassinStrike ? "CROWN " + s.CrownDamage + "  DRAIN " + s.Delay
                : "CROWN " + s.CrownDamage + "  WALL " + s.BarrierDamage;
            string name = "<color=#" + ColorUtility.ToHtmlStringRGB(SideColor(side)) + ">" + def.DisplayName.ToUpperInvariant() + "</color>";
            _plaques[side, slot].text = name + (ready ? "  <color=#FFE066>READY!</color>" : "  " + energy + "/" + cost) + "\n<size=28>" + stats + "</size>";

            // Travel: out along the groove while this unit's action plays, home again afterwards.
            bool acting = current != null && current.Side == side && current.Slot == slot && current.Stage > 0
                          && (current.Type == MatchEventType.UnitActivated || current.Type == MatchEventType.ProjectileResolved
                              || current.Type == MatchEventType.BombLaunched || current.Type == MatchEventType.EnergyDelayed
                              || current.Type == MatchEventType.CrownDamaged || current.Type == MatchEventType.BarrierDamaged
                              || current.Type == MatchEventType.CrownHealed || current.Type == MatchEventType.EnergyGranted && current.Source == EnergySource.Priest);
            var home = UnitPos(side, slot);
            var goal = acting ? AttackPos(side, slot) - home : Vector3.zero;
            var off = reducedMotion ? goal : Vector3.MoveTowards(_pieceOffset[side, slot], goal, dt * 6.5f);
            _pieceOffset[side, slot] = off;
            var piece = _pieces[side, slot];
            // A wind-up toy's shuffle while it travels.
            bool moving = (off - goal).sqrMagnitude > 0.0004f;
            float hop = moving && !reducedMotion ? Mathf.Abs(Mathf.Sin(Time.unscaledTime * 22f)) * 0.08f : 0f;
            piece.localPosition = home + off + new Vector3(0, hop, 0);

            // Strike: lunge on each shot; the weapon swings.
            var fig = piece.Find("Figure");
            if (fig != null)
            {
                float lunge = 0f;
                if (acting && !reducedMotion && (current.Type == MatchEventType.ProjectileResolved || current.Type == MatchEventType.BombLaunched))
                    lunge = Mathf.Sin(Mathf.Clamp01(progress * 2.5f) * Mathf.PI) * 0.35f;
                fig.localPosition = new Vector3(0, 0.38f, lunge);
                fig.localScale = Vector3.one * 1.25f;
                var weapon = fig.Find("Weapon");
                if (weapon != null)
                {
                    float swing = acting && !reducedMotion ? Mathf.Sin(Mathf.Clamp01(progress * 2.5f) * Mathf.PI) * 70f : 0f;
                    weapon.localRotation = def.Id == ReferenceContent.Striker ? Quaternion.Euler(swing, 0, -20) : Quaternion.Euler(swing * 0.5f, 0, 0);
                }
            }
            SetColor(_podiumRims[side, slot], acting ? Color.Lerp(Bronze, Color.white, 0.5f) : Bronze);
        }

        /// <summary>Spreads the meter segments evenly around the front of the rim for the current cost.</summary>
        private void PlaceMeter(int side, int slot, int cost)
        {
            if (_meterCount[side, slot] == cost) return;
            _meterCount[side, slot] = cost;
            var list = _meter[side, slot];
            // Front arc faces the camera (toward -z); segments fan out 130 degrees either side of it.
            for (int i = 0; i < list.Count; i++)
            {
                bool on = i < cost;
                list[i].gameObject.SetActive(on);
                if (!on) continue;
                float span = 150f;
                float a = (180f - span / 2f + span * (cost == 1 ? 0.5f : i / (float)(cost - 1))) * Mathf.Deg2Rad;
                list[i].localPosition = new Vector3(Mathf.Sin(a) * 1.1f, 0.36f, Mathf.Cos(a) * 1.1f);
                list[i].localRotation = Quaternion.Euler(0, a * Mathf.Rad2Deg, 0);
            }
        }

        private void RenderProjectile(MatchEvent e, float t, bool reducedMotion)
        {
            bool show = e != null && (e.Type == MatchEventType.ProjectileResolved || e.Type == MatchEventType.BombLaunched);
            _projectile.gameObject.SetActive(show);
            if (!show) return;
            Vector3 from = _pieces[e.Side, e.Slot].localPosition + new Vector3(0, 1.2f, 0);
            Vector3 to;
            if (e.Type == MatchEventType.BombLaunched)
            {
                to = CrownPos(e.TargetSide) + new Vector3(0, 0.6f, 0);
                SetColor(_projectileRenderer, new Color(0.08f, 0.08f, 0.1f));
            }
            else if (e.TargetIsCrown)
            {
                to = CrownPos(e.TargetSide) + new Vector3(0, 0.6f, 0);
                from.y = HeightY(e.Height) + 0.3f;
                SetColor(_projectileRenderer, SideColor(e.Side));
            }
            else
            {
                float x = Mathf.Clamp(from.x * 0.25f, -0.9f, 0.9f);
                to = new Vector3(x, HeightY(e.Height), WallZ(e.TargetSide) + (e.TargetSide == 0 ? 0.2f : -0.2f));
                from.y = HeightY(e.Height) + 0.3f;
                SetColor(_projectileRenderer, SideColor(e.Side));
            }
            if (reducedMotion)
            {
                _projectile.localPosition = to;
                _projectile.localScale = Vector3.one * 0.4f * Mathf.Clamp01(t * 2f);
                return;
            }
            float tt = Mathf.Clamp01((t - 0.15f) / 0.85f); // leaves once the piece has lunged
            var p = Vector3.Lerp(from, to, tt);
            float arc = e.Type == MatchEventType.BombLaunched ? 3.4f : (e.Height >= 6 ? 1.1f : 0.25f);
            p.y += arc * Mathf.Sin(tt * Mathf.PI);
            _projectile.localPosition = p;
            _projectile.localScale = Vector3.one * (tt > 0f ? 0.4f : 0f);
        }

        // ------------------------------------------------------------------ impacts

        /// <summary>Pops a rising number/word at the thing that changed (called at an event's impact point).</summary>
        public void ShowDelta(MatchEvent e)
        {
            string text = null;
            Color color = Color.white;
            Vector3 at = Vector3.zero;
            switch (e.Type)
            {
                case MatchEventType.CrownDamaged:
                    text = "-" + e.Amount; color = Theme.Damage; at = CrownPos(e.TargetSide) + new Vector3(0, 1.5f, 0); break;
                case MatchEventType.CrownHealed:
                    text = "+" + e.Amount; color = Theme.Heal; at = CrownPos(e.Side) + new Vector3(0, 1.5f, 0); break;
                case MatchEventType.BarrierDamaged:
                    text = "WALL -" + e.Amount; color = new Color(0.85f, 0.88f, 1f); at = new Vector3(0, 1.9f, WallZ(e.TargetSide));
                    SpawnDebris(e.TargetSide, e.Amount); break;
                case MatchEventType.BarrierBuilt:
                    text = "WALL +" + e.Amount; color = new Color(0.85f, 0.88f, 1f); at = new Vector3(0, 1.9f, WallZ(e.Side)); break;
                case MatchEventType.EnergyGranted:
                    text = "+" + e.Amount + " ENERGY"; color = e.Slot == 0 ? Theme.ChannelA : Theme.ChannelB; at = UnitPos(e.Side, e.Slot) + new Vector3(0, 2.2f, 0); break;
                case MatchEventType.EnergyDelayed:
                    text = "-" + e.Amount + " ENERGY"; color = Theme.Damage; at = UnitPos(e.TargetSide, e.TargetSlot) + new Vector3(0, 2.2f, 0); break;
                case MatchEventType.PanelXpGranted:
                case MatchEventType.ActionXpGranted:
                    text = "+" + e.Amount + " XP"; color = Theme.Xp; at = UnitPos(e.Side, e.Slot) + new Vector3(0, 2.9f, 0); break;
                case MatchEventType.UnitRankedUp:
                    text = "RANK UP!"; color = Theme.Crown; at = UnitPos(e.Side, e.Slot) + new Vector3(0, 3.1f, 0); break;
                case MatchEventType.BombQueued:
                    text = "BOMB ARMED!"; color = Theme.Crown; at = UnitPos(e.Side, e.Slot) + new Vector3(0, 3.1f, 0); break;
            }
            if (text == null || (e.Amount == 0 && e.Type != MatchEventType.UnitRankedUp && e.Type != MatchEventType.BombQueued)) return;
            var label = WorldLabel("Delta", _root, at, 46, color);
            label.text = text;
            _floating.Add(new FloatingText { Root = label.transform.parent, Label = label, Start = at, Age = 0 });
        }

        /// <summary>Knocked-out bricks tumble off the wall (decoration only).</summary>
        private void SpawnDebris(int side, int amount)
        {
            int n = Mathf.Clamp(amount * 3, 2, 9);
            var c = CrownPos(side);
            for (int i = 0; i < n; i++)
            {
                float a = ((side == 0 ? 0 : 180) + Random.Range(-WallSpanDeg, WallSpanDeg)) * Mathf.Deg2Rad;
                var p = new Vector3(c.x + Mathf.Sin(a) * WallRadius, 0.2f + Random.value * 1.0f, c.z + Mathf.Cos(a) * WallRadius);
                var go = Prim(PrimitiveType.Cube, "Chunk", _root, p, new Vector3(0.22f, 0.16f, 0.18f), BrickColor, null);
                var outward = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                _debris.Add(new Debris { T = go.transform, Velocity = outward * Random.Range(1.5f, 3f) + Vector3.up * Random.Range(2f, 4f), Spin = Random.insideUnitSphere * 540f });
            }
        }

        private void UpdateDebris(float dt)
        {
            for (int i = _debris.Count - 1; i >= 0; i--)
            {
                var d = _debris[i];
                d.Age += dt;
                d.Velocity += Vector3.down * 14f * dt;
                d.T.localPosition += d.Velocity * dt;
                d.T.localRotation *= Quaternion.Euler(d.Spin * dt);
                if (d.T.localPosition.y < 0.1f) { d.T.localPosition = new Vector3(d.T.localPosition.x, 0.1f, d.T.localPosition.z); d.Velocity *= 0.3f; d.Spin *= 0.5f; }
                if (d.Age > 1.2f) { Kill(d.T.gameObject); _debris.RemoveAt(i); }
            }
        }

        private void UpdateFloating(bool reducedMotion)
        {
            for (int i = _floating.Count - 1; i >= 0; i--)
            {
                var f = _floating[i];
                f.Age += Time.unscaledDeltaTime;
                const float life = 1.3f;
                if (f.Age >= life)
                {
                    _billboards.Remove(f.Root);
                    Kill(f.Root.gameObject);
                    _floating.RemoveAt(i);
                    continue;
                }
                f.Root.localPosition = f.Start + (reducedMotion ? Vector3.zero : new Vector3(0, 0.8f * f.Age, 0));
                var c = f.Label.color;
                c.a = Mathf.Clamp01(1.6f * (1f - f.Age / life));
                f.Label.color = c;
            }
        }

        public void ClearFloating()
        {
            foreach (var f in _floating) { _billboards.Remove(f.Root); Kill(f.Root.gameObject); }
            _floating.Clear();
            foreach (var d in _debris) Kill(d.T.gameObject);
            _debris.Clear();
        }

        public void Shake(float amount) => _shake = Random.insideUnitSphere * amount;
        public void StopShake() => _shake = Vector3.zero;

        // ------------------------------------------------------------------ helpers

        /// <summary>Camera-facing world-space text.</summary>
        private Text WorldLabel(string name, Transform parent, Vector3 localPos, int size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = _camera;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(500, 180);
            rt.localScale = Vector3.one * 0.009f;
            var text = Ui.Label("Text", go.transform, "", size, TextAnchor.MiddleCenter, color, FontStyle.Bold);
            text.rectTransform.Fill();
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.9f);
            outline.effectDistance = new Vector2(3, -3);
            _billboards.Add(go.transform);
            return text;
        }

        private GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Color color, Material mat = null, float yaw = 0f)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) Kill(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            go.transform.localScale = scale;
            var renderer = go.GetComponent<Renderer>();
            var m = mat != null ? mat : _material;
            if (m != null) renderer.sharedMaterial = m;
            SetColor(renderer, color);
            return go;
        }

        private GameObject MeshPart(string name, Transform parent, Mesh mesh, Vector3 pos, Vector3 scale, Color color, Material mat = null)
        {
            var go = Prim(PrimitiveType.Cube, name, parent, pos, scale, color, mat);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            return go;
        }

        private GameObject Cone(string name, Transform parent, Vector3 pos, Vector3 scale, Color color, Material mat = null)
        {
            var go = Prim(PrimitiveType.Cylinder, name, parent, pos, scale, color, mat);
            go.GetComponent<MeshFilter>().sharedMesh = MeshFactory.Cone;
            return go;
        }

        private static void Kill(Object o)
        {
            if (o == null) return;
            if (UnityEngine.Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static MaterialPropertyBlock _mpb;

        private static void SetColor(Renderer r, Color c)
        {
            if (r == null) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColor, c);
            _mpb.SetColor(ColorId, c);
            r.SetPropertyBlock(_mpb);
        }
    }
}
