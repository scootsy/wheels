using Tabletop.Domain;
using UnityEngine;

namespace Tabletop.Presentation
{
    /// <summary>
    /// Placeholder 3D table built from Unity primitives (M1). Purely visual: it reads the presenter's
    /// visual state and animates the current event. It never touches rules state.
    /// </summary>
    public sealed class BoardDiorama : MonoBehaviour
    {
        private readonly Transform[,] _units = new Transform[2, 2];
        private readonly Renderer[,] _unitBodies = new Renderer[2, 2];
        private readonly Transform[,] _rods = new Transform[2, 2];
        private readonly GameObject[,] _barrierSegments = new GameObject[2, 5];
        private readonly Transform[] _crowns = new Transform[2];
        private readonly Renderer[] _crownRenderers = new Renderer[2];
        private Transform _projectile;
        private Renderer _projectileRenderer;
        private Transform _root;
        private Vector3 _shake;

        public const float SegmentHeight = 0.42f;

        public static Vector3 UnitPos(int side, int slot) => new Vector3(slot == 0 ? -4.2f : 4.2f, 0f, side == 0 ? -1.7f : 1.7f);
        public static Vector3 CrownPos(int side) => new Vector3(0, 0, side == 0 ? -2.6f : 2.6f);
        public static float BarrierZ(int side) => side == 0 ? -1.2f : 1.2f;
        public static float HeightY(int height) => 0.2f + (height - 0.5f) * SegmentHeight;

        public void Build()
        {
            _root = new GameObject("Diorama").transform;
            _root.SetParent(transform, false);

            var table = Prim(PrimitiveType.Cube, "Table", new Vector3(0, -0.15f, 0), new Vector3(12f, 0.3f, 7f), new Color(0.35f, 0.22f, 0.15f));
            var lane = Prim(PrimitiveType.Cube, "ActionLane", new Vector3(0, 0.01f, 0), new Vector3(7f, 0.02f, 1.6f), new Color(0.45f, 0.30f, 0.20f));
            for (int side = 0; side < 2; side++)
            {
                var crown = Prim(PrimitiveType.Cylinder, "Crown" + side, CrownPos(side) + new Vector3(0, 0.35f, 0), new Vector3(1.1f, 0.35f, 1.1f), Theme.Crown);
                _crowns[side] = crown.transform;
                _crownRenderers[side] = crown.GetComponent<Renderer>();
                var marker = Prim(PrimitiveType.Cube, "SideMarker" + side, CrownPos(side) + new Vector3(0, 0.05f, side == 0 ? -0.8f : 0.8f), new Vector3(2.4f, 0.1f, 0.25f), side == 0 ? Theme.Player : Theme.Enemy);
                for (int i = 0; i < 5; i++)
                {
                    _barrierSegments[side, i] = Prim(PrimitiveType.Cube, "Barrier" + side + "_" + i,
                        new Vector3(0, 0.2f + i * SegmentHeight, BarrierZ(side)), new Vector3(2.8f, SegmentHeight * 0.9f, 0.3f), Theme.Barrier);
                }
                for (int slot = 0; slot < 2; slot++)
                {
                    var unit = new GameObject("Unit" + side + slot).transform;
                    unit.SetParent(_root, false);
                    unit.localPosition = UnitPos(side, slot);
                    _units[side, slot] = unit;
                    var pad = Prim(PrimitiveType.Cylinder, "Podium", Vector3.zero, new Vector3(1.4f, 0.08f, 1.4f), slot == 0 ? Theme.ChannelA : Theme.ChannelB);
                    pad.transform.SetParent(unit, false);
                    var rod = Prim(PrimitiveType.Cube, "ActionRod", new Vector3(slot == 0 ? 0.95f : -0.95f, 0.5f, 0), new Vector3(0.18f, 1f, 0.18f), slot == 0 ? Theme.ChannelA : Theme.ChannelB);
                    rod.transform.SetParent(unit, false);
                    _rods[side, slot] = rod.transform;
                }
            }
            _projectile = Prim(PrimitiveType.Sphere, "Projectile", new Vector3(0, -5, 0), Vector3.one * 0.38f, Color.white).transform;
            _projectileRenderer = _projectile.GetComponent<Renderer>();
            _projectile.gameObject.SetActive(false);
        }

        /// <summary>Builds or rebuilds the unit figure for a definition: Striker = block, Caster = cone-hat, others = capsule.</summary>
        public void SetUnitShape(int side, int slot, UnitDefinition def)
        {
            var unit = _units[side, slot];
            var old = unit.Find("Body");
            if (old != null) Destroy(old.gameObject);
            var body = new GameObject("Body").transform;
            body.SetParent(unit, false);
            GameObject main;
            if (def.Id == ReferenceContent.Striker)
            {
                main = Prim(PrimitiveType.Cube, "Torso", new Vector3(0, 0.75f, 0), new Vector3(0.7f, 1.2f, 0.5f), Color.white);
                var blade = Prim(PrimitiveType.Cube, "Blade", new Vector3(0.5f, 1.0f, 0), new Vector3(0.12f, 1.1f, 0.12f), Color.white);
                blade.transform.SetParent(body, false);
            }
            else if (def.Id == ReferenceContent.Caster)
            {
                main = Prim(PrimitiveType.Cylinder, "Robe", new Vector3(0, 0.6f, 0), new Vector3(0.7f, 0.55f, 0.7f), Color.white);
                var head = Prim(PrimitiveType.Sphere, "Head", new Vector3(0, 1.35f, 0), Vector3.one * 0.45f, Color.white);
                var hat = Prim(PrimitiveType.Cube, "HatBrim", new Vector3(0, 1.6f, 0), new Vector3(0.9f, 0.08f, 0.9f), Color.white);
                head.transform.SetParent(body, false);
                hat.transform.SetParent(body, false);
            }
            else
            {
                main = Prim(PrimitiveType.Capsule, "Body", new Vector3(0, 0.8f, 0), new Vector3(0.6f, 0.7f, 0.6f), Color.white);
            }
            main.transform.SetParent(body, false);
            _unitBodies[side, slot] = main.GetComponent<Renderer>();
            foreach (var r in body.GetComponentsInChildren<Renderer>()) r.gameObject.name += "_Body";
        }

        public void Render(VisualState v, Match match, MatchEvent current, float progress, bool applied, bool reducedMotion)
        {
            if (_root == null || match == null) return;
            for (int side = 0; side < 2; side++)
            {
                for (int i = 0; i < 5; i++) _barrierSegments[side, i].SetActive(i < v.Barrier[side]);
                float hpScale = Mathf.Clamp(v.Crown[side], 0, 12) / 10f;
                _crowns[side].localScale = new Vector3(1.1f, 0.05f + 0.35f * hpScale, 1.1f);
                _crowns[side].localPosition = CrownPos(side) + new Vector3(0, 0.05f + 0.35f * hpScale, 0);
                SetColor(_crownRenderers[side], v.Crown[side] <= 0 ? new Color(0.3f, 0.3f, 0.3f) : Theme.Crown);
                for (int slot = 0; slot < 2; slot++)
                {
                    var def = match.UnitDefinition((SideId)side, slot);
                    var body = _units[side, slot].Find("Body");
                    foreach (var r in body.GetComponentsInChildren<Renderer>()) SetColor(r, Theme.Rank(v.Rank[side, slot]));
                    int cost = def.Stats(v.Rank[side, slot]).EnergyCost;
                    float fill = cost <= 0 ? 0 : Mathf.Clamp01(v.Energy[side, slot] / (float)cost);
                    _rods[side, slot].localScale = new Vector3(0.18f, 0.05f + 1.4f * fill, 0.18f);
                    _rods[side, slot].localPosition = new Vector3(slot == 0 ? 0.95f : -0.95f, (0.05f + 1.4f * fill) / 2f, 0);
                    bool acting = current != null && current.Side == side && current.Slot == slot && current.Stage > 0 && current.Type != MatchEventType.RoundEnded;
                    float lift = acting && !reducedMotion ? 0.35f * Mathf.Sin(progress * Mathf.PI) : 0f;
                    body.localPosition = new Vector3(0, lift, 0);
                }
            }
            RenderProjectile(current, progress, reducedMotion);
            transform.localPosition = _shake;
        }

        private void RenderProjectile(MatchEvent e, float t, bool reducedMotion)
        {
            bool show = e != null && (e.Type == MatchEventType.ProjectileResolved || e.Type == MatchEventType.BombLaunched);
            _projectile.gameObject.SetActive(show);
            if (!show) return;
            Vector3 from = UnitPos(e.Side, e.Slot) + new Vector3(0, 1.0f, 0);
            Vector3 to;
            if (e.Type == MatchEventType.BombLaunched)
            {
                to = CrownPos(e.TargetSide) + new Vector3(0, 0.6f, 0);
                SetColor(_projectileRenderer, new Color(0.1f, 0.1f, 0.1f));
            }
            else if (e.TargetIsCrown)
            {
                to = CrownPos(e.TargetSide) + new Vector3(0, 0.6f, 0);
                from.y = HeightY(e.Height);
                SetColor(_projectileRenderer, e.Side == 0 ? Theme.Player : Theme.Enemy);
            }
            else
            {
                to = new Vector3(0, HeightY(e.Height), BarrierZ(e.TargetSide) + (e.TargetSide == 0 ? 0.3f : -0.3f));
                from.y = HeightY(e.Height);
                SetColor(_projectileRenderer, e.Side == 0 ? Theme.Player : Theme.Enemy);
            }
            if (reducedMotion)
            {
                // No travel: a short fade-in at the impact point.
                _projectile.localPosition = to;
                _projectile.localScale = Vector3.one * 0.38f * Mathf.Clamp01(t * 2f);
                return;
            }
            var p = Vector3.Lerp(from, to, t);
            // Height lane: the shot flies at its attack height; bombs arc high over every Barrier.
            float arc = e.Type == MatchEventType.BombLaunched ? 3.5f : (e.Height >= 6 ? 0.8f : 0.2f);
            p.y += arc * Mathf.Sin(t * Mathf.PI);
            _projectile.localPosition = p;
            _projectile.localScale = Vector3.one * 0.38f;
        }

        public void Shake(float amount) => _shake = Random.insideUnitSphere * amount;
        public void StopShake() => _shake = Vector3.zero;

        private GameObject Prim(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.SetParent(_root, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            SetColor(go.GetComponent<Renderer>(), color);
            return go;
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
