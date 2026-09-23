using System.Collections.Generic;
using Tabletop.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace Tabletop.Presentation
{
    /// <summary>
    /// Placeholder 3D table built from Unity primitives and simple procedural meshes (M1 readability pass).
    /// Purely visual: it reads the presenter's visual state and animates the current event.
    /// </summary>
    public sealed class BoardDiorama : MonoBehaviour
    {
        private sealed class FloatingText
        {
            public Transform Root;
            public Text Label;
            public Vector3 Start;
            public float Age;
        }

        private readonly Transform[,] _units = new Transform[2, 2];
        private readonly List<Renderer>[,] _rankParts = new List<Renderer>[2, 2];
        private readonly Renderer[,,] _rodPips = new Renderer[2, 2, 5];
        private readonly Renderer[,] _podiumRings = new Renderer[2, 2];
        private readonly Text[,] _unitLabels = new Text[2, 2];
        private readonly GameObject[,] _wallLayers = new GameObject[2, 5];
        private readonly Transform[] _crowns = new Transform[2];
        private readonly List<Renderer>[] _crownParts = { new List<Renderer>(), new List<Renderer>() };
        private readonly Text[] _crownLabels = new Text[2];
        private readonly Text[] _wallLabels = new Text[2];
        private readonly List<FloatingText> _floating = new List<FloatingText>();
        private readonly List<Transform> _billboards = new List<Transform>();
        private Transform _projectile;
        private Renderer _projectileRenderer;
        private Transform _root;
        private Camera _camera;
        private Material _material;
        private Vector3 _shake;
        private string[,] _shapeFor = new string[2, 2];

        public const float SegmentHeight = 0.36f;
        public Camera Camera { get => _camera; set => _camera = value; }

        /// <summary>
        /// Material asset for every piece. Required in players: runtime CreatePrimitive falls back to the
        /// built-in Standard material there, which URP cannot draw (renders magenta).
        /// </summary>
        public Material Material { get => _material; set => _material = value; }

        public static Vector3 UnitPos(int side, int slot) => new Vector3(slot == 0 ? -3.3f : 3.3f, 0f, side == 0 ? -1.9f : 1.9f);
        public static Vector3 CrownPos(int side) => new Vector3(0, 0, side == 0 ? -2.7f : 2.7f);
        public static float WallZ(int side) => side == 0 ? -1.35f : 1.35f;
        /// <summary>World Y of an attack height (height 1 = first wall layer, 6 = above a full wall).</summary>
        public static float HeightY(int height) => 0.05f + (height - 0.5f) * SegmentHeight;

        private static Color Side(int side) => side == 0 ? Theme.Player : Theme.Enemy;

        public void Build()
        {
            _root = new GameObject("Diorama").transform;
            _root.SetParent(transform, false);

            Prim(PrimitiveType.Cube, "Table", _root, new Vector3(0, -0.15f, 0), new Vector3(10.5f, 0.3f, 7.4f), new Color(0.42f, 0.27f, 0.17f));
            Prim(PrimitiveType.Cube, "TableRim", _root, new Vector3(0, -0.2f, 0), new Vector3(10.9f, 0.3f, 7.8f), new Color(0.25f, 0.15f, 0.09f));
            Prim(PrimitiveType.Cube, "ActionLane", _root, new Vector3(0, 0.005f, 0), new Vector3(1.8f, 0.02f, 4.2f), new Color(0.5f, 0.34f, 0.22f));
            for (int side = 0; side < 2; side++)
            {
                float z = side == 0 ? -3.55f : 3.55f;
                Prim(PrimitiveType.Cube, "SideStrip" + side, _root, new Vector3(0, 0.01f, z), new Vector3(10.2f, 0.03f, 0.25f), Side(side));
                BuildCrown(side);
                BuildWall(side);
                for (int slot = 0; slot < 2; slot++) BuildPodium(side, slot);
            }
            _projectile = Prim(PrimitiveType.Sphere, "Projectile", _root, new Vector3(0, -5, 0), Vector3.one * 0.45f, Color.white).transform;
            _projectileRenderer = _projectile.GetComponent<Renderer>();
            _projectile.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ construction

        private void BuildCrown(int side)
        {
            var crown = new GameObject("Crown" + side).transform;
            crown.SetParent(_root, false);
            crown.localPosition = CrownPos(side);
            _crowns[side] = crown;
            var gold = Theme.Crown;
            _crownParts[side].Add(Prim(PrimitiveType.Cylinder, "Band", crown, new Vector3(0, 0.2f, 0), new Vector3(1.0f, 0.2f, 1.0f), gold).GetComponent<Renderer>());
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f + Mathf.PI / 2f;
                var p = new Vector3(Mathf.Cos(a) * 0.4f, 0.38f, Mathf.Sin(a) * 0.4f);
                _crownParts[side].Add(Cone("Spike" + i, crown, p, new Vector3(0.28f, 0.45f, 0.28f), gold).GetComponent<Renderer>());
                Prim(PrimitiveType.Sphere, "Gem" + i, crown, p + new Vector3(0, 0.47f, 0), Vector3.one * 0.12f, new Color(0.9f, 0.2f, 0.3f));
            }
            _crownLabels[side] = WorldLabel("CrownLabel" + side, crown, new Vector3(0, 1.25f, 0), 72, Theme.Crown);
        }

        private void BuildWall(int side)
        {
            var wall = new GameObject("Wall" + side).transform;
            wall.SetParent(_root, false);
            wall.localPosition = new Vector3(0, 0, WallZ(side));
            for (int layer = 0; layer < 5; layer++)
            {
                var row = new GameObject("Layer" + layer).transform;
                row.SetParent(wall, false);
                for (int b = -1; b <= 1; b++)
                {
                    float x = b * 0.84f + (layer % 2 == 0 ? 0f : 0.2f);
                    float shade = 0.55f + 0.08f * ((layer + b + 3) % 3);
                    Prim(PrimitiveType.Cube, "Brick", row, new Vector3(x, 0.05f + (layer + 0.5f) * SegmentHeight, 0),
                        new Vector3(0.8f, SegmentHeight * 0.88f, 0.34f), new Color(shade, shade, shade + 0.05f));
                }
                _wallLayers[side, layer] = row.gameObject;
            }
            _wallLabels[side] = WorldLabel("WallLabel" + side, wall, new Vector3(1.55f, 0.35f, 0), 34, new Color(0.85f, 0.87f, 0.95f));
        }

        private void BuildPodium(int side, int slot)
        {
            var unit = new GameObject("Unit" + side + slot).transform;
            unit.SetParent(_root, false);
            unit.localPosition = UnitPos(side, slot);
            // Face the opponent.
            unit.localRotation = Quaternion.Euler(0, side == 0 ? 0 : 180, 0);
            _units[side, slot] = unit;
            var channel = slot == 0 ? Theme.ChannelA : Theme.ChannelB;
            _podiumRings[side, slot] = Prim(PrimitiveType.Cylinder, "Podium", unit, new Vector3(0, 0.05f, 0), new Vector3(1.5f, 0.05f, 1.5f), channel).GetComponent<Renderer>();
            Prim(PrimitiveType.Cylinder, "PodiumTop", unit, new Vector3(0, 0.08f, 0), new Vector3(1.25f, 0.05f, 1.25f), new Color(0.2f, 0.16f, 0.14f));
            // Action rod: one pip per energy point, lit when stored.
            // Rod sits on the outer side of the podium (world -x for left units, +x for right units).
            float rodX = slot == 0 ? -1.0f : 1.0f;
            if (side == 1) rodX = -rodX; // enemy podiums are rotated 180 degrees
            for (int i = 0; i < 5; i++)
                _rodPips[side, slot, i] = Prim(PrimitiveType.Cube, "RodPip" + i, unit, new Vector3(rodX, 0.18f + i * 0.26f, 0), new Vector3(0.22f, 0.2f, 0.22f), channel).GetComponent<Renderer>();
            _unitLabels[side, slot] = WorldLabel("UnitLabel" + side + slot, unit, new Vector3(0, 2.35f, 0), 30, Side(side));
            _rankParts[side, slot] = new List<Renderer>();
        }

        /// <summary>Builds the unit figure: Striker = knight with sword and shield, Caster = wizard with hat and staff.</summary>
        public void SetUnitShape(int side, int slot, UnitDefinition def)
        {
            if (_shapeFor[side, slot] == def.Id) return;
            _shapeFor[side, slot] = def.Id;
            var unit = _units[side, slot];
            var old = unit.Find("Figure");
            if (old != null) Destroy(old.gameObject);
            var fig = new GameObject("Figure").transform;
            fig.SetParent(unit, false);
            var parts = _rankParts[side, slot];
            parts.Clear();
            var steel = new Color(0.78f, 0.8f, 0.86f);
            var skin = new Color(0.95f, 0.8f, 0.65f);
            Color rank = Theme.Rank(Rank.Bronze);
            if (def.Id == ReferenceContent.Striker)
            {
                parts.Add(Prim(PrimitiveType.Capsule, "Body", fig, new Vector3(0, 0.72f, 0), new Vector3(0.62f, 0.55f, 0.5f), rank).GetComponent<Renderer>());
                Prim(PrimitiveType.Sphere, "Head", fig, new Vector3(0, 1.42f, 0), Vector3.one * 0.42f, steel);
                Prim(PrimitiveType.Cube, "Visor", fig, new Vector3(0, 1.42f, 0.2f), new Vector3(0.3f, 0.06f, 0.06f), new Color(0.1f, 0.1f, 0.12f));
                parts.Add(Cone("Plume", fig, new Vector3(0, 1.58f, 0), new Vector3(0.16f, 0.35f, 0.16f), rank).GetComponent<Renderer>());
                var shield = Prim(PrimitiveType.Cylinder, "Shield", fig, new Vector3(-0.42f, 0.85f, 0.2f), new Vector3(0.62f, 0.04f, 0.62f), Side(side));
                shield.transform.localRotation = Quaternion.Euler(90, 0, 0);
                var sword = new GameObject("Sword").transform;
                sword.SetParent(fig, false);
                sword.localPosition = new Vector3(0.45f, 0.9f, 0.15f);
                sword.localRotation = Quaternion.Euler(0, 0, -20);
                Prim(PrimitiveType.Cube, "Blade", sword, new Vector3(0, 0.45f, 0), new Vector3(0.1f, 0.95f, 0.04f), steel);
                Prim(PrimitiveType.Cube, "Guard", sword, new Vector3(0, -0.02f, 0), new Vector3(0.36f, 0.07f, 0.08f), new Color(0.85f, 0.7f, 0.25f));
                Prim(PrimitiveType.Cube, "Grip", sword, new Vector3(0, -0.16f, 0), new Vector3(0.07f, 0.22f, 0.07f), new Color(0.4f, 0.25f, 0.12f));
            }
            else if (def.Id == ReferenceContent.Caster)
            {
                parts.Add(Cone("Robe", fig, new Vector3(0, 0.1f, 0), new Vector3(0.95f, 1.25f, 0.95f), rank).GetComponent<Renderer>());
                Prim(PrimitiveType.Sphere, "Head", fig, new Vector3(0, 1.3f, 0), Vector3.one * 0.38f, skin);
                Prim(PrimitiveType.Cube, "Beard", fig, new Vector3(0, 1.15f, 0.14f), new Vector3(0.22f, 0.22f, 0.08f), new Color(0.92f, 0.92f, 0.95f));
                parts.Add(Prim(PrimitiveType.Cylinder, "Brim", fig, new Vector3(0, 1.46f, 0), new Vector3(0.78f, 0.025f, 0.78f), rank).GetComponent<Renderer>());
                parts.Add(Cone("Hat", fig, new Vector3(0, 1.47f, 0), new Vector3(0.48f, 0.85f, 0.48f), rank).GetComponent<Renderer>());
                Prim(PrimitiveType.Cylinder, "Staff", fig, new Vector3(0.5f, 0.95f, 0.1f), new Vector3(0.07f, 0.85f, 0.07f), new Color(0.5f, 0.32f, 0.16f));
                Prim(PrimitiveType.Sphere, "Orb", fig, new Vector3(0.5f, 1.88f, 0.1f), Vector3.one * 0.26f, Side(side));
            }
            else
            {
                parts.Add(Prim(PrimitiveType.Capsule, "Body", fig, new Vector3(0, 0.75f, 0), new Vector3(0.6f, 0.6f, 0.6f), rank).GetComponent<Renderer>());
                Prim(PrimitiveType.Sphere, "Head", fig, new Vector3(0, 1.5f, 0), Vector3.one * 0.4f, skin);
                Prim(PrimitiveType.Cube, "Badge", fig, new Vector3(0, 0.9f, 0.3f), new Vector3(0.3f, 0.3f, 0.05f), Side(side));
            }
        }

        // ------------------------------------------------------------------ per frame

        public void Render(VisualState v, Match match, MatchEvent current, float progress, bool applied, bool reducedMotion)
        {
            if (_root == null || match == null) return;
            for (int side = 0; side < 2; side++)
            {
                for (int i = 0; i < 5; i++) _wallLayers[side, i].SetActive(i < v.Barrier[side]);
                _wallLabels[side].text = v.Barrier[side] > 0 ? "WALL " + v.Barrier[side] : "";
                int hp = v.Crown[side];
                _crownLabels[side].text = hp.ToString();
                bool broken = hp <= 0;
                foreach (var r in _crownParts[side]) SetColor(r, broken ? new Color(0.35f, 0.33f, 0.3f) : Theme.Crown);
                _crowns[side].localRotation = broken ? Quaternion.Euler(side == 0 ? 60 : -60, 0, 20) : Quaternion.identity;
                for (int slot = 0; slot < 2; slot++)
                {
                    var def = match.UnitDefinition((SideId)side, slot);
                    var rankColor = Theme.Rank(v.Rank[side, slot]);
                    foreach (var r in _rankParts[side, slot]) SetColor(r, rankColor);
                    int cost = def.Stats(v.Rank[side, slot]).EnergyCost;
                    var channel = slot == 0 ? Theme.ChannelA : Theme.ChannelB;
                    for (int i = 0; i < 5; i++)
                    {
                        _rodPips[side, slot, i].gameObject.SetActive(i < cost);
                        SetColor(_rodPips[side, slot, i], i < v.Energy[side, slot] ? channel : new Color(0.18f, 0.16f, 0.15f));
                    }
                    bool ready = v.Energy[side, slot] >= cost;
                    _unitLabels[side, slot].text = def.DisplayName.ToUpperInvariant() + (ready ? "  READY!" : "  " + v.Energy[side, slot] + "/" + cost);
                    bool acting = current != null && current.Side == side && current.Slot == slot && current.Stage > 0 && current.Type != MatchEventType.RoundEnded;
                    var fig = _units[side, slot].Find("Figure");
                    float lift = acting && !reducedMotion ? 0.35f * Mathf.Sin(progress * Mathf.PI) : 0f;
                    if (fig != null) fig.localPosition = new Vector3(0, lift, 0);
                    SetColor(_podiumRings[side, slot], acting ? Color.white : channel);
                }
            }
            RenderProjectile(current, progress, reducedMotion);
            UpdateFloating(reducedMotion);
            if (_camera != null)
                foreach (var b in _billboards) b.rotation = _camera.transform.rotation;
            transform.localPosition = _shake;
        }

        private void RenderProjectile(MatchEvent e, float t, bool reducedMotion)
        {
            bool show = e != null && (e.Type == MatchEventType.ProjectileResolved || e.Type == MatchEventType.BombLaunched);
            _projectile.gameObject.SetActive(show);
            if (!show) return;
            Vector3 from = UnitPos(e.Side, e.Slot) + new Vector3(0, 1.1f, 0);
            Vector3 to;
            if (e.Type == MatchEventType.BombLaunched)
            {
                to = CrownPos(e.TargetSide) + new Vector3(0, 0.5f, 0);
                SetColor(_projectileRenderer, new Color(0.08f, 0.08f, 0.1f));
            }
            else if (e.TargetIsCrown)
            {
                to = CrownPos(e.TargetSide) + new Vector3(0, 0.5f, 0);
                from.y = HeightY(e.Height);
                SetColor(_projectileRenderer, Side(e.Side));
            }
            else
            {
                to = new Vector3(0, HeightY(e.Height), WallZ(e.TargetSide) + (e.TargetSide == 0 ? 0.3f : -0.3f));
                from.y = HeightY(e.Height);
                SetColor(_projectileRenderer, Side(e.Side));
            }
            if (reducedMotion)
            {
                _projectile.localPosition = to;
                _projectile.localScale = Vector3.one * 0.45f * Mathf.Clamp01(t * 2f);
                return;
            }
            var p = Vector3.Lerp(from, to, t);
            float arc = e.Type == MatchEventType.BombLaunched ? 3.2f : (e.Height >= 6 ? 0.6f : 0.15f);
            p.y += arc * Mathf.Sin(t * Mathf.PI);
            _projectile.localPosition = p;
            _projectile.localScale = Vector3.one * 0.45f;
        }

        // ------------------------------------------------------------------ floating deltas

        /// <summary>Pops a rising number/word at the thing that changed (called at an event's impact point).</summary>
        public void ShowDelta(MatchEvent e)
        {
            string text = null;
            Color color = Color.white;
            Vector3 at = Vector3.zero;
            switch (e.Type)
            {
                case MatchEventType.CrownDamaged:
                    text = "-" + e.Amount; color = Theme.Damage; at = CrownPos(e.TargetSide) + new Vector3(0, 1.0f, 0); break;
                case MatchEventType.CrownHealed:
                    text = "+" + e.Amount; color = Theme.Heal; at = CrownPos(e.Side) + new Vector3(0, 1.0f, 0); break;
                case MatchEventType.BarrierDamaged:
                    text = "WALL -" + e.Amount; color = new Color(0.8f, 0.85f, 1f); at = new Vector3(0, 2.0f, WallZ(e.TargetSide)); break;
                case MatchEventType.BarrierBuilt:
                    text = "WALL +" + e.Amount; color = new Color(0.8f, 0.85f, 1f); at = new Vector3(0, 2.0f, WallZ(e.Side)); break;
                case MatchEventType.EnergyGranted:
                    text = "+" + e.Amount + " ENERGY"; color = e.Slot == 0 ? Theme.ChannelA : Theme.ChannelB; at = UnitPos(e.Side, e.Slot) + new Vector3(0, 2.0f, 0); break;
                case MatchEventType.EnergyDelayed:
                    text = "-" + e.Amount + " ENERGY"; color = Theme.Damage; at = UnitPos(e.TargetSide, e.TargetSlot) + new Vector3(0, 2.0f, 0); break;
                case MatchEventType.PanelXpGranted:
                case MatchEventType.ActionXpGranted:
                    text = "+" + e.Amount + " XP"; color = Theme.Xp; at = UnitPos(e.Side, e.Slot) + new Vector3(0, 2.6f, 0); break;
                case MatchEventType.UnitRankedUp:
                    text = "RANK UP!"; color = Theme.Crown; at = UnitPos(e.Side, e.Slot) + new Vector3(0, 2.8f, 0); break;
                case MatchEventType.BombQueued:
                    text = "BOMB ARMED!"; color = Theme.Crown; at = UnitPos(e.Side, e.Slot) + new Vector3(0, 2.8f, 0); break;
            }
            if (text == null || (e.Amount == 0 && e.Type != MatchEventType.UnitRankedUp && e.Type != MatchEventType.BombQueued)) return;
            var label = WorldLabel("Delta", _root, at, 56, color);
            label.text = text;
            _floating.Add(new FloatingText { Root = label.transform.parent, Label = label, Start = at, Age = 0 });
        }

        private void UpdateFloating(bool reducedMotion)
        {
            for (int i = _floating.Count - 1; i >= 0; i--)
            {
                var f = _floating[i];
                f.Age += Time.unscaledDeltaTime;
                float life = 1.3f;
                if (f.Age >= life)
                {
                    _billboards.Remove(f.Root);
                    Destroy(f.Root.gameObject);
                    _floating.RemoveAt(i);
                    continue;
                }
                f.Root.localPosition = f.Start + (reducedMotion ? Vector3.zero : new Vector3(0, 0.7f * f.Age, 0));
                var c = f.Label.color;
                c.a = Mathf.Clamp01(1.6f * (1f - f.Age / life));
                f.Label.color = c;
            }
        }

        public void ClearFloating()
        {
            foreach (var f in _floating) { _billboards.Remove(f.Root); Destroy(f.Root.gameObject); }
            _floating.Clear();
        }

        public void Shake(float amount) => _shake = Random.insideUnitSphere * amount;
        public void StopShake() => _shake = Vector3.zero;

        // ------------------------------------------------------------------ helpers

        /// <summary>Camera-facing world-space text (placeholder labels; always readable over the table).</summary>
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
            rt.localScale = Vector3.one * 0.008f;
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

        private GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<Renderer>();
            if (_material != null) renderer.sharedMaterial = _material;
            SetColor(renderer, color);
            return go;
        }

        private GameObject Cone(string name, Transform parent, Vector3 pos, Vector3 scale, Color color)
        {
            // Borrow a primitive's renderer/material setup, then swap in the cone mesh.
            var go = Prim(PrimitiveType.Cylinder, name, parent, pos, scale, color);
            go.GetComponent<MeshFilter>().sharedMesh = MeshFactory.Cone;
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
