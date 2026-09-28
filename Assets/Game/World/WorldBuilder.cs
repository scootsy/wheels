using System.Collections.Generic;
using Tabletop.Application;
using Tabletop.Presentation;
using UnityEngine;

namespace Tabletop.World
{
    /// <summary>Result of building the world: what the app needs to run it.</summary>
    public sealed class WorldLayout
    {
        public WorldLayout() { Roam = new RoamArea(Walkable); }

        /// <summary>Paths, villages, bridges, piers and interiors: the level ground (and what shapes the land).</summary>
        public readonly WalkableArea Walkable = new WalkableArea();
        /// <summary>Where the player can actually go (D-038): the paths plus the open land around them.</summary>
        public readonly RoamArea Roam;
        /// <summary>Trees and buildings the overhead camera looks through when they stand in the way (D-038).</summary>
        public readonly List<Occluder> Occluders = new List<Occluder>();
        public readonly List<Interactable> Interactables = new List<Interactable>();
        public readonly List<Npc> Npcs = new List<Npc>();
        public readonly List<Pickup> Pickups = new List<Pickup>();
        public readonly List<Chair> Chairs = new List<Chair>();
        public Vector3 Spawn;
        public float SpawnYaw;
        public Transform Root;
        public Transform StaticRoot;
        public Terrain Terrain;
        /// <summary>The Brindlecross champion's chair (inside the hall).</summary>
        public Chair ChampionChair;
        /// <summary>The Outpost champion's chair (the table on the ledge).</summary>
        public Chair OutpostChair;
        /// <summary>D-038: the pier at Lanternmere, the stones at Duskhollow, the great bell at Ironbell.</summary>
        public Chair LakeChair;
        public Chair HollowChair;
        public Chair BellChair;
        /// <summary>The Grand Tournament's table at Crownhold; its opponent changes with the round (D-038).</summary>
        public Chair TournamentChair;
        /// <summary>Where the current round's opponent sits at the tournament table.</summary>
        public Vector3 TournamentSeat;
        public Npc Herald;
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

        public Npc FindByName(string displayName)
        {
            foreach (var n in Npcs) if (n.DisplayName == displayName) return n;
            return null;
        }

        /// <summary>Area name for a position (shown when the player moves between places).</summary>
        public static string AreaAt(Vector3 p)
        {
            if (p.x > WorldGround.InteriorThreshold) return Areas.Hall;
            var q = new Vector2(p.x, p.z);
            if (Vector2.Distance(q, new Vector2(-84f, 90f)) < 34f) return Areas.Lanternmere;
            if (Vector2.Distance(q, new Vector2(-62f, 215f)) < 30f) return Areas.Duskhollow;
            if (p.z > 210f && p.x > -36f && p.x < 36f) return Areas.Crownhold;
            if (p.x > 64f && p.z < 108f) return Areas.Ironbell;
            if (p.x > 64f && p.z < 146f) return Areas.MoorTrack;
            if (p.x > 58f) return Areas.Outpost;
            if (p.z > 159f && p.x > -16f && p.x < 45f) return Areas.TourneyRoad;
            if (p.x > 24f && p.z > 110f) return Areas.QuarryPath;
            if (p.z > 159f && p.x <= -16f) return Areas.HollowPath;
            if (p.x > 20f && p.z > 40f && p.z < 110f) return Areas.BellRoad;
            if (p.x < -12f && p.z > 64f && p.z < 112f) return Areas.StreamPath;
            if (p.z < 22f) return Areas.Hearthmoor;
            if (p.z < 113f) return Areas.NorthRoad;
            return Areas.Brindlecross;
        }
    }

    /// <summary>
    /// Builds the world (D-025, D-033): Hearthmoor (start), the North Road over the Willow Stream, Brindlecross, the
    /// Champion's Hall, and up the Quarry Path to the Stonemasons' Outpost. North is +z, the highlands are +x.
    /// Uses the art in <see cref="WorldLook"/> where it exists and primitive placeholders where it does not.
    /// </summary>
    public sealed partial class WorldBuilder
    {
        public const float InteriorX = 200f;
        public const string PlayerKey = "Player";
        public const float DefaultPersonHeight = 1.75f;
        /// <summary>Pack buildings are scaled uniformly so every door is about the same size.</summary>
        public const float HouseScale = 0.8f;

        public static readonly Vector2[] RoadPoints =
        {
            new Vector2(0, 20), new Vector2(6, 34), new Vector2(-3, 48), new Vector2(-5.5f, 62),
            new Vector2(3, 76), new Vector2(9, 90), new Vector2(2, 104), new Vector2(0, 116),
        };

        public static readonly Vector2[] QuarryPath =
        {
            new Vector2(22, 137), new Vector2(32, 138), new Vector2(40, 132), new Vector2(47, 129),
            new Vector2(53, 136), new Vector2(57, 146), new Vector2(60, 154), new Vector2(66, 158),
        };

        /// <summary>Where the Outpost champion's table stands, on the ledge above the valley.</summary>
        public static readonly Vector2 LedgeTable = new Vector2(66f, 177f);

        private readonly WorldKit _kit;
        private readonly IconSet _icons;
        private readonly WorldArtSet _art;
        private readonly WorldLook _look;
        private readonly WorldLayout _layout = new WorldLayout();
        private readonly List<Vector3> _clear = new List<Vector3>(); // x, z, radius kept free of scattered trees
        private Transform _static;
        private Transform _dynamic;
        private Transform _models;
        private int _seed = 12345;

        public WorldBuilder(WorldKit kit, IconSet icons, WorldArtSet art = null, WorldLook look = null)
        {
            _kit = kit;
            _icons = icons;
            _art = art;
            _look = look;
        }

        private float Rand()
        {
            // Deterministic scatter so the world looks the same every launch.
            _seed = unchecked(_seed * 1103515245 + 12345);
            return ((_seed >> 8) & 0xFFFF) / 65535f;
        }

        private GameObject P(string key) => _look != null ? _look.Prefab(key) : null;
        private static Vector3 G(float x, float z) => WorldGround.OnGround(x, z);
        private float T(float x, float z) => WorldGround.Terrain(x, z, _layout.Walkable);

        public WorldLayout Build(Transform parent)
        {
            var root = new GameObject("World").transform;
            root.SetParent(parent, false);
            _layout.Root = root;
            _static = new GameObject("Static").transform;
            _static.SetParent(root, false);
            _dynamic = new GameObject("People").transform;
            _dynamic.SetParent(root, false);
            // Pack models are not static-batched (their meshes are not CPU-readable); the SRP batcher handles them.
            _models = new GameObject("Models").transform;
            _models.SetParent(root, false);
            _layout.StaticRoot = _static;

            DefineWalkable();
            _layout.Terrain = WorldGround.BuildTerrain(root, _layout.Walkable, _look, Paint);
            BuildWater();
            BuildHearthmoor();
            BuildRoad();
            BuildBrindlecross();
            BuildHall();
            BuildInterior();
            BuildQuarryPath();
            BuildOutpost();
            BuildStreamPath();
            BuildLanternmere();
            BuildDuskhollow();
            BuildIronbell();
            BuildCrownhold();
            BuildPickups();
            Scatter();
            return _layout;
        }

        // ------------------------------------------------------------------ where you can walk

        private void DefineWalkable()
        {
            var w = _layout.Walkable;
            w.Rect(-21, 21, -15, 22);                                // Hearthmoor
            w.Circle(-10, 44, 5f);                                   // Wren's camp
            w.Capsule(new Vector2(-1, 44), new Vector2(-7, 44), 2.2f);
            for (int i = 0; i < RoadPoints.Length - 1; i++) w.Capsule(RoadPoints[i], RoadPoints[i + 1], 3.0f);
            // The stream either side of the bridge.
            w.Hole(-400, -7.6f, WorldGround.StreamZ - 2.6f, WorldGround.StreamZ + 2.6f);
            w.Hole(-3.4f, 400, WorldGround.StreamZ - 2.6f, WorldGround.StreamZ + 2.6f);
            w.Rect(-25, 25, 113, 158);                               // Brindlecross
            w.Rect(InteriorX - 5.3f, InteriorX + 5.3f, -4.3f, 4.2f); // inside the Champion's Hall
            for (int i = 0; i < QuarryPath.Length - 1; i++) w.Capsule(QuarryPath[i], QuarryPath[i + 1], 2.6f);
            w.Rect(62, 100, 150, 193);                               // the Outpost plateau
            w.Capsule(new Vector2(84, 191), new Vector2(84, 199), 2.4f); // ramp into the quarry pit
            w.Circle(WorldGround.PitCenter.x, WorldGround.PitCenter.y, 7.5f);
            w.Capsule(new Vector2(96, 171), new Vector2(99.5f, 171), 1.8f); // up the lookout knoll
            w.Circle(WorldGround.KnollCenter.x, WorldGround.KnollCenter.y, 3.2f);
            DefineNewWalkable(w);
        }

        // ------------------------------------------------------------------ terrain paint

        private static float DistToPolyline(Vector2 p, Vector2[] pts)
        {
            float best = float.MaxValue;
            for (int i = 0; i < pts.Length - 1; i++)
            {
                var ab = pts[i + 1] - pts[i];
                float t = Mathf.Clamp01(Vector2.Dot(p - pts[i], ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, (pts[i] + ab * t - p).magnitude);
            }
            return best;
        }

        private static float Band(float d, float inner, float soft) => 1f - WorldGround.Smooth01((d - inner) / soft);

        /// <summary>Terrain layer weights: grass, meadow, dirt, stone, rock.</summary>
        private float[] Paint(float x, float z, float slope)
        {
            var p = new Vector2(x, z);
            float meadow = Mathf.Clamp01((Mathf.PerlinNoise(x * 0.05f + 3.1f, z * 0.05f + 7.7f) - 0.45f) * 2.2f);
            float dirt = Band(DistToPolyline(p, RoadPoints), 1.9f, 1.4f);
            dirt = Mathf.Max(dirt, Band(DistToPolyline(p, QuarryPath), 1.8f, 1.2f));
            foreach (var path in NewPaths) dirt = Mathf.Max(dirt, Band(DistToPolyline(p, path), 1.8f, 1.2f));
            float lakeD = Vector2.Distance(p, WorldGround.LakeCenter);
            dirt = Mathf.Max(dirt, Band(lakeD, WorldGround.LakeRadius + 2.5f, 2f) * (1f - Band(lakeD, WorldGround.LakeRadius - 4f, 2f))); // beach
            dirt = Mathf.Max(dirt, Band(Vector2.Distance(p, new Vector2(-62, 211)), 12f, 5f) * 0.3f);         // Duskhollow floor
            dirt = Mathf.Max(dirt, Band(Vector2.Distance(p, new Vector2(0, 4)), 7.5f, 2f));        // Hearthmoor square
            dirt = Mathf.Max(dirt, Band(Vector2.Distance(p, new Vector2(-10, 44)), 4f, 2f));       // Wren's camp
            dirt = Mathf.Max(dirt, Band(Mathf.Abs(z - 44) + Mathf.Max(0, Mathf.Abs(x + 4) - 3f), 1.2f, 1f));
            float stone = Band(Vector2.Distance(p, new Vector2(0, 134)), 11.5f, 1.5f);                // Brindlecross plaza
            stone = Mathf.Max(stone, Band(Vector2.Distance(p, new Vector2(92, 79)), 12f, 2f));        // Ironbell square
            stone = Mathf.Max(stone, x > -4.5f && x < 4.5f && z > 214 && z < 232 ? 0.9f : 0f);        // Crownhold: gate to the ring
            stone = Mathf.Max(stone, Band(Vector2.Distance(p, new Vector2(0, 240)), 11f, 1.5f) * 0.8f);  // around the arena
            stone = Mathf.Max(stone, Band(Vector2.Distance(p, new Vector2(-62, 225)), 6.5f, 1f));     // the ring of stones
            stone = Mathf.Max(stone, x > -3 && x < 3 && z > 140 && z < 160 ? 1f : 0f);                // the avenue to the hall
            float plateau = x > 61 && x < 101 && z > 149 && z < 194 ? 1f : 0f;
            float moor = x > 62 && z < 146 ? WorldGround.Smooth01((x - 62f) / 8f) : 0f;
            meadow = Mathf.Max(meadow, moor * 0.8f); // heather on the eastern moor
            // Quarry dust: packed earth with stone chips.
            dirt = Mathf.Max(dirt, plateau * (0.55f + 0.45f * Mathf.PerlinNoise(x * 0.15f, z * 0.15f)));
            stone = Mathf.Max(stone, plateau * 0.5f * Mathf.PerlinNoise(x * 0.11f + 5f, z * 0.11f + 9f));
            float pit = Band(Vector2.Distance(p, WorldGround.PitCenter), 12f, 4f) * WorldGround.Highland(x);
            float rock = WorldGround.Smooth01((slope - 34f) / 10f);
            rock = Mathf.Max(rock, pit * 0.8f);
            rock = Mathf.Max(rock, WorldGround.Highland(x) * WorldGround.Smooth01((slope - 24f) / 10f));
            float bank = WorldGround.StreamMask(x, z);
            dirt = Mathf.Max(dirt, bank * 0.8f);
            float grass = Mathf.Max(0.02f, 1f - Mathf.Max(dirt, Mathf.Max(stone, rock)));
            return new[] { grass * (1f - meadow), grass * meadow, dirt * (1f - rock), stone * (1f - rock) + pit * 0.3f, rock };
        }

        private void BuildWater()
        {
            float y = WorldGround.WaterLevel;
            var water = _kit.Box("Stream", _static, new Vector3(-24.5f, y, WorldGround.StreamZ), new Vector3(99, 0.05f, 5.2f), new Color(0.2f, 0.38f, 0.44f));
            var m = water.GetComponent<Renderer>().sharedMaterial;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.92f);
            // Mirrorwater, the lake at Lanternmere (D-038).
            float d = (WorldGround.LakeRadius + 1.5f) * 2f;
            _kit.Prim(PrimitiveType.Cylinder, "Lake", _static, new Vector3(WorldGround.LakeCenter.x, y, WorldGround.LakeCenter.y), new Vector3(d, 0.025f, d), new Color(0.2f, 0.38f, 0.44f));
        }

        // ------------------------------------------------------------------ buildings, trees, props

        /// <summary>
        /// A building: the pack prefab (uniform <see cref="HouseScale"/>, door on local +z) or the primitive house.
        /// Imported art-set models (D-026) still win when assigned. Solid either way.
        /// </summary>
        private Transform House(string area, string name, string prefab, Vector2 at, float yaw, float width, float depth, float height, Color wall, Color roof, float scale = HouseScale)
        {
            var key = area + "/" + name;
            _layout.BuildingKeys.Add(key);
            var pos = G(at.x, at.y);
            _clear.Add(new Vector3(at.x, at.y, Mathf.Max(width, depth) * 0.75f + 2f));
            var slot = _art != null ? _art.Building(key) : null;
            if (slot != null)
            {
                var root = new GameObject(name).transform;
                root.SetParent(_models, false);
                root.localPosition = pos;
                root.localRotation = Quaternion.Euler(0, yaw, 0);
                var footprint = slot.size > 0 ? new Vector2(slot.size, slot.size * depth / width) : new Vector2(width, depth);
                var model = _kit.PlaceModel(slot, root, 0, footprint, out var size);
                var col = model.gameObject.AddComponent<BoxCollider>();
                var s = model.localScale.x;
                col.size = size / s;
                col.center = new Vector3(0, size.y / 2f, 0) / s;
                return root;
            }
            var pf = P(prefab);
            if (pf == null) return WorldPieces.House(_kit, _static, name, pos, yaw, width, depth, height, wall, roof);
            var holder = new GameObject(name).transform;
            holder.SetParent(_models, false);
            holder.localPosition = pos;
            holder.localRotation = Quaternion.Euler(0, yaw, 0);
            var go = _kit.PlacePrefab(pf, holder, Vector3.zero, 0, scale);
            AddOccluder(go);
            // Solid above ground (the stone foundations go below it).
            var b = WorldKit.LocalBounds(go.transform, holder);
            var box = holder.gameObject.AddComponent<BoxCollider>();
            float top = Mathf.Max(2f, b.max.y);
            box.center = new Vector3(b.center.x, top / 2f, b.center.z);
            box.size = new Vector3(b.size.x * 0.9f, top, b.size.z * 0.9f);
            return holder;
        }

        private static readonly string[] BroadleafTrees =
        {
            "SM_Env_Tree_Round_02", "SM_Env_Tree_Round_03", "SM_Env_Tree_Round_04", "SmallTreeRound_01", "SmallTreeRound_02",
            "SmallTreeRound_03", "TreeLargeStylized_01", "SM_Env_Tree_Large_01", "PT_Fruit_Tree_01_green",
        };
        private static readonly string[] PineTrees = { "TreePine_VarA", "TreePine_VarB", "TreePine_VarC", "SM_Env_Tree_Thin_01", "SM_Env_Tree_Thin_03", "SM_Env_Tree_Thin_05", "PT_Pine_Tree_03_green" };
        private static readonly string[] FruitTrees = { "PT_Fruit_Tree_01_apples", "PT_Fruit_Tree_01_pears", "PT_Fruit_Tree_01_plums", "PT_Fruit_Tree_01_green" };
        private static readonly string[] Bushes =
        {
            "SM_Env_Bush_01", "SM_Env_Bush_02", "SM_Env_Bush_04", "SM_Env_Bush_05", "Bush_02", "SM_Env_Bush_Cluster_03",
            "SM_Env_Bush_Cluster_04", "PT_Generic_Shrub_01_green",
        };

        private static readonly string[] Buildings =
        {
            "Preset_House_02_A", "Preset_House_02_B", "Preset_House_02_C", "Preset_House_03", "Preset_House_Windmill_01", "Preset_House_09_A",
            "Preset_House_09_B", "Preset_Shelter_01", "Preset_Shelter_02", "Preset_Hut_01", "Preset_Hut_02", "Preset_Church_01_A",
            "Preset_Tavern_01", "Preset_House_01_A", "Preset_Tower_01", "Preset_Stables_01", "Preset_House_Archway_01", "Preset_House_04",
            "Preset_Blacksmith_01",
        };
        private static readonly string[] Props =
        {
            "PT_Generic_Rock_01", "PT_Menhir_Rock_02", "PT_Ore_Rock_01", "PT_Pine_Tree_03_logs", "PT_Poppy_02", "PT_Modular_Fence_Wood_01",
            "Clock_03", "Picture_17", "Picture_21", "Light_05", "Armchair_18", "Book_03",
            "Knight", "Barbarian", "Mage", "Rogue", "RogueHooded",
        };
        /// <summary>Ground plants: a handful of the pack's clumps is plenty of variety.</summary>
        public const int PlantVariants = 16;

        /// <summary>
        /// Every pack prefab the world uses (D-033). The world look lists only these, so builds don't carry the rest of
        /// the packs (a referenced prefab is always built, used or not).
        /// </summary>
        public static IEnumerable<string> PrefabKeysUsed
        {
            get
            {
                foreach (var k in Buildings) yield return k;
                foreach (var k in Props) yield return k;
                foreach (var set in new[] { BroadleafTrees, PineTrees, FruitTrees, Bushes }) foreach (var k in set) yield return k;
                for (int i = 1; i <= PlantVariants; i++) yield return "Village_Scatter_" + i.ToString("00");
                foreach (var k in NewPrefabKeys) yield return k;
            }
        }

        private string Pick(string[] keys) => keys[Mathf.Min(keys.Length - 1, (int)(Rand() * keys.Length))];

        /// <summary>A tree on the terrain: a pack tree of the given family, or the primitive tree.</summary>
        private void Tree(float x, float z, string[] family, float scale = 1f)
        {
            float y = T(x, z);
            var pf = P(Pick(family));
            if (pf == null) { WorldPieces.Tree(_kit, _static, new Vector3(x, y, z), scale * (0.9f + Rand() * 0.5f), (int)(x * 7 + z)); return; }
            bool polytope = pf.name.StartsWith("PT_");
            float s = scale * (polytope ? 1.1f : 0.62f) * (0.8f + Rand() * 0.45f);
            var go = _kit.PlacePrefab(pf, _models, new Vector3(x, y - 0.2f, z), Rand() * 360f, s);
            foreach (var c in go.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            Solid(x, y, z, 0.4f, 3f);
            AddOccluder(go);
        }

        /// <summary>An invisible blocker (tree trunk, rock, stone) so wandering players walk around things (D-038).</summary>
        private void Solid(float x, float y, float z, float radius, float height)
        {
            if (_solids == null)
            {
                _solids = new GameObject("Solids").transform;
                _solids.SetParent(_layout.Root, false);
            }
            var go = new GameObject("Solid");
            go.transform.SetParent(_solids, false);
            go.transform.localPosition = new Vector3(x, y, z);
            var cap = go.AddComponent<CapsuleCollider>();
            cap.radius = radius;
            cap.height = Mathf.Max(height, radius * 2f);
            cap.center = new Vector3(0, cap.height / 2f, 0);
        }

        private Transform _solids;

        /// <summary>Remembers something tall the overhead camera may need to see through.</summary>
        private void AddOccluder(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            if (b.size.y < 2.5f) return;
            _layout.Occluders.Add(new Occluder { Renderers = rs, Bounds = b });
        }

        private void Bush(float x, float z, float scale = 1f)
        {
            float y = T(x, z);
            var pf = P(Pick(Bushes));
            if (pf == null) { WorldPieces.Bush(_kit, _static, new Vector3(x, y, z), 0.8f * scale); return; }
            _kit.PlacePrefab(pf, _models, new Vector3(x, y - 0.1f, z), Rand() * 360f, scale * (0.45f + Rand() * 0.3f) * (pf.name.StartsWith("PT_") ? 1.4f : 1f));
        }

        private void Boulder(float x, float z, float size)
        {
            float y = T(x, z);
            var pf = P(Rand() < 0.75f ? "PT_Generic_Rock_01" : "PT_Menhir_Rock_02");
            if (pf == null) { WorldPieces.Rock(_kit, _static, new Vector3(x, y, z), size); return; }
            float s = pf.name.Contains("Menhir") ? size * 0.9f : size * 7f;
            var go = _kit.PlacePrefab(pf, _models, new Vector3(x, y - 0.15f * size, z), Rand() * 360f, s);
            _kit.Restyle(go, new Color(0.66f, 0.63f, 0.58f)); // warm grey stone, no glowing runes
            if (size >= 0.55f) Solid(x, y, z, 0.45f * size, 1.2f * size);
        }

        private void Plants(float x, float z)
        {
            var pf = P("Village_Scatter_" + (1 + (int)(Rand() * PlantVariants)).ToString("00"));
            if (pf == null) return;
            _kit.PlacePrefab(pf, _models, new Vector3(x, T(x, z) - 0.05f, z), Rand() * 360f, 0.55f + Rand() * 0.3f);
        }

        private void Flowers(float x, float z, int seed)
        {
            var pf = P("PT_Poppy_02");
            if (pf == null) { WorldPieces.Flowers(_kit, _static, G(x, z), seed); return; }
            for (int i = 0; i < 4; i++)
                _kit.PlacePrefab(pf, _models, G(x + (Rand() - 0.5f) * 1.6f, z + (Rand() - 0.5f) * 1.6f), Rand() * 360f, 1f + Rand() * 0.6f);
        }

        /// <summary>A fence line: pack fence segments (2.2 m) or the primitive rail fence.</summary>
        private void Fence(Vector2 a, Vector2 b)
        {
            var pf = P("PT_Modular_Fence_Wood_01");
            var dir = b - a;
            float len = dir.magnitude;
            // A fence is a real barrier (D-038): one thin blocker along its length.
            var bar = new GameObject("FenceBlock");
            bar.transform.SetParent(_layout.Root, false);
            bar.transform.localPosition = G((a.x + b.x) / 2f, (a.y + b.y) / 2f);
            bar.transform.localRotation = Quaternion.Euler(0, Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg, 0);
            var box = bar.AddComponent<BoxCollider>();
            box.center = new Vector3(0, 1f, 0);
            box.size = new Vector3(0.3f, 2f, len);
            if (pf == null) { WorldPieces.Fence(_kit, _static, G(a.x, a.y), G(b.x, b.y)); return; }
            int n = Mathf.Max(1, Mathf.RoundToInt(len / 2.2f));
            float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg - 90f;
            for (int i = 0; i < n; i++)
            {
                var p = a + dir * ((i + 0.5f) / n);
                var go = _kit.PlacePrefab(pf, _models, G(p.x, p.y), yaw, len / n / 2.2f);
                foreach (var c in go.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            }
        }

        private void Furniture(string key, Transform parent, Vector3 localPos, float yaw, float scale = 1f)
        {
            var pf = P(key);
            if (pf == null) return;
            var go = _kit.PlacePrefab(pf, parent, localPos, yaw, scale);
            foreach (var c in go.GetComponentsInChildren<Collider>()) Object.Destroy(c);
        }

        // ------------------------------------------------------------------ Hearthmoor (start)

        private void BuildHearthmoor()
        {
            WorldPieces.Well(_kit, _static, G(0, 4));
            House(Areas.Hearthmoor, "Home", "Preset_House_02_A", new Vector2(0, -13), 0, 7, 5, 3.4f, Palette.PlasterWarm, Palette.RoofRed);
            House(Areas.Hearthmoor, "Bakery", "Preset_House_03", new Vector2(-14.5f, 8), 90, 6, 5, 3.2f, Palette.Plaster, Palette.RoofBrown);
            House(Areas.Hearthmoor, "Mill", "Preset_House_Windmill_01", new Vector2(14.5f, 10), -90, 6, 6, 4f, Palette.Plaster, Palette.RoofBlue);
            House(Areas.Hearthmoor, "Cottage", "Preset_House_09_A", new Vector2(-14.5f, -6), 90, 5, 5, 3f, Palette.PlasterWarm, Palette.RoofGreen);
            House(Areas.Hearthmoor, "Barn", "Preset_Shelter_01", new Vector2(14.5f, -7), -90, 7, 5, 3.6f, new Color(0.7f, 0.35f, 0.28f), Palette.RoofBrown);
            WorldPieces.Bench(_kit, _static, G(-6, 9), 0);
            WorldPieces.Lamp(_kit, _static, G(-4, 14));
            WorldPieces.Lamp(_kit, _static, G(4, 14));
            for (int i = 0; i < 8; i++) Flowers(-18 + i * 5.1f, 18.5f, i);
            Fence(new Vector2(-22, -16), new Vector2(22, -16));
            Fence(new Vector2(-22, -16), new Vector2(-22, 20));
            Fence(new Vector2(22, -16), new Vector2(22, 20));
            // Gran's orchard beyond the east fence.
            for (int i = 0; i < 6; i++) Tree(27 + (i % 2) * 5f, -10 + i * 4.5f, FruitTrees, 0.9f);
            AddSign(new Vector2(2.5f, 19.5f), "North Road", "NORTH ROAD\nBrindlecross - a morning's walk north.\nHome of a real Wheels Champion!");

            _layout.Spawn = G(0, -7.5f);
            _layout.SpawnYaw = 0;

            AddNpc("Gran Oddly", "retired player", new Vector2(-6, 10.2f), 180,
                new CharacterLook("Mage", hue: 60, hat: true, cape: false, prop: "Spellbook", height: 1.55f, saturation: 0.7f),
                new PersonLook { Cloth = new Color(0.55f, 0.35f, 0.55f), Accent = Palette.Plaster, Hair = new Color(0.85f, 0.85f, 0.88f), Hat = HatKind.Kerchief, HatColor = new Color(0.8f, 0.4f, 0.45f), Height = 0.9f },
                EncounterCatalog.Get(EncounterCatalog.Gran), null);
            AddNpc("Kit", "miller's kid", new Vector2(7, 2), 250,
                new CharacterLook("Rogue", hue: 150, cape: false, height: 1.25f),
                new PersonLook { Cloth = new Color(0.35f, 0.6f, 0.4f), Accent = Palette.Wood, Hat = HatKind.Cap, HatColor = new Color(0.8f, 0.3f, 0.25f), Height = 0.75f },
                null, new[]
                {
                    "Are you really going to Brindlecross? The Champion there has NEVER lost at his own table!",
                    "Gran taught everybody here how to play Wheels. You should play her first. She doesn't even play for money.",
                });
            AddNpc("Hollis", "baker", new Vector2(-10.5f, 5), 90,
                new CharacterLook("Barbarian", hue: 30, hat: false, cape: false, prop: "Mug", saturation: 0.35f, brightness: 1.25f),
                new PersonLook { Cloth = Palette.Plaster, Accent = new Color(0.85f, 0.8f, 0.7f), Hat = HatKind.WideBrim, HatColor = Color.white },
                null, new[]
                {
                    "Fresh bread for the road? Ha, I'm only teasing. I'd never let a loaf leave this village. Well. Almost never.",
                    "Follow the road north. It crosses a stream halfway. Can't miss the bridge.",
                });
            AddNpc("Marta", "miller", new Vector2(10, 12), -100,
                new CharacterLook("Mage", hue: 190, hat: false, cape: false, height: 1.65f),
                new PersonLook { Cloth = new Color(0.5f, 0.55f, 0.7f), Accent = Palette.Plaster, Hat = HatKind.Kerchief, HatColor = Color.white },
                null, new[]
                {
                    "The mill turns whether I watch it or not. Mostly not.",
                    "Gran's been playing Wheels since before the mill was built. She says it keeps her sharp.",
                });
            AddNpc("Old Tam", "farmer", new Vector2(-10, -6), 90,
                new CharacterLook("Barbarian", hue: 80, hat: true, cape: false, height: 1.6f, saturation: 0.6f, brightness: 0.9f),
                new PersonLook { Cloth = new Color(0.45f, 0.4f, 0.25f), Accent = Palette.Wood, Hat = HatKind.WideBrim, HatColor = new Color(0.6f, 0.5f, 0.3f) },
                null, new[]
                {
                    "Sixty years farming this valley. Never once beaten Gran at Wheels.",
                    "My Peg keeps the Champion's Hall up in Brindlecross. Proud of her, I am.",
                });
        }

        // ------------------------------------------------------------------ The North Road

        private void BuildRoad()
        {
            // Wren's camp in a clearing west of the road.
            WorldPieces.Campfire(_kit, _static, G(-9, 43));
            _kit.Prism("Tent", _static, G(-13, 46.5f), new Vector3(3f, 2.2f, 3.5f), new Color(0.62f, 0.52f, 0.38f), 20);
            var logs = P("PT_Pine_Tree_03_logs");
            if (logs != null) _kit.PlacePrefab(logs, _models, G(-7.2f, 45.5f), 70, 0.9f);
            _clear.Add(new Vector3(-10, 44, 7));

            // The bridge over the Willow Stream (the road crosses at z = 62).
            var bridge = new GameObject("Bridge").transform;
            bridge.SetParent(_static, false);
            bridge.localPosition = G(-5.5f, WorldGround.StreamZ) + new Vector3(0, -0.33f, 0);
            _kit.Box("Deck", bridge, new Vector3(0, 0.22f, 0), new Vector3(4.4f, 0.2f, 7.4f), Palette.Wood);
            for (int i = 0; i < 8; i++) _kit.Box("Plank", bridge, new Vector3(0, 0.34f, -3.5f + i), new Vector3(4.4f, 0.04f, 0.12f), Palette.WoodDark);
            foreach (float x in new[] { -2.2f, 2.2f })
            {
                _kit.Box("Rail", bridge, new Vector3(x, 0.95f, 0), new Vector3(0.14f, 0.14f, 7.4f), Palette.WoodDark);
                for (int i = 0; i < 4; i++) _kit.Box("RailPost", bridge, new Vector3(x, 0.6f, -3.3f + i * 2.2f), new Vector3(0.16f, 0.9f, 0.16f), Palette.WoodDark);
            }
            AddSign(new Vector2(3.2f, 57.5f), "Stream", "THE WILLOW STREAM\nBrindlecross: keep north.\nHearthmoor: back south.");
            // Stepping stones and reeds along the banks.
            for (int i = 0; i < 18; i++)
            {
                float x = -60 + i * 4.6f + Rand() * 2f;
                if (x > -10 && x < 0) continue;
                Boulder(x, WorldGround.StreamZ + (Rand() < 0.5f ? -3.3f : 3.3f), 0.35f + Rand() * 0.3f);
            }
            AddNpc("Wren", "wandering tinker", new Vector2(-11.5f, 42.5f), 60,
                new CharacterLook("RogueHooded", hue: 0, prop: "Knife", height: 1.7f),
                new PersonLook { Cloth = new Color(0.5f, 0.42f, 0.3f), Accent = new Color(0.3f, 0.5f, 0.45f), Hat = HatKind.Hood, HatColor = new Color(0.35f, 0.45f, 0.35f) },
                EncounterCatalog.Get(EncounterCatalog.Wren), null);
        }

        // ------------------------------------------------------------------ Brindlecross

        private void BuildBrindlecross()
        {
            WorldPieces.Fountain(_kit, _static, G(0, 134));
            House(Areas.Brindlecross, "Chandlery", "Preset_House_09_B", new Vector2(-19.5f, 124), 90, 7, 6, 3.6f, Palette.PlasterWarm, Palette.RoofRed);
            House(Areas.Brindlecross, "FisherHut", "Preset_Hut_01", new Vector2(19.5f, 121), -90, 6, 5, 3.2f, Palette.Plaster, Palette.RoofBlue);
            House(Areas.Brindlecross, "Infirmary", "Preset_Church_01_A", new Vector2(-19.5f, 142), 90, 7, 6, 3.8f, Color.white, Palette.RoofGreen);
            House(Areas.Brindlecross, "Inn", "Preset_Tavern_01", new Vector2(19.5f, 146), -90, 8, 7, 4.4f, Palette.PlasterWarm, Palette.RoofBrown);
            House(Areas.Brindlecross, "Cottage", "Preset_House_02_B", new Vector2(-12, 153.5f), 180, 6, 5, 3.2f, Palette.Plaster, Palette.RoofRed);
            House(Areas.Brindlecross, "Cottage2", "Preset_House_02_C", new Vector2(12, 153.5f), 180, 6, 5, 3.2f, Palette.Plaster, Palette.RoofBlue);
            // Houses beyond the walkable edge make the town feel bigger than the part you walk.
            House(Areas.Brindlecross, "WestHouse", "Preset_House_01_A", new Vector2(-33, 132), 90, 8, 7, 4f, Palette.Plaster, Palette.RoofRed);
            House(Areas.Brindlecross, "WestTower", "Preset_Tower_01", new Vector2(-32, 148), 90, 7, 7, 8f, Palette.Plaster, Palette.RoofBlue);
            House(Areas.Brindlecross, "Stables", "Preset_Stables_01", new Vector2(33, 116), -90, 10, 7, 4f, Palette.PlasterWarm, Palette.RoofBrown);

            WorldPieces.Stall(_kit, _static, G(-8, 119), 30, new Color(0.8f, 0.25f, 0.25f));
            WorldPieces.Stall(_kit, _static, G(8, 119), -30, new Color(0.25f, 0.45f, 0.8f));
            foreach (var p in new[] { new Vector2(-6, 128), new Vector2(6, 128), new Vector2(-6, 141), new Vector2(6, 141) })
                WorldPieces.Lamp(_kit, _static, G(p.x, p.y));
            WorldPieces.Bench(_kit, _static, G(-9, 134), 90);
            WorldPieces.Bench(_kit, _static, G(9, 134), 90);
            for (int i = 0; i < 10; i++) Flowers(-22 + i * 4.9f, 157, i + 3);
            // Mira's beehives behind the chandlery.
            for (int i = 0; i < 3; i++)
            {
                var hive = G(-24f, 115.5f + i * 1.6f);
                _kit.Box("HiveStand", _static, hive + new Vector3(0, 0.25f, 0), new Vector3(0.9f, 0.5f, 0.9f), Palette.WoodDark);
                _kit.Prim(PrimitiveType.Sphere, "Hive", _static, hive + new Vector3(0, 0.85f, 0), new Vector3(0.8f, 0.9f, 0.8f), new Color(0.85f, 0.7f, 0.35f));
            }
            AddSign(new Vector2(-3.5f, 113.8f), "Welcome", "BRINDLECROSS\nHome of Corvin Vale,\nWheels Champion of the valley.");
            AddSign(new Vector2(23, 133), "Quarry Path", "THE QUARRY PATH\nStonemasons' Outpost: up the hill, east.\nThey play for real stakes up there.");

            AddNpc("Bram", "town crier", new Vector2(4, 118), 200,
                new CharacterLook("Knight", hue: 25, hat: false, cape: true, height: 1.8f, saturation: 0.9f),
                new PersonLook { Cloth = new Color(0.7f, 0.55f, 0.25f), Accent = Palette.WoodDark, Hat = HatKind.WideBrim, HatColor = new Color(0.25f, 0.2f, 0.18f) },
                null, new[]
                {
                    "Welcome to Brindlecross! Everyone here plays Wheels. EVERYONE.",
                    "Mira, Tobin and Sister Halvey will all give you a game, for a few coins. Ada sells charms at her stall if you want an edge.",
                    "The Champion, Corvin Vale, lives in the great hall at the top of the town. And they say the stonemasons up the quarry path play for serious money.",
                });
            AddNpc("Mira Tallow", "candlemaker", new Vector2(-14, 124), 90,
                new CharacterLook("Mage", hue: 0, hat: false, cape: true, height: 1.7f),
                new PersonLook { Cloth = new Color(0.75f, 0.35f, 0.3f), Accent = Palette.Plaster, Hair = new Color(0.55f, 0.25f, 0.12f), Hat = HatKind.Kerchief, HatColor = new Color(0.95f, 0.85f, 0.4f) },
                EncounterCatalog.Get(EncounterCatalog.Mira), null);
            AddNpc("Tobin Reed", "fisher", new Vector2(14, 124), 270,
                new CharacterLook("Rogue", hue: 200, cape: false, prop: "Throwable", height: 1.75f),
                new PersonLook { Cloth = new Color(0.3f, 0.45f, 0.55f), Accent = new Color(0.55f, 0.45f, 0.3f), Hat = HatKind.WideBrim, HatColor = new Color(0.6f, 0.55f, 0.35f) },
                EncounterCatalog.Get(EncounterCatalog.Tobin), null);
            AddNpc("Sister Halvey", "healer", new Vector2(-14, 141), 90,
                new CharacterLook("Mage", hue: 230, hat: false, cape: true, prop: "Spellbook_open", height: 1.7f, saturation: 0.55f, brightness: 1.3f),
                new PersonLook { Cloth = new Color(0.92f, 0.92f, 0.95f), Accent = new Color(0.3f, 0.55f, 0.75f), Hat = HatKind.Hood, HatColor = new Color(0.3f, 0.5f, 0.75f), Robe = true },
                EncounterCatalog.Get(EncounterCatalog.Halvey), null);
            AddNpc("Peg", "hall steward", new Vector2(4.2f, 155), 200,
                new CharacterLook("Knight", hue: 330, hat: false, cape: true, height: 1.65f),
                new PersonLook { Cloth = Palette.Banner, Accent = Palette.Gold, Hat = HatKind.Cap, HatColor = Palette.Banner },
                null, new[]
                {
                    "This is the Champion's Hall. Master Vale keeps his table ready day and night.",
                    "Just walk in and take the empty chair. He'll know what you want. Bring coin: he plays for twenty-five.",
                });
            AddNpc("Ada Pell", "stallkeeper", new Vector2(-11.2f, 120f), 180,
                new CharacterLook("Barbarian", hue: 300, hat: false, cape: false, height: 1.6f, saturation: 0.8f),
                new PersonLook { Cloth = new Color(0.6f, 0.3f, 0.5f), Accent = Palette.Gold, Hat = HatKind.Kerchief, HatColor = new Color(0.9f, 0.5f, 0.3f) },
                null, new[] { "Charms and wheels, dear. Have a look." }, ShopCatalog.AdasStall);
        }

        // ------------------------------------------------------------------ The Champion's Hall (outside)

        private void BuildHall()
        {
            var at = new Vector2(0, 167);
            _layout.BuildingKeys.Add(Areas.Hall + "/Hall");
            var pf = P("Preset_House_Archway_01");
            var hall = new GameObject("ChampionsHall").transform;
            hall.SetParent(pf != null ? _models : _static, false);
            hall.localPosition = G(at.x, at.y);
            _clear.Add(new Vector3(at.x, at.y, 14));
            if (pf != null)
            {
                _kit.PlacePrefab(pf, hall, Vector3.zero, 0, 0.88f);
                var box = hall.gameObject.AddComponent<BoxCollider>();
                box.center = new Vector3(0, 7, 0);
                box.size = new Vector3(14f, 14f, 12.5f);
            }
            else
            {
                var wall = new Color(0.85f, 0.8f, 0.72f);
                _kit.Box("Walls", hall, new Vector3(0, 3.2f, 0), new Vector3(16, 6.4f, 12), wall, solid: true);
                _kit.Box("Base", hall, new Vector3(0, 0.25f, 0), new Vector3(16.6f, 0.5f, 12.6f), Palette.StoneDark);
                _kit.Prism("Roof", hall, new Vector3(0, 6.4f, 0), new Vector3(13.2f, 4f, 17.2f), new Color(0.3f, 0.3f, 0.45f), 90);
                _kit.Box("Door", hall, new Vector3(0, 1.6f, -6.03f), new Vector3(2.4f, 3.2f, 0.1f), Palette.WoodDark);
            }
            foreach (float x in new[] { -4.2f, 4.2f })
            {
                _kit.Box("Banner", hall, new Vector3(x, 3.2f, -7.4f), new Vector3(1.3f, 3.2f, 0.06f), Palette.Banner);
                _kit.Box("BannerCrown", hall, new Vector3(x, 3.9f, -7.45f), new Vector3(0.6f, 0.45f, 0.04f), Palette.Gold);
            }
            _kit.Label("HallTitle", hall, new Vector3(0, 9.5f, -7.5f), "THE CHAMPION'S HALL", 60, Palette.Gold, 0.012f);

            var door = new GameObject("HallDoor").AddComponent<Door>();
            door.transform.SetParent(_dynamic, false);
            door.transform.localPosition = G(0, 158.6f);
            door.Label = "Enter the Champion's Hall";
            door.Target = new Vector3(InteriorX, 0, -3.2f);
            door.TargetYaw = 0;
            door.AreaName = Areas.Hall;
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
            Furniture("Clock_03", room, new Vector3(-5.6f, 0, 1.5f), 90);
            Furniture("Picture_17", room, new Vector3(1.9f, 2.8f, 4.98f), 180, 1.3f);
            Furniture("Picture_21", room, new Vector3(-1.9f, 2.9f, 4.98f), 180, 1.3f);
            Furniture("Light_05", room, new Vector3(5.4f, 0, -3.8f), 0);
            Furniture("Light_05", room, new Vector3(-5.4f, 0, -3.8f), 0);
            Furniture("Armchair_18", room, new Vector3(4.6f, 0, -1.5f), -70);
            Furniture("Book_03", room, new Vector3(-5.1f, 1.95f, 3.5f), 90);
            WorldPieces.GameTable(_kit, room, new Vector3(0, 0, 0.8f), 1f);
            WorldPieces.Chair(_kit, room, new Vector3(0, 0, 2.3f), 180);
            WorldPieces.Chair(_kit, room, new Vector3(0, 0, -0.7f), 0);
            _layout.TableFocus = new Vector3(InteriorX, 1f, 0.8f);

            var champion = AddNpc("Corvin Vale", "Champion of Brindlecross", new Vector2(InteriorX, 2.3f), 180,
                new CharacterLook("Knight", hue: 210, hat: false, cape: true, height: 1.85f, saturation: 0.8f, brightness: 0.8f),
                new PersonLook { Cloth = new Color(0.2f, 0.2f, 0.32f), Accent = Palette.Gold, Hair = new Color(0.15f, 0.12f, 0.12f), Hat = HatKind.Circlet, Robe = true, Height = 1.05f },
                EncounterCatalog.Get(EncounterCatalog.Champion), null, seated: true);
            champion.Radius = 0f; // talk to him by sitting down
            _layout.ChampionChair = AddChair(champion, new Vector3(InteriorX, 0, -1.2f), new Vector3(InteriorX, 0, -0.7f), _layout.TableFocus);

            var exit = new GameObject("HallExit").AddComponent<Door>();
            exit.transform.SetParent(_dynamic, false);
            exit.transform.localPosition = new Vector3(InteriorX, 0, -4.1f);
            exit.Label = "Leave the hall";
            exit.Target = new Vector3(0, 0, 156.8f);
            exit.TargetYaw = 180;
            exit.AreaName = Areas.Brindlecross;
            exit.Radius = 1.6f;
            _layout.Interactables.Add(exit);
            _layout.HallExit = exit;
        }

        // ------------------------------------------------------------------ The Quarry Path

        private void BuildQuarryPath()
        {
            // Rope posts along the steep side of the climb.
            for (int i = 1; i < QuarryPath.Length - 1; i++)
            {
                var a = QuarryPath[i];
                var dir = (QuarryPath[i + 1] - a).normalized;
                var side = new Vector2(dir.y, -dir.x) * 3.1f;
                var p = a + side;
                _kit.Box("RopePost", _static, G(p.x, p.y) + new Vector3(0, 0.6f, 0), new Vector3(0.18f, 1.2f, 0.18f), Palette.WoodDark);
            }
            for (int i = 0; i < 10; i++)
            {
                float t = i / 9f;
                int seg = Mathf.Min(QuarryPath.Length - 2, (int)(t * (QuarryPath.Length - 1)));
                var p = Vector2.Lerp(QuarryPath[seg], QuarryPath[seg + 1], t * (QuarryPath.Length - 1) - seg);
                var off = new Vector2(Rand() - 0.5f, Rand() - 0.5f).normalized * (4.5f + Rand() * 3f);
                Boulder(p.x + off.x, p.y + off.y, 0.6f + Rand() * 0.6f);
            }
            AddSign(new Vector2(62.5f, 155.5f), "Outpost", "STONEMASONS' OUTPOST\nQuarry, forge and the ledge table.\nMind the drop.");
        }

        // ------------------------------------------------------------------ Stonemasons' Outpost

        private void BuildOutpost()
        {
            House(Areas.Outpost, "Lodge", "Preset_House_04", new Vector2(74, 187), 180, 12, 8, 4.5f, Palette.PlasterWarm, Palette.RoofBrown);
            House(Areas.Outpost, "Forge", "Preset_Blacksmith_01", new Vector2(94, 157), 270, 9, 8, 4f, Palette.StoneDark, Palette.RoofRed);
            House(Areas.Outpost, "CookShelter", "Preset_Shelter_02", new Vector2(66, 162), 90, 7, 5, 3f, Palette.Wood, Palette.RoofBrown);
            House(Areas.Outpost, "CarvingShed", "Preset_Shelter_01", new Vector2(94, 184), 270, 7, 5, 3f, Palette.Wood, Palette.RoofBrown);
            House(Areas.Outpost, "Hut1", "Preset_Hut_02", new Vector2(106, 154), 250, 5, 5, 3f, Palette.Wood, Palette.RoofBrown);
            House(Areas.Outpost, "Hut2", "Preset_Hut_02", new Vector2(108, 162), 260, 5, 5, 3f, Palette.Wood, Palette.RoofBrown);
            House(Areas.Outpost, "Lookout", "Preset_Tower_01", new Vector2(108, 178), 250, 7, 7, 8f, Palette.StoneDark, Palette.RoofRed, 0.7f);

            // Cut stone blocks waiting to go down the mountain, and a timber crane over the pit.
            var stone = new Color(0.56f, 0.54f, 0.5f);
            for (int i = 0; i < 12; i++)
            {
                float x = 76 + (i % 4) * 1.7f, z = 196 - (i / 4) * 1.5f;
                if (i % 5 == 3) continue;
                _kit.Box("Block", _static, G(x, z) + new Vector3(0, 0.55f + (i % 3 == 0 ? 0.0f : 0f), 0), new Vector3(1.5f, 1.1f, 1.3f), stone, solid: true, rotY: Rand() * 10f);
            }
            var crane = G(91, 197);
            _kit.Box("CraneMast", _static, crane + new Vector3(0, 3.5f, 0), new Vector3(0.4f, 7f, 0.4f), Palette.WoodDark);
            _kit.Box("CraneArm", _static, crane + new Vector3(-2.2f, 6.8f, 2.2f), new Vector3(0.3f, 0.3f, 6.5f), Palette.WoodDark, rotY: -45);
            _kit.Box("CraneRope", _static, crane + new Vector3(-4.3f, 4.6f, 4.3f), new Vector3(0.05f, 4.4f, 0.05f), new Color(0.8f, 0.7f, 0.5f));
            _kit.Box("CraneLoad", _static, crane + new Vector3(-4.3f, 2.2f, 4.3f), new Vector3(1.2f, 0.9f, 1.2f), stone);
            var ore = P("PT_Ore_Rock_01");
            for (int i = 0; i < 7; i++)
            {
                float a = i / 7f * Mathf.PI * 2f;
                var p = WorldGround.PitCenter + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (8.5f + Rand() * 1.5f);
                if (ore != null) _kit.PlacePrefab(ore, _models, new Vector3(p.x, T(p.x, p.y) - 0.1f, p.y), Rand() * 360f, 2.5f + Rand() * 2f);
                else Boulder(p.x, p.y, 0.8f);
            }
            AddSign(new Vector2(84, 189.5f), "Quarry", "THE QUARRY\nHard hats not yet invented.\nWatch your step.");
            // Working clutter: crates, log piles and stacked slabs around the plateau.
            var logs = P("PT_Pine_Tree_03_logs");
            foreach (var at in new[] { new Vector2(64.5f, 170), new Vector2(98, 166), new Vector2(97.5f, 190), new Vector2(63.5f, 190) })
            {
                if (logs != null) _kit.PlacePrefab(logs, _models, G(at.x, at.y), Rand() * 180f, 1.1f);
                _kit.Box("Crate", _static, G(at.x + 1.6f, at.y - 1.2f) + new Vector3(0, 0.4f, 0), new Vector3(0.8f, 0.8f, 0.8f), Palette.Wood, solid: true, rotY: Rand() * 40f);
                _kit.Box("Crate", _static, G(at.x + 2.3f, at.y - 0.4f) + new Vector3(0, 0.35f, 0), new Vector3(0.7f, 0.7f, 0.7f), Palette.WoodDark, solid: true, rotY: Rand() * 40f);
            }
            foreach (var at in new[] { new Vector2(86, 168), new Vector2(70, 176), new Vector2(90, 176) })
                for (int i = 0; i < 3; i++)
                    _kit.Box("Slab", _static, G(at.x, at.y) + new Vector3(0, 0.2f + i * 0.4f, 0), new Vector3(2.2f - i * 0.3f, 0.38f, 1.2f), stone, solid: true, rotY: Rand() * 15f);
            foreach (var at in new[] { new Vector2(63, 156), new Vector2(99, 156), new Vector2(63, 192) })
            {
                _kit.Box("FlagPole", _static, G(at.x, at.y) + new Vector3(0, 2.5f, 0), new Vector3(0.14f, 5f, 0.14f), Palette.WoodDark);
                _kit.Box("Flag", _static, G(at.x, at.y) + new Vector3(0.7f, 4.4f, 0), new Vector3(1.3f, 0.8f, 0.05f), new Color(0.35f, 0.4f, 0.5f));
            }

            // The ledge table where the Outpost champion plays, looking out over the valley.
            var t = LedgeTable;
            var tableRoot = new GameObject("LedgeTable").transform;
            tableRoot.SetParent(_static, false);
            tableRoot.localPosition = G(t.x, t.y);
            WorldPieces.GameTable(_kit, tableRoot, Vector3.zero, 1f);
            WorldPieces.Chair(_kit, tableRoot, new Vector3(0, 0, 1.5f), 180);
            WorldPieces.Chair(_kit, tableRoot, new Vector3(0, 0, -1.5f), 0);
            foreach (float x in new[] { -2.2f, 2.2f })
            {
                _kit.Box("Pole", tableRoot, new Vector3(x, 1.8f, 2.4f), new Vector3(0.14f, 3.6f, 0.14f), Palette.WoodDark);
                _kit.Box("Banner", tableRoot, new Vector3(x, 2.6f, 2.45f), new Vector3(0.9f, 1.8f, 0.05f), new Color(0.35f, 0.4f, 0.5f));
                _kit.Box("BannerMark", tableRoot, new Vector3(x, 2.9f, 2.4f), new Vector3(0.45f, 0.4f, 0.04f), Palette.Gold);
            }
            _clear.Add(new Vector3(t.x, t.y, 5));
            var focus = G(t.x, t.y) + new Vector3(0, 1f, 0);
            var dorran = AddNpc("Master Dorran Hale", "Champion of the Outpost", new Vector2(t.x, t.y + 1.5f), 180,
                new CharacterLook("Knight", hue: 35, hat: true, cape: true, height: 1.9f, saturation: 0.7f, brightness: 0.85f),
                new PersonLook { Cloth = new Color(0.45f, 0.4f, 0.35f), Accent = Palette.Gold, Hat = HatKind.Circlet, Height = 1.05f },
                EncounterCatalog.Get(EncounterCatalog.OutpostChampion), null, seated: true);
            dorran.Radius = 0f;
            _layout.OutpostChair = AddChair(dorran, G(t.x, t.y - 2.1f), G(t.x, t.y - 1.5f), focus);

            AddNpc("Ilse Marrow", "quarrymaster", new Vector2(79, 170), 200,
                new CharacterLook("Barbarian", hue: 190, hat: true, cape: false, prop: "1H_Axe", height: 1.8f, saturation: 0.6f),
                new PersonLook { Cloth = new Color(0.4f, 0.45f, 0.55f), Accent = Palette.WoodDark, Hat = HatKind.Cap, HatColor = new Color(0.6f, 0.5f, 0.3f) },
                EncounterCatalog.Get(EncounterCatalog.Ilse), null);
            AddNpc("Kettle", "camp cook", new Vector2(70, 163), 90,
                new CharacterLook("Barbarian", hue: 20, hat: false, cape: false, prop: "Mug", height: 1.65f, saturation: 0.5f, brightness: 1.2f),
                new PersonLook { Cloth = Palette.Plaster, Accent = Palette.Wood, Hat = HatKind.WideBrim, HatColor = Color.white },
                EncounterCatalog.Get(EncounterCatalog.Kettle), null);
            AddNpc("Old Grist", "master carver", new Vector2(89, 181), 250,
                new CharacterLook("Knight", hue: 0, hat: false, cape: false, prop: "1H_Sword", height: 1.7f, saturation: 0.2f, brightness: 0.9f),
                new PersonLook { Cloth = new Color(0.5f, 0.5f, 0.5f), Accent = Palette.WoodDark, Hair = new Color(0.8f, 0.8f, 0.8f) },
                EncounterCatalog.Get(EncounterCatalog.Grist), null);
            AddNpc("Foreman Bask", "outpost foreman", new Vector2(71, 181), 170,
                new CharacterLook("Barbarian", hue: 100, hat: true, cape: true, height: 1.85f, saturation: 0.7f),
                new PersonLook { Cloth = new Color(0.4f, 0.5f, 0.3f), Accent = Palette.WoodDark, Hat = HatKind.Cap, HatColor = Palette.Wood },
                null, new[]
                {
                    "Welcome to the Outpost. Mind the edge: it's a long way down to Brindlecross.",
                    "Master Dorran plays at the table on the ledge. Nobody's beaten him this year. Anvara at the forge sells wheels, if you've the coin.",
                });
            AddNpc("Anvara", "smith", new Vector2(88.5f, 157), 270,
                new CharacterLook("Barbarian", hue: 0, hat: false, cape: false, prop: "2H_Axe", height: 1.8f, saturation: 0.9f, brightness: 0.85f),
                new PersonLook { Cloth = new Color(0.35f, 0.25f, 0.2f), Accent = Palette.StoneDark, Hat = HatKind.None },
                null, new[] { "Wheels cut from mountain stone. Take a look." }, ShopCatalog.Forge);
            AddNpc("Rook", "lookout", WorldGround.KnollCenter, 230,
                new CharacterLook("RogueHooded", hue: 250, prop: "1H_Crossbow", height: 1.7f, saturation: 0.7f),
                new PersonLook { Cloth = new Color(0.3f, 0.3f, 0.45f), Accent = Palette.WoodDark, Hat = HatKind.Hood, HatColor = new Color(0.3f, 0.3f, 0.45f) },
                null, new[]
                {
                    "I watch the valley. Mostly I watch clouds.",
                    "From up here you can follow the Willow Stream all the way down to Hearthmoor.",
                });
        }

        // ------------------------------------------------------------------ errand pickups

        private static readonly Dictionary<string, Vector2> PickupSpots = new Dictionary<string, Vector2>
        {
            { "kit_toy", new Vector2(11.2f, -2.8f) },
            { "marta_specs", new Vector2(2.2f, 4.6f) },
            { "wren_hammer", new Vector2(-6.6f, 57.8f) },
            { "tobin_float", new Vector2(-2.4f, 66.2f) },
            { "mira_wax", new Vector2(-22.6f, 118f) },
            { "bask_chisels", new Vector2(80f, 208f) },
            { "anvara_ore", new Vector2(89.5f, 206f) },
            // D-038: several are off the paths, for wandering.
            { "maudie_oil", new Vector2(-60.5f, 80.5f) },
            { "pim_boat", new Vector2(-103.5f, 70f) },
            { "hob_axe", new Vector2(-86f, 214f) },
            { "agathe_caps", new Vector2(-63f, 233.5f) },
            { "rusk_rope", new Vector2(97.5f, 44f) },
            { "tilda_spring", new Vector2(40f, 87f) },
        };

        public static IReadOnlyDictionary<string, Vector2> Pickups => PickupSpots;

        private void BuildPickups()
        {
            foreach (var errand in ErrandCatalog.All)
            {
                if (errand.PickupId == null || !PickupSpots.TryGetValue(errand.PickupId, out var at)) continue;
                _clear.Add(new Vector3(at.x, at.y, 2.5f));
                var pickup = new GameObject("Pickup_" + errand.PickupId).AddComponent<Pickup>();
                pickup.transform.SetParent(_dynamic, false);
                pickup.transform.localPosition = new Vector3(at.x, _layout.Roam.Feet(at.x, at.y), at.y);
                pickup.PickupId = errand.PickupId;
                pickup.Errand = errand;
                pickup.Radius = 1.8f;
                var visual = new GameObject("Visual").transform;
                visual.SetParent(pickup.transform, false);
                _kit.Box("Item", visual, new Vector3(0, 0.2f, 0), new Vector3(0.35f, 0.35f, 0.35f), Palette.Gold);
                _kit.Prim(PrimitiveType.Cylinder, "Glow", visual, new Vector3(0, -0.12f, 0), new Vector3(0.9f, 0.01f, 0.9f), new Color(1f, 0.9f, 0.5f));
                if (_icons != null) _kit.IconLabel("Sparkle", visual, new Vector3(0, 1.1f, 0), _icons.xp, 0.5f);
                pickup.Visual = visual.gameObject;
                _layout.Pickups.Add(pickup);
                _layout.Interactables.Add(pickup);
            }
        }

        // ------------------------------------------------------------------ trees, bushes, rocks everywhere else

        private bool Blocked(float x, float z)
        {
            foreach (var c in _clear) if ((x - c.x) * (x - c.x) + (z - c.y) * (z - c.y) < c.z * c.z) return true;
            return WorldGround.StreamMask(x, z) > 0.2f || WorldGround.LakeMask(x, z) > 0.05f;
        }

        private void Scatter()
        {
            var walk = _layout.Walkable;
            var ext = WorldGround.Extent;
            const float step = 6f;
            for (float z = ext.yMin + 4; z < ext.yMax - 4; z += step)
                for (float x = ext.xMin + 4; x < ext.xMax - 4; x += step)
                {
                    float px = x + (Rand() - 0.5f) * step * 0.9f;
                    float pz = z + (Rand() - 0.5f) * step * 0.9f;
                    if (px > ext.xMax - 6f) continue;
                    float d = walk.Distance(px, pz, out var nearest);
                    if (d < 2.4f || d > 46f || Blocked(px, pz)) continue;
                    bool inFront = nearest.y - pz > 1.5f; // between the camera and the path: keep it low
                    bool highland = WorldGround.Highland(px) > 0.5f;
                    bool moor = px > 62f && pz < 146f;
                    bool hollow = Vector2.Distance(new Vector2(px, pz), new Vector2(-62f, 212f)) < 55f;
                    float r = Rand();
                    if (inFront && d < 20f)
                    {
                        if (r < 0.3f) Bush(px, pz, 0.8f);
                        else if (r < 0.55f) Plants(px, pz);
                        continue;
                    }
                    if (d > 32f && r < 0.35f) continue; // thinner far away
                    if (moor)
                    {
                        // Open moor: heather, stones and the odd wind-bent pine.
                        if (r < 0.14f) Tree(px, pz, PineTrees, 0.9f);
                        else if (r < 0.4f) Boulder(px, pz, 0.5f + Rand() * 0.8f);
                        else if (r < 0.75f) Bush(px, pz, 0.9f);
                        continue;
                    }
                    if (hollow)
                    {
                        // The old pinewood around Duskhollow: dense and dark, with dead trees and mushrooms.
                        if (r < 0.62f) Tree(px, pz, PineTrees, 1.2f);
                        else if (r < 0.74f) Tree(px, pz, DeadTrees, 1f);
                        else if (r < 0.86f) Mushrooms(px, pz);
                        else Bush(px, pz);
                        continue;
                    }
                    if (highland)
                    {
                        if (r < 0.62f) Tree(px, pz, PineTrees, 1.1f);
                        else if (r < 0.8f) Boulder(px, pz, 0.7f + Rand() * 0.9f);
                        else Bush(px, pz);
                    }
                    else
                    {
                        if (r < 0.62f) Tree(px, pz, d > 18f && Rand() < 0.6f ? PineTrees : BroadleafTrees);
                        else if (r < 0.78f) Bush(px, pz);
                        else if (r < 0.92f) Plants(px, pz);
                        else Boulder(px, pz, 0.5f + Rand() * 0.6f);
                    }
                }
            // Low plants along village edges and the road.
            for (int i = 0; i < 70; i++)
            {
                float px = -30 + Rand() * 60, pz = -20 + Rand() * 180;
                float d = walk.Distance(px, pz, out _);
                if (d > 0.8f && d < 6f && !Blocked(px, pz)) Plants(px, pz);
            }
        }

        // ------------------------------------------------------------------ people

        /// <summary>
        /// A person: an imported art-set model (D-026) if one is assigned, else a KayKit character (D-033), else the
        /// primitive figure. Root at ground level; child "Body" is what walks, bobs and turns.
        /// </summary>
        private Transform Person(string name, Vector3 pos, float yaw, CharacterLook cl, PersonLook fallback, out float height, out CharacterRig rig)
        {
            if (!_layout.PersonKeys.Contains(name)) _layout.PersonKeys.Add(name);
            rig = null;
            var slot = _art != null ? _art.Person(name) : null;
            height = 1.8f;
            if (slot != null)
            {
                var r = new GameObject(name).transform;
                r.SetParent(_dynamic, false);
                r.localPosition = pos;
                r.localRotation = Quaternion.Euler(0, yaw, 0);
                var b = new GameObject("Body").transform;
                b.SetParent(r, false);
                height = slot.size > 0 ? slot.size : DefaultPersonHeight;
                _kit.PlaceModel(slot, b, height, Vector2.zero, out _);
                return r;
            }
            if (cl != null && _look != null && _look.HasCharacters)
            {
                var r = new GameObject(name).transform;
                r.SetParent(_dynamic, false);
                r.localPosition = pos;
                r.localRotation = Quaternion.Euler(0, yaw, 0);
                var b = new GameObject("Body").transform;
                b.SetParent(r, false);
                rig = CharacterFactory.Build(_look, _kit.BaseMaterial, cl, b);
                if (rig != null) { height = cl.Height; return r; }
                Object.Destroy(r.gameObject);
            }
            return WorldPieces.Person(_kit, _dynamic, name, pos, yaw, fallback);
        }

        /// <summary>Builds the player's body under <paramref name="root"/> (a KayKit traveller, or the placeholder figure).</summary>
        public static Transform BuildPlayerFigure(WorldKit kit, WorldLook look, Transform root, out CharacterRig rig)
        {
            rig = null;
            if (look != null && look.HasCharacters)
            {
                var figure = new GameObject("Figure").transform;
                figure.SetParent(root, false);
                var body = new GameObject("Body").transform;
                body.SetParent(figure, false);
                rig = CharacterFactory.Build(look, kit.BaseMaterial, new CharacterLook("Rogue", hue: 330, cape: true, prop: "Knife", height: 1.75f), body);
                if (rig != null) return figure;
                Object.Destroy(figure.gameObject);
            }
            var look2 = new PersonLook
            {
                Cloth = new Color(0.25f, 0.42f, 0.78f), Accent = new Color(0.55f, 0.35f, 0.2f),
                Hair = new Color(0.25f, 0.16f, 0.1f), Hat = HatKind.None,
            };
            var person = WorldPieces.Person(kit, root, "Figure", Vector3.zero, 0, look2);
            kit.Box("Satchel", person.Find("Body"), new Vector3(0.3f, 0.85f, -0.15f), new Vector3(0.2f, 0.3f, 0.3f), new Color(0.5f, 0.32f, 0.18f));
            kit.Box("Cape", person.Find("Body"), new Vector3(0, 1.0f, -0.24f), new Vector3(0.5f, 0.8f, 0.06f), new Color(0.75f, 0.2f, 0.22f));
            return person;
        }

        private Npc AddNpc(string name, string title, Vector2 at, float yaw, CharacterLook cl, PersonLook fallback, EncounterDefinition encounter,
            string[] lines, string shopId = null, bool seated = false)
        {
            var pos = at.x > WorldGround.InteriorThreshold ? new Vector3(at.x, 0, at.y) : G(at.x, at.y);
            _clear.Add(new Vector3(at.x, at.y, 2.5f));
            var person = Person(name, pos, yaw, cl, fallback, out float height, out var rig);
            var npc = person.gameObject.AddComponent<Npc>();
            npc.DisplayName = name;
            npc.Title = title;
            npc.Encounter = encounter;
            npc.Lines = lines ?? new string[0];
            npc.Figure = person.Find("Body");
            npc.HomeYaw = yaw;
            npc.Rig = rig;
            npc.ShopId = shopId;
            if (seated)
            {
                if (rig != null) rig.Sit(true);
                else npc.BaseOffset = new Vector3(0, -0.35f, 0);
            }
            // Blocker so the player can't walk through people.
            var col = person.gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, 0.9f, 0);
            col.height = 1.8f;
            col.radius = 0.4f;
            // Well clear of the head (and any hat) so the plate never covers the person (D-028).
            float top = height;
            foreach (var r in person.GetComponentsInChildren<Renderer>()) top = Mathf.Max(top, r.bounds.max.y - person.position.y);
            float tagY = Mathf.Max(2.7f, top + 0.8f);
            npc.Tag = _kit.NameTag("NameTag", person, new Vector3(0, tagY, 0));
            if ((encounter != null || shopId != null) && _icons != null)
                npc.Marker = _kit.IconLabel("ChallengeMarker", person, new Vector3(0, tagY + 0.72f, 0), shopId != null ? _icons.hammer : _icons.energyA, 0.42f);
            npc.RefreshTag();
            _layout.Npcs.Add(npc);
            _layout.Interactables.Add(npc);
            return npc;
        }

        private Chair AddChair(Npc champion, Vector3 chairAt, Vector3 seat, Vector3 focus)
        {
            var chair = new GameObject("ChallengerChair").AddComponent<Chair>();
            chair.transform.SetParent(_dynamic, false);
            chair.transform.localPosition = chairAt;
            chair.SeatPosition = seat;
            chair.Champion = champion;
            chair.TableFocus = focus;
            chair.Radius = 1.8f;
            _layout.Interactables.Add(chair);
            _layout.Chairs.Add(chair);
            return chair;
        }

        private void AddSign(Vector2 at, string title, string text)
        {
            var pos = G(at.x, at.y);
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
