using System;
using System.Collections.Generic;
using Tabletop.Application;
using Tabletop.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Tabletop.World
{
    /// <summary>
    /// The world map and the minimap (D-041). One map texture is painted from the built land when the world loads
    /// (<see cref="MapPainter"/>); the full map shows all of it with place names, and the minimap shows the land around
    /// the player in the top-left corner while exploring. Markers: you (an arrow facing your way), challengers,
    /// beaten players, champions, stalls, and people or things an errand needs. North is up.
    /// </summary>
    public sealed class WorldMap
    {
        public enum MarkerKind { Hidden, Challenger, Beaten, Champion, Stall, Errand }

        /// <summary>Metres of land the minimap shows across.</summary>
        public const float MiniMetresAcross = 110f;
        public const float MiniSize = 260f;
        public const int TextureWidth = 512;

        private sealed class Marker
        {
            public Func<Vector3> Position;
            public Func<MarkerKind> Kind;
            public Image Full;
            public Image Mini;
            public MarkerKind Shown = (MarkerKind)(-1);
        }

        public readonly GameObject FullRoot;
        public readonly GameObject MiniRoot;
        public readonly Button Close;
        public readonly Button MinimapToggle;
        /// <summary>Tapping or clicking the minimap opens the full map.</summary>
        public readonly TapTarget MiniTap;
        public Texture2D Texture { get; }

        private readonly WorldLayout _layout;
        private readonly IconSet _icons;
        private readonly Rect _extent = WorldGround.Extent;
        private readonly RectTransform _fullMap;
        private readonly RectTransform _miniContent;
        private readonly RectTransform _fullPlayer;
        private readonly RectTransform _miniPlayer;
        private readonly Text _where;
        private readonly List<Marker> _markers = new List<Marker>();
        private readonly Vector2 _fullSize;
        private readonly Vector2 _miniContentSize;
        private float _refresh;
        /// <summary>The land never changes, so the map is painted once per run, not on every return from a table.</summary>
        private static Texture2D _painted;

        public bool IsOpen => FullRoot.activeSelf;
        public bool MinimapShown => MiniRoot.activeSelf;
        /// <summary>Where the player's arrow sits on the full map (0..1 across the land), for tests.</summary>
        public Vector2 PlayerUv { get; private set; }
        public float PlayerArrowAngle => _fullPlayer.localEulerAngles.z;

        public WorldMap(Transform frame, WorldLayout layout, IconSet icons)
        {
            _layout = layout;
            _icons = icons;
            if (_painted == null) _painted = Paint(layout);
            Texture = _painted;
            var arrow = ArrowSprite();

            // ---- the minimap, top left: a framed window onto the map, centred on the player.
            var mini = Ui.Card("Minimap", frame, new Color(0.09f, 0.05f, 0.035f, 0.9f), true);
            mini.rectTransform.Place(40, 40, MiniSize + 20, MiniSize + 20);
            mini.raycastTarget = true;
            MiniRoot = mini.gameObject;
            MiniTap = MiniRoot.AddComponent<TapTarget>();
            var window = Ui.Rect("Window", mini.transform);
            window.Fill(10);
            window.gameObject.AddComponent<RectMask2D>();
            float scale = MiniSize / MiniMetresAcross;
            _miniContentSize = new Vector2(_extent.width * scale, _extent.height * scale);
            _miniContent = MapImage("Land", window, _miniContentSize);
            _miniContent.anchorMin = _miniContent.anchorMax = new Vector2(0.5f, 0.5f);
            _miniPlayer = ArrowImage(window, arrow, 30f);
            _miniPlayer.anchorMin = _miniPlayer.anchorMax = new Vector2(0.5f, 0.5f);
            _miniPlayer.anchoredPosition = Vector2.zero;
            var north = Ui.Label("North", mini.transform, "N", 20, TextAnchor.UpperCenter, Theme.Gilt, FontStyle.Bold);
            north.rectTransform.Place((MiniSize + 20) / 2f - 20, 10, 40, 26);
            north.gameObject.AddComponent<Outline>().effectColor = new Color(0.1f, 0.05f, 0.02f, 0.9f);
            MiniRoot.SetActive(false);

            // ---- the full map: the whole land on the left, the key and buttons on the right.
            var full = Ui.Panel("MapScreen", frame, Theme.Overlay);
            full.rectTransform.Fill();
            full.raycastTarget = true;
            FullRoot = full.gameObject;
            var box = Ui.Card("Box", full.transform, Theme.Panel, true);
            box.rectTransform.Place(230, 30, 1460, 1020);
            float mapH = 960f;
            _fullSize = new Vector2(mapH * _extent.width / _extent.height, mapH);
            var holder = Ui.Rect("MapHolder", box.transform);
            holder.Place(30, 30, _fullSize.x, _fullSize.y);
            _fullMap = MapImage("Land", holder, _fullSize);
            _fullMap.anchorMin = _fullMap.anchorMax = Vector2.zero;
            _fullMap.anchoredPosition = Vector2.zero;
            foreach (var (name, at) in Places) PlaceLabel(name, at);
            _fullPlayer = ArrowImage(_fullMap, arrow, 38f);
            _fullPlayer.anchorMin = _fullPlayer.anchorMax = Vector2.zero;

            float rx = _fullSize.x + 70f, rw = 1460f - rx - 40f;
            Ui.Label("Title", box.transform, "MAP", 52, TextAnchor.UpperLeft, Theme.Gilt, FontStyle.Bold).rectTransform.Place(rx, 34, rw, 64);
            _where = Ui.Label("Where", box.transform, "", 26, TextAnchor.UpperLeft, Theme.Text, FontStyle.Bold);
            _where.rectTransform.Place(rx, 104, rw, 70);
            float y = 200f;
            Key(box.transform, rx, ref y, null, arrow, Theme.Gilt, "You");
            Key(box.transform, rx, ref y, MarkerKind.Challenger, null, Color.white, "Plays Wheels: challenge them");
            Key(box.transform, rx, ref y, MarkerKind.Champion, null, Color.white, "A champion, or your next match");
            Key(box.transform, rx, ref y, MarkerKind.Beaten, null, Color.white, "Already beaten");
            Key(box.transform, rx, ref y, MarkerKind.Stall, null, Color.white, "A stall");
            Key(box.transform, rx, ref y, MarkerKind.Errand, null, Color.white, "An errand: work, a delivery, or an item");
            MinimapToggle = Ui.Button("MinimapToggle", box.transform, "MINIMAP: ON", null, 24);
            ((RectTransform)MinimapToggle.transform).Place(rx, 860, (rw - 20) / 2f, 64);
            Close = Ui.Button("Close", box.transform, "CLOSE", null, 24, Theme.ButtonPrimary);
            ((RectTransform)Close.transform).Place(rx + (rw + 20) / 2f, 860, (rw - 20) / 2f, 64);
            Link(MinimapToggle, Close);
            FullRoot.SetActive(false);

            BuildMarkers();
        }

        // ------------------------------------------------------------------ positions

        /// <summary>
        /// Where a world position sits on the map, 0..1 across the land. Inside the Champion's Hall (built away from the
        /// land) counts as its door.
        /// </summary>
        public Vector2 UvOf(Vector3 p)
        {
            if (p.x > WorldGround.InteriorThreshold && _layout.HallDoor != null) p = _layout.HallDoor.transform.position;
            return new Vector2((p.x - _extent.xMin) / _extent.width, (p.z - _extent.yMin) / _extent.height);
        }

        // ------------------------------------------------------------------ per frame

        /// <summary>Moves the arrows and the minimap; refreshes the markers twice a second.</summary>
        public void Tick(Vector3 player, float yaw, bool showMinimap, float dt)
        {
            if (MiniRoot.activeSelf != showMinimap) MiniRoot.SetActive(showMinimap);
            var uv = UvOf(player);
            PlayerUv = uv;
            var turn = Quaternion.Euler(0, 0, -yaw);
            _fullPlayer.anchoredPosition = Vector2.Scale(uv, _fullSize);
            _fullPlayer.localRotation = turn;
            _miniPlayer.localRotation = turn;
            _miniContent.anchoredPosition = -Vector2.Scale(uv, _miniContentSize);
            if ((_refresh -= dt) <= 0f) RefreshMarkers();
        }

        public Selectable Open(string area)
        {
            FullRoot.SetActive(true);
            _where.text = "You are in " + area + ".\nCoins: " + GameFlow.Coins;
            MinimapToggle.SetText(GameFlow.MinimapHidden ? "MINIMAP: OFF" : "MINIMAP: ON");
            RefreshMarkers();
            return Close;
        }

        public void Hide() => FullRoot.SetActive(false);

        public void RefreshToggleLabel() => MinimapToggle.SetText(GameFlow.MinimapHidden ? "MINIMAP: OFF" : "MINIMAP: ON");

        /// <summary>How many markers of a kind are showing (tests).</summary>
        public int Count(MarkerKind kind)
        {
            int n = 0;
            foreach (var m in _markers) if (m.Shown == kind) n++;
            return n;
        }

        // ------------------------------------------------------------------ markers

        /// <summary>What a person's marker says right now: errands first, then stalls, then who plays Wheels.</summary>
        public static MarkerKind KindOf(Npc n)
        {
            if (!string.IsNullOrEmpty(n.ErrandHint())) return MarkerKind.Errand;
            if (n.ShopId != null) return MarkerKind.Stall;
            if (n.Role == "herald") return GameFlow.TournamentQualified && GameFlow.TournamentRound == 0 ? MarkerKind.Champion : MarkerKind.Hidden;
            var e = n.Encounter;
            if (e == null) return MarkerKind.Hidden;
            if (e.Tournament) return GameFlow.TournamentRound == e.TournamentRound ? MarkerKind.Champion : MarkerKind.Hidden;
            if (GameFlow.HasDefeated(e.Id)) return MarkerKind.Beaten;
            return e.IsChampion ? MarkerKind.Champion : MarkerKind.Challenger;
        }

        private void BuildMarkers()
        {
            foreach (var n in _layout.Npcs)
            {
                var npc = n;
                AddMarker(() => npc.transform.position, () => KindOf(npc));
            }
            foreach (var p in _layout.Pickups)
            {
                var pickup = p;
                AddMarker(() => pickup.transform.position, () => pickup.Available ? MarkerKind.Errand : MarkerKind.Hidden);
            }
            RefreshMarkers();
        }

        private void AddMarker(Func<Vector3> position, Func<MarkerKind> kind)
        {
            var m = new Marker { Position = position, Kind = kind };
            m.Full = Ui.Icon("Marker", _fullMap, null, 30f, Color.clear);
            m.Full.rectTransform.anchorMin = m.Full.rectTransform.anchorMax = Vector2.zero;
            m.Mini = Ui.Icon("Marker", _miniContent, null, 24f, Color.clear);
            m.Mini.rectTransform.anchorMin = m.Mini.rectTransform.anchorMax = Vector2.zero;
            _markers.Add(m);
        }

        private void RefreshMarkers()
        {
            _refresh = 0.5f;
            foreach (var m in _markers)
            {
                var kind = m.Kind();
                var uv = UvOf(m.Position());
                m.Full.rectTransform.anchoredPosition = Vector2.Scale(uv, _fullSize);
                m.Mini.rectTransform.anchoredPosition = Vector2.Scale(uv, _miniContentSize);
                if (kind == m.Shown) continue;
                m.Shown = kind;
                bool on = kind != MarkerKind.Hidden;
                m.Full.gameObject.SetActive(on);
                m.Mini.gameObject.SetActive(on);
                if (!on) continue;
                Style(m.Full, kind);
                Style(m.Mini, kind);
            }
            _fullPlayer.SetAsLastSibling(); // the player's arrow on top of everything
        }

        private void Style(Image img, MarkerKind kind)
        {
            img.sprite = SpriteFor(kind);
            img.color = img.sprite != null ? TintFor(kind) : FallbackFor(kind);
        }

        private Sprite SpriteFor(MarkerKind kind)
        {
            if (_icons == null) return null;
            switch (kind)
            {
                case MarkerKind.Champion: return _icons.crown;
                case MarkerKind.Stall: return _icons.hammer;
                case MarkerKind.Errand: return _icons.xp;
                case MarkerKind.Challenger:
                case MarkerKind.Beaten: return _icons.uiDiamond;
                default: return null;
            }
        }

        private static Color TintFor(MarkerKind kind) =>
            kind == MarkerKind.Challenger ? Theme.Gilt : kind == MarkerKind.Beaten ? new Color(0.45f, 0.6f, 0.45f, 0.9f) : Color.white;

        private static Color FallbackFor(MarkerKind kind)
        {
            switch (kind)
            {
                case MarkerKind.Champion: return Theme.Crown;
                case MarkerKind.Stall: return Theme.Hammer;
                case MarkerKind.Errand: return Theme.Xp;
                case MarkerKind.Beaten: return new Color(0.45f, 0.6f, 0.45f, 0.9f);
                default: return Theme.Gilt;
            }
        }

        // ------------------------------------------------------------------ building the pieces

        /// <summary>Place names written on the full map.</summary>
        private static readonly (string name, Vector2 at)[] Places =
        {
            (Areas.Hearthmoor, new Vector2(0, -18)),
            (Areas.NorthRoad, new Vector2(16, 82)),
            (Areas.Brindlecross, new Vector2(0, 116)),
            (Areas.Hall, new Vector2(0, 178)),
            (Areas.Outpost, new Vector2(82, 196)),
            (Areas.Lanternmere, new Vector2(-85, 112)),
            ("Mirrorwater", new Vector2(-85, 72)),
            (Areas.Duskhollow, new Vector2(-62, 196)),
            (Areas.Ironbell, new Vector2(92, 62)),
            (Areas.Crownhold, new Vector2(0, 262)),
        };

        private void PlaceLabel(string name, Vector2 at)
        {
            var uv = new Vector2((at.x - _extent.xMin) / _extent.width, (at.y - _extent.yMin) / _extent.height);
            var t = Ui.Label("Place", _fullMap, name.ToUpperInvariant(), 20, TextAnchor.MiddleCenter, new Color(0.22f, 0.12f, 0.06f), FontStyle.Bold);
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(260, 30);
            rt.anchoredPosition = Vector2.Scale(uv, _fullSize);
            t.gameObject.AddComponent<Outline>().effectColor = new Color(0.96f, 0.9f, 0.76f, 0.85f);
        }

        private void Key(Transform box, float x, ref float y, MarkerKind? kind, Sprite sprite, Color tint, string text)
        {
            var icon = Ui.Icon("KeyIcon", box, null, 40f, Color.clear);
            icon.rectTransform.Place(x, y, 40, 40);
            if (kind.HasValue) Style(icon, kind.Value);
            else { icon.sprite = sprite; icon.color = sprite != null ? Color.white : tint; }
            Ui.Label("KeyText", box, text, 24, TextAnchor.MiddleLeft, Theme.Text).rectTransform.Place(x + 56, y - 4, 460, 48);
            y += 62f;
        }

        private RectTransform MapImage(string name, Transform parent, Vector2 size)
        {
            var rt = Ui.Rect(name, parent);
            var img = rt.gameObject.AddComponent<RawImage>();
            img.texture = Texture;
            img.raycastTarget = false;
            rt.pivot = Vector2.zero;
            rt.sizeDelta = size;
            return rt;
        }

        private static RectTransform ArrowImage(Transform parent, Sprite arrow, float size)
        {
            var img = Ui.Icon("You", parent, arrow, size, Theme.Gilt);
            img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            return img.rectTransform;
        }

        private static Sprite ArrowSprite()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "MapArrow", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            tex.LoadRawTextureData(MapPainter.Arrow(size));
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>Paints the map texture from the built land: its terrain paint and heights, water, buildings and trees.</summary>
        public static Texture2D Paint(WorldLayout layout)
        {
            var ext = WorldGround.Extent;
            var src = new MapPainter.Source { XMin = ext.xMin, ZMin = ext.yMin, SizeX = ext.width, SizeZ = ext.height };
            var data = layout.Terrain != null ? layout.Terrain.terrainData : null;
            if (data != null)
            {
                src.Alpha = data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight);
                int res = data.heightmapResolution;
                var normal = data.GetHeights(0, 0, res, res);
                src.Heights = new float[res, res];
                for (int z = 0; z < res; z++)
                    for (int x = 0; x < res; x++)
                        src.Heights[z, x] = WorldGround.MinHeight + normal[z, x] * (WorldGround.MaxHeight - WorldGround.MinHeight);
            }
            src.Water = (x, z) => Mathf.Max(WorldGround.StreamMask(x, z), WorldGround.LakeMask(x, z));
            src.Deck = (x, z) => layout.Walkable.Contains(x, z);
            foreach (var b in layout.MapBuildings) src.Buildings.Add((b.at.x, b.at.y, b.size.x, b.size.y, b.yaw));
            foreach (var t in layout.MapTrees) src.Trees.Add((t.x, t.y));
            int w = TextureWidth, h = Mathf.RoundToInt(TextureWidth * ext.height / ext.width);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "WorldMap", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            tex.LoadRawTextureData(MapPainter.Paint(src, w, h));
            tex.Apply(false, true);
            return tex;
        }

        private static void Link(Selectable a, Selectable b)
        {
            a.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = b, selectOnDown = b };
            b.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = a, selectOnUp = a };
        }
    }
}
