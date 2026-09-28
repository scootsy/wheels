using System.Collections.Generic;
using Tabletop.Application;
using UnityEngine;

namespace Tabletop.World
{
    /// <summary>
    /// The rest of the journey (D-038): the Stream Path to Lanternmere on Mirrorwater, the Hollow Path into
    /// Duskhollow, the Bell Road up onto the moor at Ironbell (with the Moor Track north to the Outpost), and the
    /// Tourney Road to Crownhold, where the Grand Tournament is played.
    /// </summary>
    public sealed partial class WorldBuilder
    {
        public static readonly Vector2[] StreamPath =
        {
            new Vector2(-1.2f, 69), new Vector2(-12, 70), new Vector2(-26, 72), new Vector2(-40, 76), new Vector2(-52, 84),
            new Vector2(-62, 92), new Vector2(-70, 97),
        };

        public static readonly Vector2[] HollowPath =
        {
            new Vector2(-22, 155), new Vector2(-30, 163), new Vector2(-40, 174), new Vector2(-48, 186), new Vector2(-55, 198),
        };

        public static readonly Vector2[] BellRoad =
        {
            new Vector2(9, 90), new Vector2(20, 88), new Vector2(34, 85), new Vector2(48, 82), new Vector2(62, 80), new Vector2(73, 79),
        };

        public static readonly Vector2[] MoorTrack =
        {
            new Vector2(101, 99), new Vector2(100, 116), new Vector2(95, 132), new Vector2(88, 145), new Vector2(84, 152),
        };

        public static readonly Vector2[] TourneyRoad =
        {
            new Vector2(20, 155), new Vector2(25, 168), new Vector2(21, 184), new Vector2(12, 198), new Vector2(4, 209), new Vector2(0, 218),
        };

        private static readonly Vector2[][] NewPaths = { StreamPath, HollowPath, BellRoad, MoorTrack, TourneyRoad };

        /// <summary>Where each new champion's table stands.</summary>
        public static readonly Vector2 PierTable = new Vector2(-85f, 77.5f);
        public static readonly Vector2 StoneTable = new Vector2(-62f, 225f);
        public static readonly Vector2 BellTable = new Vector2(92f, 84f);
        public static readonly Vector2 TourneyTable = new Vector2(0f, 240f);

        private static readonly string[] DeadTrees = { "PT_Pine_Tree_03_dead", "PT_Fruit_Tree_01_dead" };
        private static readonly string[] GreatTree = { "SM_Env_Tree_Large_02" };

        private static readonly string[] NewPrefabKeys =
        {
            "Preset_Church_01_B", "Preset_House_05", "Preset_House_06", "Preset_House_07", "Preset_House_08", "Preset_House_10",
            "Preset_House_09_C", "Preset_House_09_D", "Preset_Outhouse_01",
            "SM_Env_Waterlily_01", "SM_Env_Waterlily_02", "SM_Env_Waterlily_03", "SM_Env_Waterlily_04", "SM_Env_Waterlily_05",
            "SM_Env_Tree_Large_02", "PT_Pine_Tree_03_dead", "PT_Fruit_Tree_01_dead", "PT_Pine_Tree_03_dead_cut",
            "PT_Caesars_Mushroom_01", "PT_River_Rock_Pile_02", "PT_Generic_Shrub_01_dead",
        };

        private void DefineNewWalkable(WalkableArea w)
        {
            foreach (var path in NewPaths)
                for (int i = 0; i < path.Length - 1; i++) w.Capsule(path[i], path[i + 1], 2.6f);
            // Lanternmere: the village green on the north shore, the pier, and the shore path to the boathouse.
            w.Rect(-104, -64, 92, 116);
            w.Capsule(new Vector2(-85, 93), new Vector2(-85, 81), 1.5f);
            w.Circle(PierTable.x, PierTable.y, 3.4f);
            w.Capsule(new Vector2(-66, 93), new Vector2(-61, 80), 2.4f);
            // Duskhollow: the hollow floor and the ring of stones.
            w.Circle(-62, 211, 16);
            w.Circle(StoneTable.x, StoneTable.y, 6.5f);
            // Ironbell on the moor.
            w.Rect(70, 114, 58, 100);
            // Crownhold, inside its walls.
            w.Rect(-28, 28, 216, 262);
        }

        private void Mushrooms(float x, float z)
        {
            var pf = P("PT_Caesars_Mushroom_01");
            if (pf == null) return;
            for (int i = 0; i < 3; i++)
                _kit.PlacePrefab(pf, _models, new Vector3(x + (Rand() - 0.5f) * 1.2f, T(x, z) - 0.05f, z + (Rand() - 0.5f) * 1.2f), Rand() * 360f, 1.2f + Rand() * 1.4f);
        }

        /// <summary>A standing stone (solid).</summary>
        private void Standing(float x, float z, float size)
        {
            float y = T(x, z);
            var pf = P("PT_Menhir_Rock_02");
            if (pf == null) _kit.Box("StandingStone", _static, new Vector3(x, y + size, z), new Vector3(0.8f, size * 2f, 0.6f) * 1f, Palette.StoneDark, rotY: Rand() * 40f);
            else
            {
                var go = _kit.PlacePrefab(pf, _models, new Vector3(x, y - 0.1f, z), Rand() * 360f, size);
                _kit.Restyle(go, new Color(0.6f, 0.6f, 0.58f));
            }
            Solid(x, y, z, 0.55f, 2.5f);
        }

        /// <summary>A champion's table: the champion sits on the north side, the challenger's chair is on the south (the seated camera looks north).</summary>
        private Chair ChampionTable(string name, Vector2 t, Npc champion)
        {
            var root = new GameObject(name).transform;
            root.SetParent(_static, false);
            root.localPosition = G(t.x, t.y);
            WorldPieces.GameTable(_kit, root, Vector3.zero, 1f);
            WorldPieces.Chair(_kit, root, new Vector3(0, 0, 1.5f), 180);
            WorldPieces.Chair(_kit, root, new Vector3(0, 0, -1.5f), 0);
            _clear.Add(new Vector3(t.x, t.y, 5));
            var focus = G(t.x, t.y) + new Vector3(0, 1f, 0);
            if (champion != null) champion.Radius = 0f; // talk to them by sitting down
            return AddChair(champion, G(t.x, t.y - 2.1f), G(t.x, t.y - 1.5f), focus);
        }

        private Npc SeatedChampion(string name, Vector2 t, CharacterLook cl, PersonLook fallback, string encounterId) =>
            AddNpc(name, EncounterCatalog.Get(encounterId).Title, new Vector2(t.x, t.y + 1.5f), 180, cl, fallback, EncounterCatalog.Get(encounterId), null, seated: true);

        // ------------------------------------------------------------------ The Stream Path

        private void BuildStreamPath()
        {
            AddSign(new Vector2(-4.5f, 72.5f), "Stream Path", "THE STREAM PATH\nLanternmere and Mirrorwater: follow the stream west.\nBrindlecross: keep north on the road.");
            AddSign(new Vector2(12.5f, 93.5f), "Bell Road", "THE BELL ROAD\nIronbell: east, up onto the moor.\nListen for the bells.");
            for (int i = 1; i < StreamPath.Length - 1; i += 2)
            {
                var a = StreamPath[i];
                Flowers(a.x, a.y + 3.4f, i + 20);
            }
            // Willows and reeds along the north bank.
            for (int i = 0; i < 9; i++)
            {
                float x = -14 - i * 6.5f + Rand() * 2f;
                Tree(x, 66.8f + Rand() * 0.8f, BroadleafTrees, 0.8f);
            }
        }

        // ------------------------------------------------------------------ Lanternmere

        private void BuildLanternmere()
        {
            const string A = Areas.Lanternmere;
            float water = WorldGround.WaterLevel;
            House(A, "Chapel", "Preset_Church_01_B", new Vector2(-84, 113.5f), 180, 8, 7, 5f, Color.white, Palette.RoofBlue);
            House(A, "Lanternhouse", "Preset_House_05", new Vector2(-100.5f, 104), 90, 7, 6, 3.6f, Palette.PlasterWarm, Palette.RoofRed);
            House(A, "NetLoft", "Preset_House_06", new Vector2(-67.5f, 109), -90, 7, 6, 3.6f, Palette.Plaster, Palette.RoofBrown);
            House(A, "Cottage", "Preset_House_09_C", new Vector2(-99, 114.5f), 150, 5, 5, 3f, Palette.PlasterWarm, Palette.RoofGreen);
            House(A, "Boathouse", "Preset_Shelter_02", new Vector2(-56.5f, 80), -90, 7, 5, 3f, Palette.Wood, Palette.RoofBrown);
            WorldPieces.Stall(_kit, _static, G(-94, 100), 0, new Color(0.95f, 0.75f, 0.3f));
            AddSign(new Vector2(-66, 95.5f), "Lanternmere", "LANTERNMERE\non Mirrorwater, where the Willow Stream begins.\nMother Seraphine plays on the pier.");

            // Lanterns along the shore (the village's name).
            for (float x = -102; x <= -67; x += 5.8f)
                if (Mathf.Abs(x + 85) > 3.5f) WorldPieces.Lamp(_kit, _static, G(x, 93.2f));

            // The pier and its table.
            var deckY = G(-85, 87).y;
            var pier = new GameObject("Pier").transform;
            pier.SetParent(_static, false);
            pier.localPosition = new Vector3(-85, deckY, 87);
            _kit.Box("Deck", pier, new Vector3(0, -0.12f, 0), new Vector3(2.6f, 0.24f, 12.5f), Palette.Wood);
            for (int i = 0; i < 12; i++) _kit.Box("Plank", pier, new Vector3(0, 0.005f, -5.8f + i * 1.05f), new Vector3(2.6f, 0.02f, 0.1f), Palette.WoodDark);
            _kit.Box("Platform", pier, new Vector3(0, -0.12f, PierTable.y - 87), new Vector3(7.2f, 0.24f, 7.2f), Palette.Wood);
            foreach (var p in new[] { new Vector2(-1.3f, 5), new Vector2(1.3f, 5), new Vector2(-1.3f, -1), new Vector2(1.3f, -1), new Vector2(-3.5f, -13), new Vector2(3.5f, -13), new Vector2(-3.5f, -6), new Vector2(3.5f, -6) })
                _kit.Box("Piling", pier, new Vector3(p.x, -1.2f, p.y), new Vector3(0.3f, 2.4f, 0.3f), Palette.WoodDark);
            foreach (var p in new[] { new Vector2(-3.4f, -13.1f), new Vector2(3.4f, -13.1f), new Vector2(-3.4f, -6.1f), new Vector2(3.4f, -6.1f) })
            {
                _kit.Box("LanternPost", pier, new Vector3(p.x, 0.9f, p.y), new Vector3(0.14f, 1.8f, 0.14f), Palette.WoodDark);
                _kit.Box("Lantern", pier, new Vector3(p.x, 1.95f, p.y), new Vector3(0.36f, 0.4f, 0.36f), Palette.WindowLit);
            }
            var seraphine = SeatedChampion("Mother Seraphine Vell", PierTable,
                new CharacterLook("Mage", hue: 200, hat: true, cape: true, prop: "Spellbook_open", height: 1.7f, saturation: 0.45f, brightness: 1.35f),
                new PersonLook { Cloth = new Color(0.9f, 0.92f, 0.96f), Accent = Palette.Gold, Hat = HatKind.Hood, HatColor = new Color(0.55f, 0.7f, 0.9f), Robe = true, Height = 1.0f },
                EncounterCatalog.LakeChampion);
            _layout.LakeChair = ChampionTable("PierTable", PierTable, seraphine);

            // Boats on the water and upturned on the shore; lilies and reeds.
            foreach (var (x, z, yaw) in new[] { (-76.5f, 83f, 20f), (-94f, 81.5f, -35f), (-90f, 64f, 70f) })
            {
                _kit.Box("Boat", _static, new Vector3(x, water + 0.12f, z), new Vector3(1.3f, 0.45f, 3.4f), Palette.Wood, rotY: yaw);
                _kit.Box("BoatInside", _static, new Vector3(x, water + 0.3f, z), new Vector3(1.0f, 0.12f, 3.0f), Palette.WoodDark, rotY: yaw);
            }
            foreach (var (x, z, yaw) in new[] { (-74f, 94.4f, 80f), (-77.5f, 94.5f, 95f) })
                _kit.Box("UpturnedBoat", _static, G(x, z) + new Vector3(0, 0.3f, 0), new Vector3(1.3f, 0.6f, 3.4f), new Color(0.45f, 0.3f, 0.2f), solid: true, rotY: yaw);
            for (int i = 0; i < 16; i++)
            {
                float a = Rand() * Mathf.PI * 2f, r = 4f + Rand() * 8f;
                var p = WorldGround.LakeCenter + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                if (Mathf.Abs(p.x - PierTable.x) < 5f && p.y > PierTable.y - 5f) continue;
                var lily = P("SM_Env_Waterlily_0" + (1 + i % 5));
                if (lily != null) _kit.PlacePrefab(lily, _models, new Vector3(p.x, water + 0.02f, p.y), Rand() * 360f, 0.8f + Rand() * 0.6f);
                else _kit.Prim(PrimitiveType.Cylinder, "Lily", _static, new Vector3(p.x, water + 0.02f, p.y), new Vector3(0.9f, 0.01f, 0.9f), new Color(0.3f, 0.55f, 0.3f));
            }
            for (int i = 0; i < 40; i++)
            {
                float a = Rand() * Mathf.PI * 2f;
                var p = WorldGround.LakeCenter + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (12.8f + Rand() * 1.6f);
                if (Mathf.Abs(p.x - PierTable.x) < 3f && p.y > 85f) continue;
                if (p.x > -72f && Mathf.Abs(p.y - WorldGround.StreamZ) < 4f) continue; // the stream's mouth
                for (int k = 0; k < 4; k++)
                    _kit.Box("Reed", _static, new Vector3(p.x + (Rand() - 0.5f) * 0.6f, water + 0.5f, p.y + (Rand() - 0.5f) * 0.6f), new Vector3(0.05f, 1.1f + Rand() * 0.5f, 0.05f),
                        new Color(0.45f, 0.58f, 0.3f), rotY: Rand() * 90f);
            }
            // Big rocks on the far (west) shore, where Pim's boat washes up.
            Boulder(-106f, 67.5f, 1.3f);
            Boulder(-106.5f, 73.5f, 1.0f);
            Boulder(-102f, 64f, 0.9f);

            AddNpc("Finn Harrow", "boatwright", new Vector2(-70, 99), 250,
                new CharacterLook("Barbarian", hue: 30, hat: false, cape: false, prop: "1H_Axe", height: 1.8f, saturation: 0.7f),
                new PersonLook { Cloth = new Color(0.55f, 0.4f, 0.28f), Accent = Palette.WoodDark, Hat = HatKind.Cap, HatColor = new Color(0.3f, 0.35f, 0.5f) },
                EncounterCatalog.Get(EncounterCatalog.Finn), null);
            AddNpc("Nell Cotter", "net-weaver", new Vector2(-78, 106), 180,
                new CharacterLook("Rogue", hue: 170, hat: false, cape: false, height: 1.65f, saturation: 0.8f),
                new PersonLook { Cloth = new Color(0.3f, 0.55f, 0.55f), Accent = Palette.Wood, Hat = HatKind.Kerchief, HatColor = new Color(0.9f, 0.85f, 0.7f) },
                EncounterCatalog.Get(EncounterCatalog.Nell), null);
            AddNpc("Brother Aldous", "lamplighter", new Vector2(-89, 108), 160,
                new CharacterLook("Mage", hue: 40, hat: false, cape: false, prop: "2H_Staff", height: 1.75f, saturation: 0.5f, brightness: 0.9f),
                new PersonLook { Cloth = new Color(0.5f, 0.38f, 0.25f), Accent = Palette.Gold, Hat = HatKind.Hood, HatColor = new Color(0.5f, 0.38f, 0.25f), Robe = true },
                EncounterCatalog.Get(EncounterCatalog.Aldous), null);
            AddNpc("Old Maudie", "lantern maker", new Vector2(-94, 98.2f), 180,
                new CharacterLook("Mage", hue: 310, hat: false, cape: false, prop: "Mug", height: 1.5f, saturation: 0.6f),
                new PersonLook { Cloth = new Color(0.55f, 0.35f, 0.5f), Accent = Palette.Gold, Hair = new Color(0.85f, 0.85f, 0.85f), Hat = HatKind.Kerchief, HatColor = new Color(0.95f, 0.75f, 0.3f), Height = 0.9f },
                null, new[] { "Lanterns and charms, love. Have a look." }, ShopCatalog.LanternStall);
            AddNpc("Pim", "ferry kid", new Vector2(-80, 95.5f), 200,
                new CharacterLook("Rogue", hue: 60, hat: false, cape: false, height: 1.2f),
                new PersonLook { Cloth = new Color(0.8f, 0.6f, 0.25f), Accent = Palette.Wood, Hat = HatKind.Cap, HatColor = new Color(0.25f, 0.45f, 0.75f), Height = 0.72f },
                null, new[]
                {
                    "Mother Seraphine plays on the end of the pier. She's never lost there. Not ever.",
                    "She's got a Priest! It heals her Crown right back up. It's not fair. It's brilliant.",
                });
        }

        // ------------------------------------------------------------------ Duskhollow

        private void BuildDuskhollow()
        {
            const string A = Areas.Duskhollow;
            foreach (var q in new[] { new Vector2(-70, 207), new Vector2(-54, 213), new Vector2(-68.5f, 216.5f), new Vector2(-57, 201.5f), new Vector2(-70, 222) })
                _clear.Add(new Vector3(q.x, q.y, 3f)); // people and the kiln (the pines inside the hollow keep clear)
            AddSign(new Vector2(-19f, 160.5f), "Hollow Path", "THE HOLLOW PATH\nDuskhollow: down through the pinewood, north-west.\nThey say the Nightjar never loses inside the stones.");
            House(A, "HerbHut", "Preset_Hut_02", new Vector2(-76, 205), 60, 5, 5, 3f, Palette.Wood, Palette.RoofGreen);
            House(A, "ForagerHut", "Preset_House_09_D", new Vector2(-47.5f, 207), -75, 5, 5, 3f, Palette.PlasterWarm, Palette.RoofBrown);
            House(A, "Kiln", "Preset_Shelter_01", new Vector2(-76.5f, 218), 105, 7, 5, 3f, Palette.WoodDark, Palette.RoofBrown);
            House(A, "WoodShed", "Preset_Outhouse_01", new Vector2(-47, 218), -110, 3, 3, 2.5f, Palette.Wood, Palette.RoofBrown);
            House(A, "Lodge", "Preset_House_10", new Vector2(-73, 195.5f), 30, 7, 6, 3.6f, Palette.PlasterWarm, Palette.RoofBrown);
            // Corwen's charcoal kiln mound, smouldering.
            _kit.Prim(PrimitiveType.Sphere, "KilnMound", _static, G(-70, 222) + new Vector3(0, 0.2f, 0), new Vector3(3f, 2f, 3f), new Color(0.2f, 0.18f, 0.16f), solid: true);
            _kit.Cone("KilnGlow", _static, G(-70, 222) + new Vector3(0, 1.05f, 0), new Vector3(0.5f, 0.3f, 0.5f), Palette.Fire);

            // The ring of stones, open to the south, with the Nightjar's table inside.
            var t = StoneTable;
            for (int i = 0; i < 10; i++)
            {
                float deg = i * 36f + 18f;
                if (Mathf.Abs(Mathf.DeltaAngle(deg, 270f)) < 30f) continue; // the way in
                float a = deg * Mathf.Deg2Rad;
                Standing(t.x + Mathf.Cos(a) * 5.6f, t.y + Mathf.Sin(a) * 5.6f, 1.3f + Rand() * 0.3f);
            }
            foreach (var p in new[] { new Vector2(-66.5f, 218.5f), new Vector2(-57.5f, 218.5f), new Vector2(-69, 230), new Vector2(-55, 230) })
                WorldPieces.Lamp(_kit, _static, G(p.x, p.y));
            var silas = SeatedChampion("Silas Thorne", t,
                new CharacterLook("RogueHooded", hue: 270, prop: "Knife", height: 1.8f, saturation: 0.5f, brightness: 0.6f),
                new PersonLook { Cloth = new Color(0.18f, 0.15f, 0.22f), Accent = new Color(0.5f, 0.45f, 0.6f), Hat = HatKind.Hood, HatColor = new Color(0.18f, 0.15f, 0.22f), Height = 1.02f },
                EncounterCatalog.HollowChampion);
            _layout.HollowChair = ChampionTable("StoneTable", t, silas);

            // The great tree north of the stones, silvercaps beneath it.
            Tree(-67f, 237f, GreatTree, 1.9f);
            _clear.Add(new Vector3(-67, 237, 5));
            for (int i = 0; i < 5; i++) Mushrooms(-66f + Rand() * 6f, 231.5f + Rand() * 3f);
            // Fallen pines up the western slope, where Hob's axe head landed.
            foreach (var (x, z, yaw) in new[] { (-84f, 212.5f, 30f), (-88.5f, 216f, -60f), (-83f, 217f, 100f) })
            {
                var log = P(Rand() < 0.5f ? "PT_Pine_Tree_03_dead_cut" : "PT_Pine_Tree_03_logs");
                if (log != null) _kit.PlacePrefab(log, _models, new Vector3(x, T(x, z) - 0.1f, z), yaw, 1f);
                else _kit.Box("FallenPine", _static, new Vector3(x, T(x, z) + 0.3f, z), new Vector3(0.6f, 0.6f, 6f), Palette.Trunk, rotY: yaw);
            }
            _clear.Add(new Vector3(-86, 214, 5));
            // Pines inside the hollow as well, so it reads as a clearing in the wood rather than a field.
            for (int i = 0; i < 16; i++)
            {
                float a = Rand() * Mathf.PI * 2f;
                var p = new Vector2(-62, 211) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (10.5f + Rand() * 5f);
                if (Blocked(p.x, p.y) || Vector2.Distance(p, HollowPath[HollowPath.Length - 1]) < 5f || Vector2.Distance(p, t) < 8.5f) continue;
                Tree(p.x, p.y, PineTrees, 1.1f);
                _clear.Add(new Vector3(p.x, p.y, 2.5f));
            }
            for (int i = 0; i < 12; i++)
            {
                float a = Rand() * Mathf.PI * 2f;
                var p = new Vector2(-62, 211) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (12f + Rand() * 5f);
                if (Vector2.Distance(p, HollowPath[HollowPath.Length - 1]) < 5f || Vector2.Distance(p, t) < 8f) continue;
                Mushrooms(p.x, p.y);
            }

            AddNpc("Moth", "herbalist", new Vector2(-70, 207), 60,
                new CharacterLook("Mage", hue: 110, hat: false, cape: true, prop: "Spellbook", height: 1.6f, saturation: 0.6f, brightness: 0.85f),
                new PersonLook { Cloth = new Color(0.35f, 0.45f, 0.35f), Accent = Palette.Wood, Hat = HatKind.Hood, HatColor = new Color(0.3f, 0.4f, 0.3f), Robe = true },
                EncounterCatalog.Get(EncounterCatalog.Moth), null);
            AddNpc("Grey Agathe", "mushroom forager", new Vector2(-54, 213), 250,
                new CharacterLook("Mage", hue: 0, hat: true, cape: false, prop: "2H_Staff", height: 1.55f, saturation: 0.15f, brightness: 0.95f),
                new PersonLook { Cloth = new Color(0.55f, 0.55f, 0.55f), Accent = Palette.WoodDark, Hair = new Color(0.8f, 0.8f, 0.8f), Hat = HatKind.Pointed, HatColor = new Color(0.4f, 0.4f, 0.42f), Height = 0.92f },
                EncounterCatalog.Get(EncounterCatalog.Agathe), null);
            AddNpc("Corwen Ash", "charcoal burner", new Vector2(-68.5f, 216.5f), 110,
                new CharacterLook("Barbarian", hue: 0, hat: false, cape: false, prop: "2H_Axe", height: 1.85f, saturation: 0.3f, brightness: 0.55f),
                new PersonLook { Cloth = new Color(0.25f, 0.22f, 0.2f), Accent = Palette.StoneDark, Hat = HatKind.None },
                EncounterCatalog.Get(EncounterCatalog.Corwen), null);
            AddNpc("Hob", "woodcutter", new Vector2(-57, 201.5f), 200,
                new CharacterLook("Barbarian", hue: 90, hat: true, cape: false, prop: "1H_Axe", height: 1.75f, saturation: 0.6f),
                new PersonLook { Cloth = new Color(0.4f, 0.5f, 0.3f), Accent = Palette.Wood, Hat = HatKind.Cap, HatColor = new Color(0.55f, 0.25f, 0.2f) },
                null, new[]
                {
                    "Keep your voice down in the hollow. The Nightjar likes it quiet.",
                    "He plays in the ring of stones, up at the north end. Nobody's beaten him inside them. Nobody.",
                });
        }

        // ------------------------------------------------------------------ Ironbell

        private void BuildIronbell()
        {
            const string A = Areas.Ironbell;
            House(A, "BellTower", "Preset_Tower_01", new Vector2(92, 96.5f), 180, 7, 7, 9f, Palette.StoneDark, Palette.RoofRed, 0.95f);
            House(A, "Clockworks", "Preset_House_07", new Vector2(76.5f, 93), 150, 7, 6, 3.8f, Palette.Plaster, Palette.RoofBlue);
            House(A, "Bellfoundry", "Preset_Blacksmith_01", new Vector2(108.5f, 71), -90, 9, 8, 4f, Palette.StoneDark, Palette.RoofRed);
            House(A, "Inn", "Preset_House_08", new Vector2(108, 90), -90, 8, 7, 4f, Palette.PlasterWarm, Palette.RoofBrown);
            House(A, "Cottage", "Preset_House_06", new Vector2(77, 63.5f), 45, 6, 5, 3.4f, Palette.Plaster, Palette.RoofRed);
            House(A, "Cottage2", "Preset_House_10", new Vector2(96, 61.5f), 0, 6, 5, 3.4f, Palette.PlasterWarm, Palette.RoofBlue);
            // The town gate on the bell road.
            foreach (float z in new[] { 74.8f, 83.2f })
            {
                _kit.Box("GatePillar", _static, G(70.5f, z) + new Vector3(0, 2.4f, 0), new Vector3(1.2f, 4.8f, 1.2f), Palette.StoneDark, solid: true);
                _kit.Box("GateBanner", _static, G(69.8f, z) + new Vector3(0, 3.2f, 0), new Vector3(0.05f, 1.8f, 0.9f), new Color(0.25f, 0.35f, 0.55f));
            }
            AddSign(new Vector2(67, 85.5f), "Ironbell", "IRONBELL\nThe bell town on the moor.\nThe Moor Track runs north to the Stonemasons' Outpost.");
            AddSign(new Vector2(104.5f, 101.5f), "Moor Track", "THE MOOR TRACK\nStonemasons' Outpost: north across the moor.");
            AddSign(new Vector2(87.5f, 149), "Moor Track south", "THE MOOR TRACK\nIronbell: south across the moor.");
            foreach (var p in new[] { new Vector2(80, 72), new Vector2(104, 72), new Vector2(80, 88), new Vector2(104, 86) })
                WorldPieces.Lamp(_kit, _static, G(p.x, p.y));

            // The great bell in its timber frame, with the Bellwarden's table beneath it.
            var t = BellTable;
            foreach (float x in new[] { -4.2f, 4.2f })
                _kit.Box("BellPost", _static, G(t.x + x, t.y + 0.5f) + new Vector3(0, 3.6f, 0), new Vector3(0.5f, 7.2f, 0.5f), Palette.WoodDark, solid: true);
            _kit.Box("BellBeam", _static, G(t.x, t.y + 0.5f) + new Vector3(0, 7.1f, 0), new Vector3(9f, 0.6f, 0.6f), Palette.WoodDark);
            var bronze = new Color(0.72f, 0.5f, 0.24f);
            _kit.Cone("GreatBell", _static, G(t.x, t.y + 0.5f) + new Vector3(0, 4.3f, 0), new Vector3(2.6f, 2.6f, 2.6f), bronze);
            _kit.Prim(PrimitiveType.Sphere, "Clapper", _static, G(t.x, t.y + 0.5f) + new Vector3(0, 4.35f, 0), Vector3.one * 0.45f, Palette.StoneDark);
            var orlan = SeatedChampion("Magister Orlan Vey", t,
                new CharacterLook("Mage", hue: 280, hat: true, cape: true, prop: "1H_Wand", height: 1.85f, saturation: 0.8f, brightness: 0.6f),
                new PersonLook { Cloth = new Color(0.3f, 0.18f, 0.35f), Accent = Palette.Gold, Hat = HatKind.Pointed, HatColor = new Color(0.25f, 0.15f, 0.3f), Robe = true, Height = 1.05f },
                EncounterCatalog.BellChampion);
            _layout.BellChair = ChampionTable("BellTable", t, orlan);

            // Standing stones on the moor south of town, and Widow Callow's sheep.
            foreach (var p in new[] { new Vector2(94, 41), new Vector2(99.5f, 40.5f), new Vector2(101, 46.5f), new Vector2(93.5f, 47) })
                Standing(p.x, p.y, 1.2f + Rand() * 0.3f);
            _clear.Add(new Vector3(97, 44, 7));
            for (int i = 0; i < 7; i++)
            {
                float x = 76 + Rand() * 12f, z = 44 + Rand() * 9f;
                var sheep = new GameObject("Sheep").transform;
                sheep.SetParent(_static, false);
                sheep.localPosition = new Vector3(x, T(x, z), z);
                sheep.localRotation = Quaternion.Euler(0, Rand() * 360f, 0);
                _kit.Box("Fleece", sheep, new Vector3(0, 0.55f, 0), new Vector3(0.7f, 0.6f, 1.05f), new Color(0.93f, 0.92f, 0.88f));
                _kit.Box("Head", sheep, new Vector3(0, 0.75f, 0.6f), new Vector3(0.3f, 0.32f, 0.36f), new Color(0.18f, 0.16f, 0.15f));
                foreach (var l in new[] { new Vector2(-0.22f, -0.35f), new Vector2(0.22f, -0.35f), new Vector2(-0.22f, 0.35f), new Vector2(0.22f, 0.35f) })
                    _kit.Box("Leg", sheep, new Vector3(l.x, 0.15f, l.y), new Vector3(0.1f, 0.3f, 0.1f), new Color(0.18f, 0.16f, 0.15f));
                _clear.Add(new Vector3(x, z, 1.5f));
            }

            AddNpc("Tilda Brass", "clockmaker's apprentice", new Vector2(80.5f, 89), 150,
                new CharacterLook("Rogue", hue: 20, hat: false, cape: false, prop: "Throwable", height: 1.6f, saturation: 0.9f),
                new PersonLook { Cloth = new Color(0.7f, 0.45f, 0.2f), Accent = Palette.Gold, Hat = HatKind.Cap, HatColor = new Color(0.4f, 0.3f, 0.2f) },
                EncounterCatalog.Get(EncounterCatalog.Tilda), null);
            AddNpc("Captain Rusk", "retired bellringer", new Vector2(86.5f, 92.5f), 170,
                new CharacterLook("Knight", hue: 200, hat: false, cape: true, height: 1.8f, saturation: 0.5f, brightness: 0.9f),
                new PersonLook { Cloth = new Color(0.25f, 0.35f, 0.5f), Accent = Palette.Gold, Hair = new Color(0.85f, 0.85f, 0.85f), Hat = HatKind.WideBrim, HatColor = new Color(0.2f, 0.25f, 0.35f) },
                EncounterCatalog.Get(EncounterCatalog.Rusk), null);
            AddNpc("Widow Callow", "moor shepherd", new Vector2(83, 64.5f), 200,
                new CharacterLook("RogueHooded", hue: 30, prop: "2H_Staff", height: 1.65f, saturation: 0.35f, brightness: 0.8f),
                new PersonLook { Cloth = new Color(0.35f, 0.3f, 0.28f), Accent = Palette.Wood, Hat = HatKind.Hood, HatColor = new Color(0.3f, 0.28f, 0.26f) },
                EncounterCatalog.Get(EncounterCatalog.Callow), null);
            AddNpc("Oskar Bell", "bellfounder", new Vector2(102.5f, 71), 90,
                new CharacterLook("Barbarian", hue: 25, hat: false, cape: false, prop: "2H_Axe", height: 1.85f, saturation: 0.8f, brightness: 0.8f),
                new PersonLook { Cloth = new Color(0.45f, 0.3f, 0.2f), Accent = Palette.StoneDark, Hat = HatKind.None },
                null, new[] { "Bells, gears and wheels. Have a look." }, ShopCatalog.Bellfoundry);
            AddNpc("Wenna", "gatekeeper", new Vector2(73.5f, 76.5f), 250,
                new CharacterLook("Knight", hue: 210, hat: true, cape: false, prop: "1H_Sword", height: 1.75f, saturation: 0.6f),
                new PersonLook { Cloth = new Color(0.35f, 0.4f, 0.55f), Accent = Palette.StoneDark, Hat = HatKind.Cap, HatColor = new Color(0.35f, 0.4f, 0.55f) },
                null, new[]
                {
                    "Welcome to Ironbell. The great bell rings every hour. You'll stop noticing. Eventually.",
                    "Magister Vey plays under the bell. Beat him and you'll have five titles, if you've been busy. Five titles gets you into the Grand Tournament at Crownhold, north of Brindlecross.",
                });
        }

        // ------------------------------------------------------------------ Crownhold and the Grand Tournament

        private void BuildCrownhold()
        {
            const string A = Areas.Crownhold;
            AddSign(new Vector2(23.5f, 151f), "Tourney Road", "THE TOURNEY ROAD\nCrownhold and the Grand Tournament: north, past the hall.\nOpen to the five town champions.");
            AddSign(new Vector2(7.5f, 211.5f), "Crownhold", "CROWNHOLD\nHome of the Grand Tournament.\nThree rounds. One Grand Champion.");
            // Pennants along the road up the hill.
            for (int i = 1; i < TourneyRoad.Length - 1; i++)
            {
                var a = TourneyRoad[i];
                var dir = (TourneyRoad[i + 1] - a).normalized;
                var side = new Vector2(dir.y, -dir.x) * 3.3f;
                foreach (var p in new[] { a + side, a - side })
                {
                    _kit.Box("PennantPole", _static, G(p.x, p.y) + new Vector3(0, 1.8f, 0), new Vector3(0.12f, 3.6f, 0.12f), Palette.WoodDark);
                    _kit.Box("Pennant", _static, G(p.x, p.y) + new Vector3(0.35f, 3.2f, 0), new Vector3(0.7f, 0.9f, 0.04f), i % 2 == 0 ? Palette.Banner : new Color(0.25f, 0.35f, 0.6f));
                }
            }

            // Walls: low at the front (the camera looks over them), tall on the other three sides.
            var stone = new Color(0.72f, 0.69f, 0.63f);
            void Wall(Vector2 a, Vector2 b, float h)
            {
                var mid = (a + b) / 2f;
                var d = b - a;
                float yaw = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
                float y = Mathf.Min(WorldGround.Walk(a.x, a.y), WorldGround.Walk(b.x, b.y)) - 0.5f;
                _kit.Box("Wall", _static, new Vector3(mid.x, y + (h + 0.5f) / 2f, mid.y), new Vector3(1.2f, h + 0.5f, d.magnitude), stone, solid: true, rotY: yaw);
                int n = Mathf.FloorToInt(d.magnitude / 2f);
                for (int i = 0; i < n; i++)
                {
                    var p = a + d * ((i + 0.5f) / n);
                    _kit.Box("Merlon", _static, new Vector3(p.x, y + h + 0.5f + 0.3f, p.y), new Vector3(1.2f, 0.6f, 0.8f), stone, rotY: yaw);
                }
            }
            Wall(new Vector2(-30, 214.5f), new Vector2(-5, 214.5f), 2.4f);
            Wall(new Vector2(5, 214.5f), new Vector2(30, 214.5f), 2.4f);
            Wall(new Vector2(-30.5f, 214), new Vector2(-30.5f, 264.5f), 5f);
            Wall(new Vector2(30.5f, 214), new Vector2(30.5f, 264.5f), 5f);
            Wall(new Vector2(-30.5f, 264.5f), new Vector2(30.5f, 264.5f), 5f);
            foreach (var c in new[] { new Vector2(-30.5f, 214.5f), new Vector2(30.5f, 214.5f), new Vector2(-30.5f, 264.5f), new Vector2(30.5f, 264.5f), new Vector2(-5.2f, 214.5f), new Vector2(5.2f, 214.5f) })
            {
                bool gate = Mathf.Abs(c.x) < 6f;
                _kit.Prim(PrimitiveType.Cylinder, "Tower", _static, G(c.x, c.y) + new Vector3(0, gate ? 2.5f : 3.5f, 0), new Vector3(gate ? 2.2f : 3.2f, gate ? 2.5f : 3.5f, gate ? 2.2f : 3.2f), stone, solid: true);
                _kit.Cone("TowerRoof", _static, G(c.x, c.y) + new Vector3(0, gate ? 5f : 7f, 0), new Vector3(gate ? 2.6f : 3.8f, 2.2f, gate ? 2.6f : 3.8f), Palette.RoofBlue);
                if (gate) _kit.Box("GateBanner", _static, G(c.x, c.y - 1.15f) + new Vector3(0, 3f, 0), new Vector3(1f, 2.2f, 0.05f), Palette.Banner);
            }

            House(A, "Keep", "Preset_Church_01_A", new Vector2(0, 256), 180, 9, 8, 6f, Palette.Plaster, Palette.RoofBlue, 1.1f);
            House(A, "WestTower", "Preset_Tower_01", new Vector2(-20, 255), 180, 7, 7, 8f, Palette.Plaster, Palette.RoofBlue);
            House(A, "EastTower", "Preset_Tower_01", new Vector2(20, 255), 180, 7, 7, 8f, Palette.Plaster, Palette.RoofBlue);

            // The arena: a raised stone ring with stands either side and the tournament table in the middle.
            var t = TourneyTable;
            var floorY = G(t.x, t.y).y;
            _kit.Prim(PrimitiveType.Cylinder, "ArenaRing", _static, new Vector3(t.x, floorY + 0.02f, t.y), new Vector3(19f, 0.02f, 19f), new Color(0.55f, 0.4f, 0.22f));
            _kit.Prim(PrimitiveType.Cylinder, "Arena", _static, new Vector3(t.x, floorY + 0.04f, t.y), new Vector3(18f, 0.02f, 18f), new Color(0.6f, 0.56f, 0.5f));
            foreach (float side in new[] { -1f, 1f })
            {
                for (int i = 0; i < 3; i++)
                {
                    float x = side * (12.5f + i * 1.3f);
                    float h = 0.5f * (i + 1);
                    _kit.Box("Stand", _static, new Vector3(x, floorY + h / 2f, t.y), new Vector3(1.3f, h, 15f), i % 2 == 0 ? Palette.Wood : Palette.WoodDark, solid: true);
                }
                for (int i = 0; i < 4; i++)
                {
                    float z = t.y - 6f + i * 4f;
                    float x = side * 16.8f;
                    _kit.Box("BannerPole", _static, new Vector3(x, floorY + 2.5f, z), new Vector3(0.14f, 5f, 0.14f), Palette.WoodDark);
                    _kit.Box("Banner", _static, new Vector3(x - side * 0.1f, floorY + 3.6f, z), new Vector3(0.05f, 2.2f, 1.1f), i % 2 == 0 ? Palette.Banner : new Color(0.25f, 0.35f, 0.6f));
                }
            }
            // The trophy on its pedestal.
            _kit.Box("Pedestal", _static, G(7, 229) + new Vector3(0, 0.6f, 0), new Vector3(1f, 1.2f, 1f), stone, solid: true);
            _kit.Prim(PrimitiveType.Cylinder, "Trophy", _static, G(7, 229) + new Vector3(0, 1.45f, 0), new Vector3(0.55f, 0.25f, 0.55f), Palette.Gold);
            _kit.Cone("TrophyCrown", _static, G(7, 229) + new Vector3(0, 1.7f, 0), new Vector3(0.6f, 0.45f, 0.6f), Palette.Gold);

            var root = new GameObject("TourneyTable").transform;
            root.SetParent(_static, false);
            root.localPosition = G(t.x, t.y);
            WorldPieces.GameTable(_kit, root, Vector3.zero, 1.1f);
            WorldPieces.Chair(_kit, root, new Vector3(0, 0, 1.6f), 180);
            WorldPieces.Chair(_kit, root, new Vector3(0, 0, -1.6f), 0);
            _clear.Add(new Vector3(t.x, t.y, 6));
            _layout.TournamentSeat = G(t.x, t.y + 1.6f);
            var chair = AddChair(null, G(t.x, t.y - 2.2f), G(t.x, t.y - 1.6f), G(t.x, t.y) + new Vector3(0, 1f, 0));
            chair.TournamentTable = true;
            _layout.TournamentChair = chair;

            // The competitors wait at the edge of the ring for their round (WorldApp seats the one you face next).
            AddNpc("Dame Ottilie Frane", "the Iron Rose", new Vector2(-8.5f, 247), 120,
                new CharacterLook("Knight", hue: 340, hat: false, cape: true, prop: "1H_Sword", height: 1.8f, saturation: 0.9f, brightness: 0.85f),
                new PersonLook { Cloth = new Color(0.6f, 0.2f, 0.3f), Accent = Palette.Gold, Hat = HatKind.Circlet },
                EncounterCatalog.Get(EncounterCatalog.TourneyFirst), null);
            AddNpc("Lord Casimir Vane", "tournament finalist", new Vector2(8.5f, 247), 240,
                new CharacterLook("Mage", hue: 230, hat: false, cape: true, prop: "1H_Wand", height: 1.85f, saturation: 0.7f, brightness: 0.7f),
                new PersonLook { Cloth = new Color(0.2f, 0.25f, 0.45f), Accent = Palette.Gold, Hat = HatKind.Circlet, Robe = true },
                EncounterCatalog.Get(EncounterCatalog.TourneySecond), null);
            AddNpc("Aldric Mourne", "Grand Champion of the Realm", new Vector2(0, 250), 180,
                new CharacterLook("Knight", hue: 45, hat: true, cape: true, prop: "1H_Sword", height: 1.95f, saturation: 0.9f, brightness: 0.95f),
                new PersonLook { Cloth = new Color(0.75f, 0.6f, 0.25f), Accent = Palette.Gold, Hat = HatKind.Circlet, Robe = true, Height = 1.08f },
                EncounterCatalog.Get(EncounterCatalog.GrandChampion), null);
            var herald = AddNpc("Herald Aubrey", "tournament herald", new Vector2(-5, 228.5f), 180,
                new CharacterLook("Knight", hue: 330, hat: false, cape: true, height: 1.75f, saturation: 0.9f),
                new PersonLook { Cloth = Palette.Banner, Accent = Palette.Gold, Hat = HatKind.WideBrim, HatColor = Palette.Banner },
                null, new[] { "Hear ye! The Grand Tournament of Crownhold!" });
            herald.Role = "herald";
            _layout.Herald = herald;
            foreach (var n in _layout.Npcs)
                if (n.Encounter != null && n.Encounter.Tournament) { n.HomePosition = n.transform.position; n.StandingYaw = n.HomeYaw; }

            AddNpc("Pip Lark", "tourney-goer", new Vector2(-11, 233), 90,
                new CharacterLook("Rogue", hue: 100, hat: false, cape: false, prop: "Mug", height: 1.7f),
                new PersonLook { Cloth = new Color(0.4f, 0.6f, 0.35f), Accent = Palette.Wood, Hat = HatKind.Cap, HatColor = new Color(0.8f, 0.3f, 0.25f) },
                null, new[]
                {
                    "I've come every year for ten years. Aldric wins every year. I still come.",
                    "They say he changes his pair every time someone reaches the final. Keeps you guessing.",
                });
            AddNpc("Maren Hale", "stonemason's niece", new Vector2(10.5f, 229), 250,
                new CharacterLook("Barbarian", hue: 180, hat: false, cape: false, height: 1.65f, saturation: 0.7f),
                new PersonLook { Cloth = new Color(0.3f, 0.5f, 0.55f), Accent = Palette.WoodDark, Hat = HatKind.Kerchief, HatColor = Palette.Plaster },
                null, new[]
                {
                    "Uncle Dorran reached the final once. He still won't talk about it.",
                    "Dame Ottilie plays the Assassin and the Priest together. Everyone says that's the strongest pair in the realm.",
                });
            AddNpc("Old Colm", "retired finalist", new Vector2(-10.5f, 250), 90,
                new CharacterLook("Knight", hue: 0, hat: false, cape: true, height: 1.7f, saturation: 0.2f, brightness: 0.8f),
                new PersonLook { Cloth = new Color(0.45f, 0.45f, 0.45f), Accent = Palette.Gold, Hair = new Color(0.85f, 0.85f, 0.85f) },
                null, new[]
                {
                    "Three rounds, one after another, no rest between. Lose once and you start again from the first round.",
                    "Lord Casimir aims everything high. A tall Bulwark won't save you from him.",
                });
        }
    }
}
