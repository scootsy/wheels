using System.Collections.Generic;
using Tabletop.Application;
using Tabletop.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Tabletop.World
{
    /// <summary>Something the player can walk up to and use with the Interact action.</summary>
    public abstract class Interactable : MonoBehaviour
    {
        public float Radius = 2.4f;
        public abstract string Prompt { get; }
        public virtual bool Available => true;
        public abstract void Interact(WorldApp app);
    }

    /// <summary>A villager: talks, and (if they have an encounter) can be challenged to a match.</summary>
    public sealed class Npc : Interactable
    {
        public string DisplayName;
        public string Title;
        [System.NonSerialized] public EncounterDefinition Encounter;
        public string[] Lines = new string[0];
        public Transform Figure;
        [System.NonSerialized] public NameTagView Tag;
        public Image Marker;
        public float HomeYaw;
        /// <summary>Rest offset of the figure (e.g. lowered when seated).</summary>
        public Vector3 BaseOffset;
        private float _bobPhase;

        public override string Prompt => "Talk to " + DisplayName;

        public void RefreshTag()
        {
            if (Tag == null) return;
            bool beaten = Encounter != null && GameFlow.HasDefeated(Encounter.Id);
            var kit = Ui.Kit;
            string sub;
            Color accent;
            Sprite emblem = null;
            if (ShopId != null) { sub = (Title ?? "").ToUpperInvariant() + "  -  STALL"; accent = Theme.Gilt; emblem = kit != null ? kit.hammer : null; }
            else if (Encounter == null) { var hint = ErrandHint(); sub = (Title ?? "").ToUpperInvariant() + hint; accent = hint.Length > 0 ? Theme.Gilt : Theme.TextDim; }
            else if (Encounter.Tournament)
            {
                bool due = GameFlow.TournamentRound == Encounter.TournamentRound;
                sub = (Encounter.TournamentRound == EncounterCatalog.TournamentRounds ? "THE FINAL" : "ROUND " + Encounter.TournamentRound) + (due ? "  -  YOUR NEXT MATCH" : beaten ? "  -  BEATEN" : "");
                accent = due ? Theme.Gilt : Theme.TextDim;
                emblem = kit != null ? kit.crown : null;
            }
            else if (beaten) { sub = "BEATEN"; accent = new Color(0.55f, 0.8f, 0.55f); emblem = kit != null ? kit.uiDiamond : null; }
            else if (Encounter.IsChampion) { sub = "CHAMPION  -  CHALLENGE"; accent = Theme.Gilt; emblem = kit != null ? kit.crown : null; }
            else { sub = "CHALLENGER"; accent = Theme.Gilt; emblem = kit != null ? kit.uiDiamond : null; }
            Tag.Set(DisplayName, sub, emblem, accent);
            bool challenge = Encounter != null && (Encounter.Tournament ? GameFlow.TournamentRound == Encounter.TournamentRound : !beaten);
            if (Marker != null) Marker.gameObject.SetActive(ShopId != null || challenge || (Role == "herald" && GameFlow.TournamentQualified && GameFlow.TournamentRound == 0));
        }

        /// <summary>"  -  HAS WORK" / "  -  DELIVERY HERE" when an errand involves this person (D-033).</summary>
        private string ErrandHint()
        {
            foreach (var e in ErrandCatalog.All)
            {
                var st = GameFlow.ErrandState(e.Id);
                if (e.IsDelivery && e.Receiver == DisplayName && st == ErrandStage.Active) return "  -  DELIVERY HERE";
                if (e.Giver == DisplayName && st == ErrandStage.Found) return "  -  WAITING FOR YOU";
                if (e.Giver == DisplayName && ErrandCatalog.Offerable(e)) return "  -  HAS WORK";
            }
            return "";
        }

        public void FaceTowards(Vector3 worldPos)
        {
            var d = worldPos - transform.position;
            d.y = 0;
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d);
        }

        public void FaceHome() => transform.rotation = Quaternion.Euler(0, HomeYaw, 0);

        private CanvasGroup _tagGroup;

        /// <summary>Name tags fade in as the player gets close instead of floating over everyone (D-027).</summary>
        public void SetTagAlpha(float alpha)
        {
            if (Tag == null) return;
            if (_tagGroup == null)
            {
                _tagGroup = Tag.Canvas.gameObject.GetComponent<CanvasGroup>();
                if (_tagGroup == null) _tagGroup = Tag.Canvas.gameObject.AddComponent<CanvasGroup>();
                _tagGroup.blocksRaycasts = false;
            }
            _tagGroup.alpha = alpha;
        }

        public float TagAlpha => _tagGroup != null ? _tagGroup.alpha : 1f;

        /// <summary>The animated body (D-033), or null for a placeholder figure.</summary>
        [System.NonSerialized] public CharacterRig Rig;
        /// <summary>Stall this person keeps (D-033), or null.</summary>
        public string ShopId;

        private void Update()
        {
            // Gentle idle breathing so placeholder people read as alive (real characters breathe by themselves).
            if (Figure == null || Rig != null) return;
            _bobPhase += Time.deltaTime * 2f;
            Figure.localPosition = BaseOffset + new Vector3(0, Mathf.Abs(Mathf.Sin(_bobPhase)) * 0.03f, 0);
        }

        public override void Interact(WorldApp app) => app.TalkTo(this);

        /// <summary>A special part in the story (D-038): "herald" enters the player in the Grand Tournament.</summary>
        public string Role;
        /// <summary>Where this person stands when not seated at a table (tournament opponents move to the table on their round).</summary>
        [System.NonSerialized] public Vector3 HomePosition;
        [System.NonSerialized] public bool Seated;
        [System.NonSerialized] public float StandingYaw;
    }

    /// <summary>Something tall (a tree, a building) that the overhead camera sees through when it hides the player (D-038).</summary>
    public sealed class Occluder
    {
        public Renderer[] Renderers;
        public Bounds Bounds;
        public bool Hidden;

        public void SetHidden(bool hidden)
        {
            if (hidden == Hidden) return;
            Hidden = hidden;
            var mode = hidden ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
            foreach (var r in Renderers) if (r != null) r.shadowCastingMode = mode;
        }
    }

    /// <summary>A door that moves the player somewhere else (e.g. into the Champion's Hall).</summary>
    public sealed class Door : Interactable
    {
        public string Label;
        public Vector3 Target;
        public float TargetYaw;
        public string AreaName;
        public override string Prompt => Label;
        public override void Interact(WorldApp app) => app.UseDoor(this);
    }

    /// <summary>The challenger's chair at the Champion's table.</summary>
    public sealed class Chair : Interactable
    {
        public Npc Champion;
        /// <summary>The Grand Tournament's table (D-038): only open while the player is in the tournament.</summary>
        public bool TournamentTable;
        public override bool Available => !TournamentTable || (GameFlow.TournamentRound > 0 && Champion != null);
        public Vector3 SeatPosition;
        /// <summary>Centre of the table the camera frames while seated.</summary>
        public Vector3 TableFocus;
        public override string Prompt => "Sit down at the table";
        public override void Interact(WorldApp app) => app.SitAt(this);
    }

    /// <summary>Something an errand asks you to find (D-033); only there while that errand is under way.</summary>
    public sealed class Pickup : Interactable
    {
        public string PickupId;
        [System.NonSerialized] public ErrandDefinition Errand;
        public GameObject Visual;
        private float _phase;

        public override string Prompt => "Pick up " + (Errand != null ? Errand.ItemName : "it");
        public override bool Available => Errand != null && GameFlow.ErrandState(Errand.Id) == ErrandStage.Active;
        public override void Interact(WorldApp app) => app.PickUp(this);

        private void Update()
        {
            bool on = Available;
            if (Visual != null && Visual.activeSelf != on) Visual.SetActive(on);
            if (on && Visual != null)
            {
                _phase += Time.deltaTime;
                Visual.transform.localPosition = new Vector3(0, 0.15f + Mathf.Sin(_phase * 2.5f) * 0.08f, 0);
                Visual.transform.localRotation = Quaternion.Euler(0, _phase * 60f, 0);
            }
        }
    }

    /// <summary>A readable sign.</summary>
    public sealed class Sign : Interactable
    {
        public string Title;
        public string Text;
        public override string Prompt => "Read the sign";
        public override void Interact(WorldApp app) => app.ReadSign(this);
    }

    /// <summary>Where the player may walk: union of rectangles, circles, and path capsules (XZ plane).</summary>
    public sealed class WalkableArea
    {
        private readonly List<Vector4> _rects = new List<Vector4>();   // xmin, xmax, zmin, zmax
        private readonly List<Vector3> _circles = new List<Vector3>(); // x, z, r
        private readonly List<(Vector2 a, Vector2 b, float r)> _capsules = new List<(Vector2, Vector2, float)>();
        private readonly List<Vector4> _holes = new List<Vector4>();

        public void Rect(float xmin, float xmax, float zmin, float zmax) => _rects.Add(new Vector4(xmin, xmax, zmin, zmax));
        public void Circle(float x, float z, float r) => _circles.Add(new Vector3(x, z, r));
        public void Capsule(Vector2 a, Vector2 b, float r) => _capsules.Add((a, b, r));
        /// <summary>A rectangle cut out of everything else (e.g. the stream either side of the bridge).</summary>
        public void Hole(float xmin, float xmax, float zmin, float zmax) => _holes.Add(new Vector4(xmin, xmax, zmin, zmax));

        public bool Contains(float x, float z)
        {
            foreach (var h in _holes) if (x >= h.x && x <= h.y && z >= h.z && z <= h.w) return false;
            return InsideShapes(x, z);
        }

        /// <summary>Distance to the nearest walkable shape (0 inside) and the closest walkable point (holes ignored).</summary>
        public float Distance(float x, float z, out Vector2 nearest)
        {
            var p = new Vector2(x, z);
            float best = float.MaxValue;
            nearest = p;
            foreach (var r in _rects)
            {
                var q = new Vector2(Mathf.Clamp(x, r.x, r.y), Mathf.Clamp(z, r.z, r.w));
                float d = (q - p).magnitude;
                if (d < best) { best = d; nearest = q; }
            }
            foreach (var c in _circles)
            {
                var center = new Vector2(c.x, c.y);
                var off = p - center;
                float d = Mathf.Max(0f, off.magnitude - c.z);
                if (d < best) { best = d; nearest = d <= 0f ? p : center + off.normalized * c.z; }
            }
            foreach (var cap in _capsules)
            {
                var ab = cap.b - cap.a;
                float t = Mathf.Clamp01(Vector2.Dot(p - cap.a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
                var axis = cap.a + ab * t;
                var off = p - axis;
                float d = Mathf.Max(0f, off.magnitude - cap.r);
                if (d < best) { best = d; nearest = d <= 0f ? p : axis + off.normalized * cap.r; }
            }
            return best;
        }

        private bool InsideShapes(float x, float z)
        {
            foreach (var r in _rects) if (x >= r.x && x <= r.y && z >= r.z && z <= r.w) return true;
            foreach (var c in _circles) if ((x - c.x) * (x - c.x) + (z - c.y) * (z - c.y) <= c.z * c.z) return true;
            var p = new Vector2(x, z);
            foreach (var cap in _capsules)
            {
                var ab = cap.b - cap.a;
                float t = Mathf.Clamp01(Vector2.Dot(p - cap.a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
                if ((cap.a + ab * t - p).sqrMagnitude <= cap.r * cap.r) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Where the player may go (D-038): anywhere on the land within reach of the paths and villages, except real
    /// barriers: water (bridges and piers excepted), ground too steep to climb or drop down, and the edge of the map.
    /// Paths, villages, bridges, piers and interiors are always fine. Buildings, trees, rocks, fences and people block
    /// with their own colliders.
    /// </summary>
    public sealed class RoamArea
    {
        /// <summary>Steepest ground you can walk up or down, in degrees.</summary>
        public const float MaxSlope = 34f;
        /// <summary>How far from the nearest path or village you can wander (the map is a small sandbox).</summary>
        public const float Reach = 40f;
        /// <summary>Kept clear of the terrain's edge.</summary>
        public const float EdgeMargin = 14f;
        /// <summary>A single step may not rise or drop more than this per metre (a ledge, not a slope).</summary>
        public const float MaxStepGradient = 1.4f;

        public RoamArea(WalkableArea paths) { Paths = paths; }

        /// <summary>The paths, villages, bridges and interiors: level ground, always walkable.</summary>
        public WalkableArea Paths { get; }

        public float Feet(float x, float z) => WorldGround.Feet(x, z, Paths);

        public bool CanStand(float x, float z)
        {
            if (Paths.Contains(x, z)) return true;
            if (x > WorldGround.InteriorThreshold) return false;
            var e = WorldGround.Extent;
            if (x < e.xMin + EdgeMargin || x > e.xMax - EdgeMargin || z < e.yMin + EdgeMargin || z > e.yMax - EdgeMargin) return false;
            if (WorldGround.WaterAt(x, z)) return false;
            if (Paths.Distance(x, z, out _) > Reach) return false;
            return WorldGround.SlopeDegrees(x, z, Paths) <= MaxSlope;
        }

        /// <summary>Can the player move from one spot to the next (no stepping off a ledge the slope test missed)?</summary>
        public bool CanStep(Vector3 from, Vector3 to)
        {
            if (!CanStand(to.x, to.z)) return false;
            if (Paths.Contains(to.x, to.z) && Paths.Contains(from.x, from.z)) return true;
            float run = new Vector2(to.x - from.x, to.z - from.z).magnitude;
            if (run < 0.0001f) return true;
            return Mathf.Abs(Feet(to.x, to.z) - Feet(from.x, from.z)) <= Mathf.Max(0.25f, run * MaxStepGradient);
        }
    }

    /// <summary>Walks the player over the land with the Move action; blocked by buildings, trees and the roam area.</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        public float Speed = 5.5f;
        public float SprintMultiplier = 1.8f;
        /// <summary>Take-off speed; with <see cref="Gravity"/> this gives about a 1.1 m hop.</summary>
        public float JumpSpeed = 7.5f;
        public float Gravity = 26f;
        public Transform Figure;
        [System.NonSerialized] public RoamArea Roam;
        private CharacterController _cc;
        private float _walkPhase;
        private float _yaw;
        private float _height;
        private float _vertical;

        /// <summary>Height above the ground while jumping.</summary>
        public float Height => _height;
        public bool Grounded => _height <= 0f && _vertical <= 0f;

        public bool Seated { get; private set; }
        /// <summary>Walking pace this frame (0 standing, 1 walking, ~1.8 sprinting); drives footsteps.</summary>
        public float LastSpeed { get; private set; }
        public float DistanceWalked { get; private set; }

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _cc.center = new Vector3(0, 0.9f, 0);
            _cc.height = 1.8f;
            _cc.radius = 0.4f;
            _yaw = transform.eulerAngles.y;
        }

        /// <summary>The animated body, when the player is a real character (D-033); null for the placeholder figure.</summary>
        [System.NonSerialized] public CharacterRig Rig;

        public void Teleport(Vector3 pos, float yaw)
        {
            _cc.enabled = false;
            transform.position = new Vector3(pos.x, Ground(pos.x, pos.z), pos.z);
            _height = 0;
            _vertical = 0;
            _yaw = yaw;
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            _cc.enabled = true;
        }

        private float Ground(float x, float z) => Roam != null ? Roam.Feet(x, z) : WorldGround.Walk(x, z);

        private void SetPosition(Vector3 p)
        {
            _cc.enabled = false;
            transform.position = p;
            _cc.enabled = true;
        }

        public void SetSeated(bool seated, Vector3 seat, float yaw)
        {
            Seated = seated;
            if (seated)
            {
                Teleport(seat, yaw);
                if (Figure != null) Figure.localPosition = Rig != null ? Vector3.zero : new Vector3(0, -0.35f, 0);
            }
            else if (Figure != null) Figure.localPosition = Vector3.zero;
            if (Rig != null) Rig.Sit(seated);
        }

        /// <summary>
        /// Move by input (x = east, y = north) for this frame. <paramref name="faceYaw"/> keeps the body facing the
        /// camera (first person); otherwise it turns toward the direction of travel.
        /// </summary>
        public void Step(Vector2 input, float dt, bool sprint = false, bool jump = false, float? faceYaw = null)
        {
            if (Seated) return;
            if (input.sqrMagnitude > 1f) input.Normalize();
            if (jump && Grounded) _vertical = JumpSpeed;
            if (!Grounded)
            {
                _vertical -= Gravity * dt;
                _height += _vertical * dt;
                if (_height <= 0f) { _height = 0f; _vertical = 0f; }
            }
            if (faceYaw.HasValue)
            {
                _yaw = faceYaw.Value;
                transform.rotation = Quaternion.Euler(0, _yaw, 0);
            }
            var delta = new Vector3(input.x, 0, input.y) * Speed * (sprint ? SprintMultiplier : 1f) * dt;
            if (delta.sqrMagnitude > 0.000001f)
            {
                var before = transform.position;
                before.y = 0;
                _cc.Move(delta);
                var p = transform.position;
                p.y = 0;
                if (Roam != null && !Roam.CanStep(before, p))
                {
                    // Slide along whichever axis can still be walked.
                    var tryX = new Vector3(p.x, 0, before.z);
                    var tryZ = new Vector3(before.x, 0, p.z);
                    if (Roam.CanStep(before, tryX)) p = tryX;
                    else if (Roam.CanStep(before, tryZ)) p = tryZ;
                    else p = new Vector3(before.x, 0, before.z);
                }
                DistanceWalked += (p - before).magnitude;
                p.y = Ground(p.x, p.z) + _height;
                SetPosition(p);
                if (!faceYaw.HasValue)
                {
                    _yaw = Mathf.MoveTowardsAngle(_yaw, Mathf.Atan2(input.x, input.y) * Mathf.Rad2Deg, 720f * dt);
                    transform.rotation = Quaternion.Euler(0, _yaw, 0);
                }
                _walkPhase = Grounded ? _walkPhase + dt * (sprint ? 16f : 11f) : 0f;
            }
            else
            {
                _walkPhase = 0;
                float y = Ground(transform.position.x, transform.position.z) + _height;
                if (!Mathf.Approximately(transform.position.y, y))
                    SetPosition(new Vector3(transform.position.x, y, transform.position.z));
            }
            float speed = delta.sqrMagnitude > 0.000001f && dt > 0 ? input.magnitude * (sprint ? SprintMultiplier : 1f) : 0f;
            LastSpeed = speed;
            if (Rig != null)
            {
                Rig.SetMove(speed, !Grounded);
                if (Figure != null && !Seated) Figure.localPosition = Vector3.zero;
            }
            else if (Figure != null && !Seated)
                Figure.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(_walkPhase)) * 0.12f, 0);
        }
    }
}
