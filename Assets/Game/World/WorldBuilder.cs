using System.Collections.Generic;
using Tabletop.Application;
using Tabletop.Presentation;
using UnityEngine;

namespace Tabletop.World
{
    /// <summary>Result of building the world: what the app needs to run it.</summary>
    public sealed class WorldLayout
    {
        public readonly WalkableArea Walkable = new WalkableArea();
        public readonly List<Interactable> Interactables = new List<Interactable>();
        public readonly List<Npc> Npcs = new List<Npc>();
        public Vector3 Spawn;
        public float SpawnYaw;
        public Transform Root;
        public Transform StaticRoot;
        public Chair ChampionChair;
        public Vector3 TableFocus;
        public Door HallDoor;
        public Door HallExit;
        /// <summary>Names that <see cref="WorldArtSet"/> slots can use: people, and buildings as "Area/Name".</summary>
        public readonly List<string> PersonKeys = new List<string> { WorldBuilder.PlayerKey };
        public readonly List<string> BuildingKeys = new List<string>();

        public Npc Find(string encounterId)
        {
            foreach (var n in Npcs) if (n.Encounter != null && n.Encounter.Id == encounterId) return n;
            return null;
        }

        /// <summary>Area name for a position (shown when the player moves between places).</summary>
        public static string AreaAt(Vector3 p)
        {
            if (p.x > 150) return "The Champion's Hall";
            if (p.z < 22) return "Hearthmoor";
            if (p.z < 113) return "The North Road";
            return "Brindlecross";
        }
    }

    /// <summary>
    /// Builds the first world slice (D-025) from primitives: Hearthmoor (start), the North Road with a stream,
    /// Brindlecross with its villagers, and the interior of the Champion's Hall. North is +z.
    /// </summary>
    public sealed class WorldBuilder
    {
        public const float InteriorX = 200f;
        public const string PlayerKey = "Player";
        public const float DefaultPersonHeight = 1.75f;

        private readonly WorldKit _kit;
        private readonly IconSet _icons;
        private readonly WorldArtSet _art;
        private readonly WorldLayout _layout = new WorldLayout();
        private Transform _static;
        private Transform _dynamic;
        private Transform _models;
        private int _seed = 12345;

        public WorldBuilder(WorldKit kit, IconSet icons, WorldArtSet art = null)
        {
            _kit = kit;
            _icons = icons;
            _art = art;
        }

        private float Rand()
        {
            // Deterministic scatter so the world looks the same every launch.
            _seed = unchecked(_seed * 1103515245 + 12345);
            return ((_seed >> 8) & 0xFFFF) / 65535f;
        }

        public static readonly Vector2[] RoadPoints =
        {
            new Vector2(0, 20), new Vector2(6, 34), new Vector2(-3, 48), new Vector2(-5.5f, 62),
            new Vector2(3, 76), new Vector2(9, 90), new Vector2(2, 104), new Vector2(0, 116),
        };

        public WorldLayout Build(Transform parent)
        {
            var root = new GameObject("World").transform;
            root.SetParent(parent, false);
            _layout.Root = root;
            _static = new GameObject("Static").transform;
            _static.SetParent(root, false);
            _dynamic = new GameObject("People").transform;
            _dynamic.SetParent(root, false);
            // Imported models are not static-batched (their meshes are not CPU-readable).
            _models = new GameObject("Models").transform;
            _models.SetParent(root, false);
            _layout.StaticRoot = _static;

            // Ground (solid so the character controller has a floor), with darker grass patches for texture.
            _kit.Box("Ground", _static, new Vector3(0, -0.5f, 70), new Vector3(140, 1, 220), Palette.Grass, solid: true);
            for (int i = 0; i < 90; i++)
            {
                var p = new Vector3(-65 + Rand() * 130, 0.004f, -35 + Rand() * 210);
                float s = 3f + Rand() * 7f;
                _kit.Prim(PrimitiveType.Cylinder, "GrassPatch", _static, p, new Vector3(s, 0.005f, s * (0.6f + Rand() * 0.6f)), Rand() < 0.5f ? Palette.GrassDark : Palette.LeavesLight);
            }

            BuildHearthmoor();
            BuildRoad();
            BuildBrindlecross();
            BuildHall();
            BuildInterior();
            return _layout;
        }

        // ------------------------------------------------------------------ Hearthmoor (start)

        private void BuildHearthmoor()
        {
            var w = _layout.Walkable;
            w.Rect(-21, 21, -15, 22);
            _kit.Prim(PrimitiveType.Cylinder, "Square", _static, new Vector3(0, 0.01f, 4), new Vector3(18, 0.02f, 18), Palette.Dirt);
            WorldPieces.Well(_kit, _static, new Vector3(0, 0, 4));
            House("Hearthmoor", "Home", new Vector3(0, 0, -13), 0, 7, 5, 3.4f, Palette.PlasterWarm, Palette.RoofRed);
            House("Hearthmoor", "Bakery", new Vector3(-14, 0, 8), 90, 6, 5, 3.2f, Palette.Plaster, Palette.RoofBrown);
            House("Hearthmoor", "Mill", new Vector3(14, 0, 10), -90, 6, 6, 4f, Palette.Plaster, Palette.RoofBlue);
            House("Hearthmoor", "Cottage", new Vector3(-14, 0, -6), 90, 5, 5, 3f, Palette.PlasterWarm, Palette.RoofGreen);
            House("Hearthmoor", "Barn", new Vector3(14, 0, -7), -90, 7, 5, 3.6f, new Color(0.7f, 0.35f, 0.28f), Palette.RoofBrown);
            WorldPieces.Bench(_kit, _static, new Vector3(-6, 0, 9), 0);
            WorldPieces.Lamp(_kit, _static, new Vector3(-4, 0, 14));
            WorldPieces.Lamp(_kit, _static, new Vector3(4, 0, 14));
            for (int i = 0; i < 8; i++) WorldPieces.Flowers(_kit, _static, new Vector3(-18 + i * 5.1f, 0, 18.5f), i);
            WorldPieces.Fence(_kit, _static, new Vector3(-22, 0, -16), new Vector3(22, 0, -16));
            WorldPieces.Fence(_kit, _static, new Vector3(-22, 0, -16), new Vector3(-22, 0, 20));
            WorldPieces.Fence(_kit, _static, new Vector3(22, 0, -16), new Vector3(22, 0, 20));
            for (int i = 0; i < 14; i++)
            {
                WorldPieces.Tree(_kit, _static, new Vector3(-26 - Rand() * 6, 0, -14 + i * 3), 1f + Rand() * 0.4f, i);
                WorldPieces.Tree(_kit, _static, new Vector3(26 + Rand() * 6, 0, -14 + i * 3), 1f + Rand() * 0.4f, i + 1);
            }
            for (int i = 0; i < 12; i++) WorldPieces.Tree(_kit, _static, new Vector3(-24 + i * 4.4f, 0, -20 - Rand() * 3), 1.1f, i);
            for (int i = 0; i < 5; i++)
            {
                WorldPieces.Tree(_kit, _static, new Vector3(-20 + i * 3.5f, 0, 24 + Rand() * 2), 1f + Rand() * 0.3f, i);
                WorldPieces.Tree(_kit, _static, new Vector3(7 + i * 3.5f, 0, 24 + Rand() * 2), 1f + Rand() * 0.3f, i + 2);
            }
            AddSign(new Vector3(2.5f, 0, 19.5f), "North Road", "NORTH ROAD\nBrindlecross - a morning's walk north.\nHome of a real Reels Champion!");

            _layout.Spawn = new Vector3(0, 0, -7.5f);
            _layout.SpawnYaw = 0;

            AddNpc("Gran Oddly", "retired player", new Vector3(-6, 0, 10.2f), 180,
                new PersonLook { Cloth = new Color(0.55f, 0.35f, 0.55f), Accent = Palette.Plaster, Hair = new Color(0.85f, 0.85f, 0.88f), Hat = HatKind.Kerchief, HatColor = new Color(0.8f, 0.4f, 0.45f), Height = 0.9f },
                EncounterCatalog.Get(EncounterCatalog.Gran), null);
            AddNpc("Kit", "miller's kid", new Vector3(7, 0, 2), 250,
                new PersonLook { Cloth = new Color(0.35f, 0.6f, 0.4f), Accent = Palette.Wood, Hat = HatKind.Cap, HatColor = new Color(0.8f, 0.3f, 0.25f), Height = 0.75f },
                null, new[]
                {
                    "Are you really going to Brindlecross? The Champion there has NEVER lost at his own table!",
                    "Gran taught everybody here how to play Reels. You should play her first.",
                });
            AddNpc("Hollis", "baker", new Vector3(-10.5f, 0, 5), 90,
                new PersonLook { Cloth = Palette.Plaster, Accent = new Color(0.85f, 0.8f, 0.7f), Hat = HatKind.WideBrim, HatColor = Color.white, Height = 1f },
                null, new[]
                {
                    "Fresh bread for the road? Ha, I'm only teasing. I'd never let a loaf leave this village.",
                    "Follow the road north. It crosses a stream halfway. Can't miss the bridge.",
                });
        }

        // ------------------------------------------------------------------ The North Road

        private void BuildRoad()
        {
            var w = _layout.Walkable;
            // Wren's campfire clearing west of the road, joined by a short trail (added first so no trees spawn in it).
            w.Circle(-10, 44, 5f);
            w.Capsule(new Vector2(-1, 44), new Vector2(-7, 44), 2.2f);
            _kit.Box("CampTrail", _static, new Vector3(-4, 0.018f, 44), new Vector3(7, 0.03f, 2.6f), Palette.Dirt);
            for (int i = 0; i < RoadPoints.Length - 1; i++)
            {
                var a = RoadPoints[i];
                var b = RoadPoints[i + 1];
                w.Capsule(a, b, 3.0f);
                var dir = b - a;
                float len = dir.magnitude;
                float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
                var mid = (a + b) / 2f;
                _kit.Box("Road", _static, new Vector3(mid.x, 0.02f, mid.y), new Vector3(4.2f, 0.04f, len + 3.5f), Palette.Dirt, false, yaw);
                // Trees and rocks line both sides of the road.
                var n = new Vector2(dir.y, -dir.x).normalized;
                for (float t = 0; t < len; t += 3.2f)
                {
                    var p = a + dir.normalized * t;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float off = 6.2f + Rand() * 5f;
                        var q = p + n * side * off;
                        if (w.Contains(q.x, q.y) || InStream(q.y) || HidesCamp(q)) continue;
                        if (Rand() < 0.8f) WorldPieces.Tree(_kit, _static, new Vector3(q.x, 0, q.y), 0.9f + Rand() * 0.6f, (int)(t * 7 + side));
                        else WorldPieces.Rock(_kit, _static, new Vector3(q.x, 0, q.y), 0.8f + Rand());
                        var q2 = p + n * side * (off + 5f + Rand() * 4f);
                        if (!InStream(q2.y) && !w.Contains(q2.x, q2.y) && !HidesCamp(q2)) WorldPieces.Tree(_kit, _static, new Vector3(q2.x, 0, q2.y), 1.1f + Rand() * 0.6f, (int)(t * 3) + side);
                    }
                    if (Rand() < 0.35f)
                    {
                        var f = p + n * (Rand() < 0.5f ? -3.4f : 3.4f);
                        if (Rand() < 0.5f) WorldPieces.Flowers(_kit, _static, new Vector3(f.x, 0, f.y), (int)t);
                        else WorldPieces.Bush(_kit, _static, new Vector3(f.x, 0, f.y), 0.8f);
                    }
                }
            }

            // The stream and its bridge (the road crosses at z = 62).
            _kit.Box("Stream", _static, new Vector3(0, 0.03f, 62), new Vector3(140, 0.06f, 4.5f), Palette.Water);
            _kit.Box("StreamBankN", _static, new Vector3(0, 0.02f, 64.6f), new Vector3(140, 0.05f, 0.8f), Palette.Dirt);
            _kit.Box("StreamBankS", _static, new Vector3(0, 0.02f, 59.4f), new Vector3(140, 0.05f, 0.8f), Palette.Dirt);
            var bridge = new GameObject("Bridge").transform;
            bridge.SetParent(_static, false);
            bridge.localPosition = new Vector3(-5.5f, 0, 62);
            _kit.Box("Deck", bridge, new Vector3(0, 0.22f, 0), new Vector3(4.2f, 0.2f, 7f), Palette.Wood);
            for (int i = 0; i < 7; i++) _kit.Box("Plank", bridge, new Vector3(0, 0.34f, -3 + i), new Vector3(4.2f, 0.04f, 0.1f), Palette.WoodDark);
            foreach (float x in new[] { -2.1f, 2.1f })
            {
                _kit.Box("Rail", bridge, new Vector3(x, 0.9f, 0), new Vector3(0.12f, 0.12f, 7f), Palette.WoodDark);
                for (int i = 0; i < 4; i++) _kit.Box("RailPost", bridge, new Vector3(x, 0.6f, -3 + i * 2), new Vector3(0.14f, 0.8f, 0.14f), Palette.WoodDark);
            }

            // Wren's camp in a clearing west of the road.
            _kit.Prim(PrimitiveType.Cylinder, "Clearing", _static, new Vector3(-10, 0.015f, 44), new Vector3(9, 0.02f, 9), Palette.Dirt * 1.05f);
            WorldPieces.Campfire(_kit, _static, new Vector3(-9, 0, 43));
            _kit.Prism("Tent", _static, new Vector3(-13, 0, 46.5f), new Vector3(3f, 2.2f, 3.5f), new Color(0.75f, 0.6f, 0.35f), 20);
            AddSign(new Vector3(3.2f, 0, 57.5f), "Stream", "THE WILLOW STREAM\nBrindlecross: keep north.\nHearthmoor: back south.");
            AddNpc("Wren", "wandering tinker", new Vector3(-11.5f, 0, 42.5f), 60,
                new PersonLook { Cloth = new Color(0.5f, 0.42f, 0.3f), Accent = new Color(0.3f, 0.5f, 0.45f), Hat = HatKind.Hood, HatColor = new Color(0.35f, 0.45f, 0.35f) },
                EncounterCatalog.Get(EncounterCatalog.Wren), null);
        }

        private static bool InStream(float z) => z > 58.5f && z < 65.5f;

        /// <summary>The camera looks north from the south: keep trees off the strip in front of Wren's camp.</summary>
        private static bool HidesCamp(Vector2 q) => q.x > -19f && q.x < -1f && q.y > 33f && q.y < 44f;

        // ------------------------------------------------------------------ Brindlecross

        private void BuildBrindlecross()
        {
            var w = _layout.Walkable;
            w.Rect(-25, 25, 113, 158);
            _kit.Prim(PrimitiveType.Cylinder, "Plaza", _static, new Vector3(0, 0.01f, 134), new Vector3(24, 0.02f, 24), Palette.Stone);
            _kit.Box("Avenue", _static, new Vector3(0, 0.015f, 150), new Vector3(5, 0.03f, 16), Palette.Stone);
            WorldPieces.Fountain(_kit, _static, new Vector3(0, 0, 134));
            House("Brindlecross", "Chandlery", new Vector3(-19, 0, 124), 90, 7, 6, 3.6f, Palette.PlasterWarm, Palette.RoofRed);
            House("Brindlecross", "FisherHut", new Vector3(19, 0, 122), -90, 6, 5, 3.2f, Palette.Plaster, Palette.RoofBlue);
            House("Brindlecross", "Infirmary", new Vector3(-19, 0, 141), 90, 7, 6, 3.8f, Color.white, Palette.RoofGreen);
            House("Brindlecross", "Inn", new Vector3(19, 0, 142), -90, 8, 7, 4.4f, Palette.PlasterWarm, Palette.RoofBrown);
            House("Brindlecross", "Cottage", new Vector3(-12, 0, 153), 180, 6, 5, 3.2f, Palette.Plaster, Palette.RoofRed);
            House("Brindlecross", "Cottage2", new Vector3(12, 0, 153), 180, 6, 5, 3.2f, Palette.Plaster, Palette.RoofBlue);
            WorldPieces.Stall(_kit, _static, new Vector3(-8, 0, 119), 30, new Color(0.8f, 0.25f, 0.25f));
            WorldPieces.Stall(_kit, _static, new Vector3(8, 0, 119), -30, new Color(0.25f, 0.45f, 0.8f));
            foreach (var p in new[] { new Vector3(-6, 0, 128), new Vector3(6, 0, 128), new Vector3(-6, 0, 141), new Vector3(6, 0, 141) })
                WorldPieces.Lamp(_kit, _static, p);
            WorldPieces.Bench(_kit, _static, new Vector3(-9, 0, 134), 90);
            WorldPieces.Bench(_kit, _static, new Vector3(9, 0, 134), 90);
            for (int i = 0; i < 10; i++) WorldPieces.Flowers(_kit, _static, new Vector3(-22 + i * 4.9f, 0, 157), i + 3);
            // Village edge: trees all round.
            for (int i = 0; i < 16; i++)
            {
                WorldPieces.Tree(_kit, _static, new Vector3(-29 - Rand() * 5, 0, 112 + i * 3.6f), 1.1f + Rand() * 0.4f, i);
                WorldPieces.Tree(_kit, _static, new Vector3(29 + Rand() * 5, 0, 112 + i * 3.6f), 1.1f + Rand() * 0.4f, i + 1);
            }
            for (int i = 0; i < 6; i++)
            {
                WorldPieces.Tree(_kit, _static, new Vector3(-26 + i * 3.8f, 0, 110 - Rand() * 3), 1f, i);
                WorldPieces.Tree(_kit, _static, new Vector3(7 + i * 3.8f, 0, 110 - Rand() * 3), 1f, i + 3);
            }
            AddSign(new Vector3(-3.5f, 0, 113.8f), "Welcome", "BRINDLECROSS\nHome of Corvin Vale,\nReels Champion of the valley.");

            AddNpc("Bram", "town crier", new Vector3(4, 0, 118), 200,
                new PersonLook { Cloth = new Color(0.7f, 0.55f, 0.25f), Accent = Palette.WoodDark, Hat = HatKind.WideBrim, HatColor = new Color(0.25f, 0.2f, 0.18f) },
                null, new[]
                {
                    "Welcome to Brindlecross! Everyone here plays Reels. EVERYONE.",
                    "Mira, Tobin and Sister Halvey will all give you a game. Beat them and people will start to talk.",
                    "The Champion, Corvin Vale, lives in the great hall at the top of the town. He never turns down a challenger.",
                });
            AddNpc("Mira Tallow", "candlemaker", new Vector3(-13.5f, 0, 124), 90,
                new PersonLook { Cloth = new Color(0.75f, 0.35f, 0.3f), Accent = Palette.Plaster, Hair = new Color(0.55f, 0.25f, 0.12f), Hat = HatKind.Kerchief, HatColor = new Color(0.95f, 0.85f, 0.4f) },
                EncounterCatalog.Get(EncounterCatalog.Mira), null);
            AddNpc("Tobin Reed", "fisher", new Vector3(13.5f, 0, 124), 270,
                new PersonLook { Cloth = new Color(0.3f, 0.45f, 0.55f), Accent = new Color(0.55f, 0.45f, 0.3f), Hat = HatKind.WideBrim, HatColor = new Color(0.6f, 0.55f, 0.35f) },
                EncounterCatalog.Get(EncounterCatalog.Tobin), null);
            AddNpc("Sister Halvey", "healer", new Vector3(-13.5f, 0, 141), 90,
                new PersonLook { Cloth = new Color(0.92f, 0.92f, 0.95f), Accent = new Color(0.3f, 0.55f, 0.75f), Hat = HatKind.Hood, HatColor = new Color(0.3f, 0.5f, 0.75f), Robe = true },
                EncounterCatalog.Get(EncounterCatalog.Halvey), null);
            AddNpc("Peg", "hall steward", new Vector3(4.2f, 0, 154.5f), 200,
                new PersonLook { Cloth = Palette.Banner, Accent = Palette.Gold, Hat = HatKind.Cap, HatColor = Palette.Banner },
                null, new[]
                {
                    "This is the Champion's Hall. Master Vale keeps his table ready day and night.",
                    "Just walk in and take the empty chair. He'll know what you want.",
                });
        }

        // ------------------------------------------------------------------ The Champion's Hall (outside)

        private void BuildHall()
        {
            var hall = new GameObject("ChampionsHall").transform;
            hall.SetParent(_static, false);
            hall.localPosition = new Vector3(0, 0, 166);
            var wall = new Color(0.85f, 0.8f, 0.72f);
            _kit.Box("Walls", hall, new Vector3(0, 3.2f, 0), new Vector3(16, 6.4f, 12), wall, solid: true);
            _kit.Box("Base", hall, new Vector3(0, 0.25f, 0), new Vector3(16.6f, 0.5f, 12.6f), Palette.StoneDark);
            _kit.Prism("Roof", hall, new Vector3(0, 6.4f, 0), new Vector3(13.2f, 4f, 17.2f), new Color(0.3f, 0.3f, 0.45f), 90);
            foreach (float x in new[] { -7.6f, 7.6f })
            {
                _kit.Box("Tower", hall, new Vector3(x, 4.5f, -5.6f), new Vector3(2.2f, 9f, 2.2f), wall, solid: true);
                _kit.Cone("TowerRoof", hall, new Vector3(x, 9f, -5.6f), new Vector3(3f, 2.6f, 3f), new Color(0.3f, 0.3f, 0.45f));
                _kit.Box("Banner", hall, new Vector3(x * 0.55f, 3.6f, -6.05f), new Vector3(1.3f, 3.2f, 0.06f), Palette.Banner);
                _kit.Box("BannerCrown", hall, new Vector3(x * 0.55f, 4.3f, -6.1f), new Vector3(0.6f, 0.45f, 0.04f), Palette.Gold);
                _kit.Box("Torch", hall, new Vector3(x * 0.25f, 2.4f, -6.2f), new Vector3(0.2f, 0.5f, 0.2f), Palette.Fire);
            }
            _kit.Box("Door", hall, new Vector3(0, 1.6f, -6.03f), new Vector3(2.4f, 3.2f, 0.1f), Palette.WoodDark);
            _kit.Box("DoorArch", hall, new Vector3(0, 3.35f, -6.06f), new Vector3(3f, 0.3f, 0.12f), Palette.Gold);
            _kit.Box("Steps", hall, new Vector3(0, 0.1f, -6.9f), new Vector3(4.4f, 0.2f, 1.6f), Palette.Stone);
            _kit.Label("HallTitle", hall, new Vector3(0, 7.2f, -6.2f), "THE CHAMPION'S HALL", 60, Palette.Gold, 0.012f);

            var door = new GameObject("HallDoor").AddComponent<Door>();
            door.transform.SetParent(_dynamic, false);
            door.transform.localPosition = new Vector3(0, 0, 158.6f);
            door.Label = "Enter the Champion's Hall";
            door.Target = new Vector3(InteriorX, 0, -3.2f);
            door.TargetYaw = 0;
            door.AreaName = "The Champion's Hall";
            door.Radius = 2.6f;
            _layout.Interactables.Add(door);
            _layout.HallDoor = door;
        }

        // ------------------------------------------------------------------ The Champion's Hall (inside)

        private void BuildInterior()
        {
            var room = new GameObject("HallInterior").transform;
            room.SetParent(_static, false);
            room.localPosition = new Vector3(InteriorX, 0, 0);
            _layout.Walkable.Rect(InteriorX - 5.3f, InteriorX + 5.3f, -4.3f, 4.2f);
            var wall = new Color(0.62f, 0.5f, 0.42f);
            _kit.Box("Floor", room, new Vector3(0, 0.01f, 0), new Vector3(12, 0.04f, 10), new Color(0.45f, 0.3f, 0.2f));
            _kit.Box("Rug", room, new Vector3(0, 0.04f, 0.6f), new Vector3(6, 0.02f, 5), Palette.Banner);
            _kit.Box("RugTrim", room, new Vector3(0, 0.035f, 0.6f), new Vector3(6.4f, 0.02f, 5.4f), Palette.Gold);
            _kit.Box("WallN", room, new Vector3(0, 2.5f, 5.2f), new Vector3(12.4f, 5f, 0.4f), wall, solid: true);
            _kit.Box("WallW", room, new Vector3(-6.2f, 2.5f, 0), new Vector3(0.4f, 5f, 10.4f), wall, solid: true);
            _kit.Box("WallE", room, new Vector3(6.2f, 2.5f, 0), new Vector3(0.4f, 5f, 10.4f), wall, solid: true);
            // Low front wall: the camera looks in over it (diorama cut-away).
            _kit.Box("WallS_L", room, new Vector3(-3.7f, 0.4f, -5.1f), new Vector3(5f, 0.8f, 0.3f), wall, solid: true);
            _kit.Box("WallS_R", room, new Vector3(3.7f, 0.4f, -5.1f), new Vector3(5f, 0.8f, 0.3f), wall, solid: true);
            _kit.Box("ExitMat", room, new Vector3(0, 0.03f, -4.6f), new Vector3(2.2f, 0.02f, 0.8f), Palette.WoodDark);
            // Fireplace, banners, trophies.
            _kit.Box("Fireplace", room, new Vector3(0, 1.2f, 4.8f), new Vector3(3f, 2.4f, 0.6f), Palette.StoneDark);
            _kit.Box("Hearth", room, new Vector3(0, 0.7f, 4.55f), new Vector3(1.6f, 1.0f, 0.2f), new Color(0.12f, 0.08f, 0.06f));
            _kit.Cone("Fire", room, new Vector3(0, 0.3f, 4.5f), new Vector3(0.8f, 0.9f, 0.4f), Palette.Fire);
            foreach (float x in new[] { -3.8f, 3.8f })
            {
                _kit.Box("Banner", room, new Vector3(x, 3f, 4.95f), new Vector3(1.4f, 2.8f, 0.05f), Palette.Banner);
                _kit.Box("BannerMark", room, new Vector3(x, 3.4f, 4.9f), new Vector3(0.6f, 0.5f, 0.04f), Palette.Gold);
                _kit.Box("Shelf", room, new Vector3(x * 1.35f, 1.5f, 3.5f), new Vector3(0.6f, 3f, 2.4f), Palette.WoodDark, solid: true);
                for (int i = 0; i < 3; i++)
                    _kit.Prim(PrimitiveType.Cylinder, "Trophy", room, new Vector3(x * 1.35f, 0.6f + i * 0.9f, 3.5f), new Vector3(0.3f, 0.18f, 0.3f), Palette.Gold);
            }
            WorldPieces.GameTable(_kit, room, new Vector3(0, 0, 0.8f), 1f);
            WorldPieces.Chair(_kit, room, new Vector3(0, 0, 2.3f), 180);
            WorldPieces.Chair(_kit, room, new Vector3(0, 0, -0.7f), 0);
            _layout.TableFocus = new Vector3(InteriorX, 1f, 0.8f);

            var champion = AddNpc("Corvin Vale", "Champion of Brindlecross", new Vector3(InteriorX, 0, 2.3f), 180,
                new PersonLook { Cloth = new Color(0.2f, 0.2f, 0.32f), Accent = Palette.Gold, Hair = new Color(0.15f, 0.12f, 0.12f), Hat = HatKind.Circlet, Robe = true, Height = 1.05f },
                EncounterCatalog.Get(EncounterCatalog.Champion), null);
            champion.BaseOffset = new Vector3(0, -0.35f, 0); // seated in his chair
            champion.Radius = 0f; // talk to him by sitting down

            var chair = new GameObject("ChallengerChair").AddComponent<Chair>();
            chair.transform.SetParent(_dynamic, false);
            chair.transform.localPosition = new Vector3(InteriorX, 0, -1.2f);
            chair.SeatPosition = new Vector3(InteriorX, 0, -0.7f);
            chair.Champion = champion;
            chair.Radius = 1.8f;
            _layout.Interactables.Add(chair);
            _layout.ChampionChair = chair;

            var exit = new GameObject("HallExit").AddComponent<Door>();
            exit.transform.SetParent(_dynamic, false);
            exit.transform.localPosition = new Vector3(InteriorX, 0, -4.1f);
            exit.Label = "Leave the hall";
            exit.Target = new Vector3(0, 0, 156.8f);
            exit.TargetYaw = 180;
            exit.AreaName = "Brindlecross";
            exit.Radius = 1.6f;
            _layout.Interactables.Add(exit);
            _layout.HallExit = exit;
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>A house, or the imported model assigned to "Area/Name" in the art set (door faces local +z either way).</summary>
        private Transform House(string area, string name, Vector3 pos, float yaw, float width, float depth, float height, Color wall, Color roof)
        {
            var key = area + "/" + name;
            _layout.BuildingKeys.Add(key);
            var slot = _art != null ? _art.Building(key) : null;
            if (slot == null) return WorldPieces.House(_kit, _static, name, pos, yaw, width, depth, height, wall, roof);
            var root = new GameObject(name).transform;
            root.SetParent(_models, false);
            root.localPosition = pos;
            root.localRotation = Quaternion.Euler(0, yaw, 0);
            var footprint = slot.size > 0 ? new Vector2(slot.size, slot.size * depth / width) : new Vector2(width, depth);
            var model = _kit.PlaceModel(slot, root, 0, footprint, out var size);
            var col = model.gameObject.AddComponent<BoxCollider>(); // solid, like the placeholder walls
            var s = model.localScale.x;
            col.size = size / s;
            col.center = new Vector3(0, size.y / 2f, 0) / s;
            return root;
        }

        /// <summary>A person: the imported model assigned to them in the art set, or the primitive figure.</summary>
        private Transform Person(string name, Vector3 pos, float yaw, PersonLook look, out float height)
        {
            if (!_layout.PersonKeys.Contains(name)) _layout.PersonKeys.Add(name);
            var slot = _art != null ? _art.Person(name) : null;
            height = 1.8f;
            if (slot == null) return WorldPieces.Person(_kit, _dynamic, name, pos, yaw, look);
            var root = new GameObject(name).transform;
            root.SetParent(_dynamic, false);
            root.localPosition = pos;
            root.localRotation = Quaternion.Euler(0, yaw, 0);
            var body = new GameObject("Body").transform;
            body.SetParent(root, false);
            height = slot.size > 0 ? slot.size : DefaultPersonHeight;
            _kit.PlaceModel(slot, body, height, Vector2.zero, out _);
            return root;
        }

        private Npc AddNpc(string name, string title, Vector3 pos, float yaw, PersonLook look, EncounterDefinition encounter, string[] lines)
        {
            var person = Person(name, pos, yaw, look, out float height);
            var npc = person.gameObject.AddComponent<Npc>();
            npc.DisplayName = name;
            npc.Title = title;
            npc.Encounter = encounter;
            npc.Lines = lines ?? new string[0];
            npc.Figure = person.Find("Body");
            npc.HomeYaw = yaw;
            // Blocker so the player can't walk through people.
            var col = person.gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, 0.9f, 0);
            col.height = 1.8f;
            col.radius = 0.4f;
            float tagY = Mathf.Max(2.25f, height + 0.45f);
            npc.Tag = _kit.Label("NameTag", person, new Vector3(0, tagY, 0), "", 34, encounter != null ? Palette.Gold : Color.white);
            if (encounter != null && _icons != null)
                npc.Marker = _kit.IconLabel("ChallengeMarker", person, new Vector3(0, tagY + 1.1f, 0), _icons.energyA, 0.55f);
            npc.RefreshTag();
            _layout.Npcs.Add(npc);
            _layout.Interactables.Add(npc);
            return npc;
        }

        private void AddSign(Vector3 pos, string title, string text)
        {
            WorldPieces.Signpost(_kit, _static, pos, 0);
            var sign = new GameObject("Sign_" + title).AddComponent<Sign>();
            sign.transform.SetParent(_dynamic, false);
            sign.transform.localPosition = pos;
            sign.Title = title;
            sign.Text = text;
            sign.Radius = 2f;
            _layout.Interactables.Add(sign);
        }
    }
}
