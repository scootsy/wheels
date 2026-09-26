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
            if (Encounter == null) { sub = (Title ?? "").ToUpperInvariant(); accent = Theme.TextDim; }
            else if (beaten) { sub = "BEATEN"; accent = new Color(0.55f, 0.8f, 0.55f); emblem = kit != null ? kit.uiDiamond : null; }
            else if (Encounter.IsChampion) { sub = "CHAMPION  -  CHALLENGE"; accent = Theme.Gilt; emblem = kit != null ? kit.crown : null; }
            else { sub = "CHALLENGER"; accent = Theme.Gilt; emblem = kit != null ? kit.uiDiamond : null; }
            Tag.Set(DisplayName, sub, emblem, accent);
            if (Marker != null) Marker.gameObject.SetActive(Encounter != null && !beaten);
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

        private void Update()
        {
            // Gentle idle breathing so people read as alive.
            if (Figure == null) return;
            _bobPhase += Time.deltaTime * 2f;
            Figure.localPosition = BaseOffset + new Vector3(0, Mathf.Abs(Mathf.Sin(_bobPhase)) * 0.03f, 0);
        }

        public override void Interact(WorldApp app) => app.TalkTo(this);
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
        public Vector3 SeatPosition;
        public override string Prompt => "Sit down at the table";
        public override void Interact(WorldApp app) => app.SitAt(this);
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

        public void Rect(float xmin, float xmax, float zmin, float zmax) => _rects.Add(new Vector4(xmin, xmax, zmin, zmax));
        public void Circle(float x, float z, float r) => _circles.Add(new Vector3(x, z, r));
        public void Capsule(Vector2 a, Vector2 b, float r) => _capsules.Add((a, b, r));

        public bool Contains(float x, float z)
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

    /// <summary>Walks the player on the ground plane with the Move action; blocked by buildings and the walkable area.</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        public float Speed = 5.5f;
        public float SprintMultiplier = 1.8f;
        /// <summary>Take-off speed; with <see cref="Gravity"/> this gives about a 1.1 m hop.</summary>
        public float JumpSpeed = 7.5f;
        public float Gravity = 26f;
        public Transform Figure;
        [System.NonSerialized] public WalkableArea Walkable;
        private CharacterController _cc;
        private float _walkPhase;
        private float _yaw;
        private float _height;
        private float _vertical;

        /// <summary>Height above the ground while jumping.</summary>
        public float Height => _height;
        public bool Grounded => _height <= 0f && _vertical <= 0f;

        public bool Seated { get; private set; }
        public float DistanceWalked { get; private set; }

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _cc.center = new Vector3(0, 0.9f, 0);
            _cc.height = 1.8f;
            _cc.radius = 0.4f;
            _yaw = transform.eulerAngles.y;
        }

        public void Teleport(Vector3 pos, float yaw)
        {
            _cc.enabled = false;
            transform.position = new Vector3(pos.x, 0, pos.z);
            _height = 0;
            _vertical = 0;
            _yaw = yaw;
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            _cc.enabled = true;
        }

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
                if (Figure != null) Figure.localPosition = new Vector3(0, -0.35f, 0);
            }
            else if (Figure != null) Figure.localPosition = Vector3.zero;
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
                if (Walkable != null && !Walkable.Contains(p.x, p.z))
                {
                    // Slide along whichever axis is still inside the walkable area.
                    var tryX = new Vector3(p.x, 0, before.z);
                    var tryZ = new Vector3(before.x, 0, p.z);
                    if (Walkable.Contains(tryX.x, tryX.z)) p = tryX;
                    else if (Walkable.Contains(tryZ.x, tryZ.z)) p = tryZ;
                    else p = new Vector3(before.x, 0, before.z);
                }
                DistanceWalked += (p - before).magnitude;
                p.y = _height;
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
                if (!Mathf.Approximately(transform.position.y, _height))
                    SetPosition(new Vector3(transform.position.x, _height, transform.position.z));
            }
            if (Figure != null && !Seated)
                Figure.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(_walkPhase)) * 0.12f, 0);
        }
    }
}
