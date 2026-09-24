using UnityEngine;

namespace Tabletop.World
{
    public enum HatKind { None, Pointed, WideBrim, Hood, Circlet, Cap, Kerchief }

    /// <summary>How a person looks (placeholder, built from primitives).</summary>
    public sealed class PersonLook
    {
        public Color Cloth = new Color(0.4f, 0.45f, 0.6f);
        public Color Accent = new Color(0.55f, 0.4f, 0.25f);
        public Color Skin = Palette.Skin;
        public Color Hair = new Color(0.35f, 0.22f, 0.12f);
        public HatKind Hat = HatKind.None;
        public Color HatColor = new Color(0.3f, 0.3f, 0.35f);
        public float Height = 1f;
        public bool Robe;
    }

    /// <summary>Placeholder props and figures for the world (low-poly primitive silhouettes).</summary>
    public static class WorldPieces
    {
        /// <summary>A person. Returns the root; child "Body" is the part that bobs when walking.</summary>
        public static Transform Person(WorldKit kit, Transform parent, string name, Vector3 pos, float yaw, PersonLook look)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = pos;
            root.localRotation = Quaternion.Euler(0, yaw, 0);
            var body = new GameObject("Body").transform;
            body.SetParent(root, false);
            body.localScale = Vector3.one * look.Height;
            var legs = new Color(0.25f, 0.22f, 0.2f);
            if (look.Robe)
            {
                kit.Cone("Robe", body, new Vector3(0, 0f, 0), new Vector3(0.8f, 1.35f, 0.8f), look.Cloth);
            }
            else
            {
                kit.Prim(PrimitiveType.Capsule, "LegL", body, new Vector3(-0.13f, 0.35f, 0), new Vector3(0.18f, 0.35f, 0.18f), legs);
                kit.Prim(PrimitiveType.Capsule, "LegR", body, new Vector3(0.13f, 0.35f, 0), new Vector3(0.18f, 0.35f, 0.18f), legs);
                kit.Prim(PrimitiveType.Capsule, "Torso", body, new Vector3(0, 0.98f, 0), new Vector3(0.55f, 0.42f, 0.4f), look.Cloth);
                kit.Box("Belt", body, new Vector3(0, 0.78f, 0), new Vector3(0.52f, 0.08f, 0.38f), look.Accent);
            }
            kit.Prim(PrimitiveType.Capsule, "ArmL", body, new Vector3(-0.33f, 1.0f, 0), new Vector3(0.14f, 0.3f, 0.14f), look.Cloth);
            kit.Prim(PrimitiveType.Capsule, "ArmR", body, new Vector3(0.33f, 1.0f, 0), new Vector3(0.14f, 0.3f, 0.14f), look.Cloth);
            kit.Prim(PrimitiveType.Sphere, "Head", body, new Vector3(0, 1.55f, 0), Vector3.one * 0.42f, look.Skin);
            kit.Prim(PrimitiveType.Sphere, "Hair", body, new Vector3(0, 1.6f, -0.05f), new Vector3(0.45f, 0.4f, 0.42f), look.Hair);
            kit.Box("EyeL", body, new Vector3(-0.08f, 1.57f, 0.2f), new Vector3(0.05f, 0.07f, 0.02f), new Color(0.1f, 0.1f, 0.12f));
            kit.Box("EyeR", body, new Vector3(0.08f, 1.57f, 0.2f), new Vector3(0.05f, 0.07f, 0.02f), new Color(0.1f, 0.1f, 0.12f));
            switch (look.Hat)
            {
                case HatKind.Pointed:
                    kit.Prim(PrimitiveType.Cylinder, "Brim", body, new Vector3(0, 1.72f, 0), new Vector3(0.62f, 0.02f, 0.62f), look.HatColor);
                    kit.Cone("Hat", body, new Vector3(0, 1.73f, 0), new Vector3(0.38f, 0.6f, 0.38f), look.HatColor);
                    break;
                case HatKind.WideBrim:
                    kit.Prim(PrimitiveType.Cylinder, "Brim", body, new Vector3(0, 1.74f, 0), new Vector3(0.75f, 0.02f, 0.75f), look.HatColor);
                    kit.Prim(PrimitiveType.Cylinder, "Crown", body, new Vector3(0, 1.84f, 0), new Vector3(0.36f, 0.1f, 0.36f), look.HatColor);
                    break;
                case HatKind.Hood:
                    kit.Prim(PrimitiveType.Sphere, "Hood", body, new Vector3(0, 1.6f, -0.06f), new Vector3(0.5f, 0.48f, 0.5f), look.HatColor);
                    kit.Cone("HoodTip", body, new Vector3(0, 1.78f, -0.12f), new Vector3(0.22f, 0.25f, 0.22f), look.HatColor);
                    break;
                case HatKind.Circlet:
                    kit.Prim(PrimitiveType.Cylinder, "Circlet", body, new Vector3(0, 1.74f, -0.01f), new Vector3(0.44f, 0.04f, 0.44f), Palette.Gold);
                    for (int i = 0; i < 3; i++)
                        kit.Cone("Point" + i, body, new Vector3((i - 1) * 0.13f, 1.76f, 0.17f), new Vector3(0.07f, 0.12f, 0.07f), Palette.Gold);
                    break;
                case HatKind.Cap:
                    kit.Prim(PrimitiveType.Sphere, "Cap", body, new Vector3(0, 1.7f, 0), new Vector3(0.44f, 0.22f, 0.44f), look.HatColor);
                    kit.Box("Visor", body, new Vector3(0, 1.66f, 0.2f), new Vector3(0.3f, 0.03f, 0.14f), look.HatColor);
                    break;
                case HatKind.Kerchief:
                    kit.Prim(PrimitiveType.Sphere, "Kerchief", body, new Vector3(0, 1.66f, -0.03f), new Vector3(0.46f, 0.3f, 0.46f), look.HatColor);
                    break;
            }
            return root;
        }

        /// <summary>A house with plaster walls, pitched roof, door and windows. Door faces local +z. Solid walls.</summary>
        public static Transform House(WorldKit kit, Transform parent, string name, Vector3 pos, float yaw, float width, float depth, float height,
            Color wall, Color roof, bool chimney = true)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = pos;
            root.localRotation = Quaternion.Euler(0, yaw, 0);
            kit.Box("Walls", root, new Vector3(0, height / 2f, 0), new Vector3(width, height, depth), wall, solid: true);
            kit.Box("Beam", root, new Vector3(0, height - 0.1f, depth / 2f + 0.02f), new Vector3(width + 0.1f, 0.2f, 0.06f), Palette.WoodDark);
            kit.Box("Base", root, new Vector3(0, 0.15f, 0), new Vector3(width + 0.2f, 0.3f, depth + 0.2f), Palette.StoneDark);
            // Prism is rotated 90 degrees so the ridge runs left-right (gables on the sides); its local x spans the house depth.
            kit.Prism("Roof", root, new Vector3(0, height, 0), new Vector3(depth + 0.8f, height * 0.6f, width + 0.8f), roof, 90);
            if (chimney) kit.Box("Chimney", root, new Vector3(width * 0.28f, height + height * 0.45f, -depth * 0.15f), new Vector3(0.6f, 1.4f, 0.6f), Palette.StoneDark);
            kit.Box("Door", root, new Vector3(0, 0.95f, depth / 2f + 0.03f), new Vector3(1.0f, 1.9f, 0.08f), Palette.WoodDark);
            kit.Box("DoorStep", root, new Vector3(0, 0.08f, depth / 2f + 0.4f), new Vector3(1.4f, 0.16f, 0.6f), Palette.Stone);
            float wx = width * 0.3f;
            foreach (float x in new[] { -wx, wx })
            {
                kit.Box("Window", root, new Vector3(x, height * 0.55f, depth / 2f + 0.03f), new Vector3(0.8f, 0.8f, 0.06f), Palette.WindowLit);
                kit.Box("Shutter", root, new Vector3(x, height * 0.55f + 0.45f, depth / 2f + 0.05f), new Vector3(1.0f, 0.1f, 0.08f), Palette.WoodDark);
            }
            return root;
        }

        public static Transform Tree(WorldKit kit, Transform parent, Vector3 pos, float scale, int variant)
        {
            var root = new GameObject("Tree").transform;
            root.SetParent(parent, false);
            root.localPosition = pos;
            root.localScale = Vector3.one * scale;
            kit.Prim(PrimitiveType.Cylinder, "Trunk", root, new Vector3(0, 0.8f, 0), new Vector3(0.35f, 0.8f, 0.35f), Palette.Trunk);
            var leaves = variant % 2 == 0 ? Palette.Leaves : Palette.LeavesLight;
            if (variant % 3 == 0)
            {
                kit.Prim(PrimitiveType.Sphere, "Crown", root, new Vector3(0, 2.4f, 0), new Vector3(2.0f, 1.8f, 2.0f), leaves);
                kit.Prim(PrimitiveType.Sphere, "Crown2", root, new Vector3(0.4f, 2.9f, 0.2f), new Vector3(1.3f, 1.2f, 1.3f), leaves * 1.1f);
            }
            else
            {
                kit.Cone("Low", root, new Vector3(0, 1.1f, 0), new Vector3(2.2f, 1.9f, 2.2f), leaves);
                kit.Cone("High", root, new Vector3(0, 2.2f, 0), new Vector3(1.6f, 1.7f, 1.6f), leaves * 1.08f);
            }
            return root;
        }

        public static void Rock(WorldKit kit, Transform parent, Vector3 pos, float size)
        {
            kit.Prim(PrimitiveType.Sphere, "Rock", parent, pos + new Vector3(0, size * 0.25f, 0), new Vector3(size, size * 0.6f, size * 0.8f), Palette.StoneDark);
        }

        public static void Bush(WorldKit kit, Transform parent, Vector3 pos, float size)
        {
            kit.Prim(PrimitiveType.Sphere, "Bush", parent, pos + new Vector3(0, size * 0.35f, 0), new Vector3(size, size * 0.7f, size), Palette.GrassDark);
        }

        public static void Flowers(WorldKit kit, Transform parent, Vector3 pos, int seed)
        {
            var colors = new[] { new Color(0.95f, 0.4f, 0.45f), new Color(0.98f, 0.85f, 0.3f), new Color(0.6f, 0.45f, 0.9f), Color.white };
            for (int i = 0; i < 5; i++)
            {
                float a = (seed * 37 + i * 71) % 360 * Mathf.Deg2Rad;
                float r = 0.3f + (i % 3) * 0.2f;
                kit.Prim(PrimitiveType.Sphere, "Flower", parent, pos + new Vector3(Mathf.Cos(a) * r, 0.15f, Mathf.Sin(a) * r), Vector3.one * 0.18f, colors[(seed + i) % colors.Length]);
            }
        }

        public static void Well(WorldKit kit, Transform parent, Vector3 pos)
        {
            var root = new GameObject("Well").transform;
            root.SetParent(parent, false);
            root.localPosition = pos;
            kit.Prim(PrimitiveType.Cylinder, "Ring", root, new Vector3(0, 0.45f, 0), new Vector3(1.8f, 0.45f, 1.8f), Palette.Stone, solid: true);
            kit.Prim(PrimitiveType.Cylinder, "Water", root, new Vector3(0, 0.88f, 0), new Vector3(1.4f, 0.02f, 1.4f), Palette.Water);
            kit.Box("PostL", root, new Vector3(-0.8f, 1.4f, 0), new Vector3(0.15f, 1.9f, 0.15f), Palette.Wood);
            kit.Box("PostR", root, new Vector3(0.8f, 1.4f, 0), new Vector3(0.15f, 1.9f, 0.15f), Palette.Wood);
            kit.Prism("Roof", root, new Vector3(0, 2.3f, 0), new Vector3(2.2f, 0.7f, 1.4f), Palette.RoofRed);
        }

        public static void Fountain(WorldKit kit, Transform parent, Vector3 pos)
        {
            var root = new GameObject("Fountain").transform;
            root.SetParent(parent, false);
            root.localPosition = pos;
            kit.Prim(PrimitiveType.Cylinder, "Basin", root, new Vector3(0, 0.35f, 0), new Vector3(4.2f, 0.35f, 4.2f), Palette.Stone, solid: true);
            kit.Prim(PrimitiveType.Cylinder, "Water", root, new Vector3(0, 0.66f, 0), new Vector3(3.7f, 0.03f, 3.7f), Palette.Water);
            kit.Prim(PrimitiveType.Cylinder, "Pillar", root, new Vector3(0, 1.2f, 0), new Vector3(0.5f, 0.9f, 0.5f), Palette.Stone);
            kit.Prim(PrimitiveType.Cylinder, "Bowl", root, new Vector3(0, 2.0f, 0), new Vector3(1.4f, 0.15f, 1.4f), Palette.Stone);
            kit.Prim(PrimitiveType.Sphere, "Spray", root, new Vector3(0, 2.3f, 0), new Vector3(0.6f, 0.4f, 0.6f), Palette.Water);
        }

        public static void Fence(WorldKit kit, Transform parent, Vector3 a, Vector3 b)
        {
            var dir = b - a;
            float len = dir.magnitude;
            int posts = Mathf.Max(2, Mathf.CeilToInt(len / 2f) + 1);
            for (int i = 0; i < posts; i++)
            {
                var p = Vector3.Lerp(a, b, i / (float)(posts - 1));
                kit.Box("Post", parent, p + new Vector3(0, 0.5f, 0), new Vector3(0.15f, 1.0f, 0.15f), Palette.Wood);
            }
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            var mid = (a + b) / 2f;
            kit.Box("RailTop", parent, mid + new Vector3(0, 0.8f, 0), new Vector3(0.08f, 0.1f, len), Palette.Wood, false, yaw);
            kit.Box("RailLow", parent, mid + new Vector3(0, 0.45f, 0), new Vector3(0.08f, 0.1f, len), Palette.Wood, false, yaw);
        }

        public static void Stall(WorldKit kit, Transform parent, Vector3 pos, float yaw, Color awning)
        {
            var root = new GameObject("Stall").transform;
            root.SetParent(parent, false);
            root.localPosition = pos;
            root.localRotation = Quaternion.Euler(0, yaw, 0);
            kit.Box("Counter", root, new Vector3(0, 0.55f, 0), new Vector3(3.2f, 1.1f, 1.1f), Palette.Wood, solid: true);
            foreach (float x in new[] { -1.5f, 1.5f })
                kit.Box("Pole", root, new Vector3(x, 1.4f, -0.4f), new Vector3(0.12f, 2.8f, 0.12f), Palette.WoodDark);
            for (int i = 0; i < 4; i++)
                kit.Box("Stripe", root, new Vector3(-1.2f + i * 0.8f, 2.75f, -0.1f), new Vector3(0.8f, 0.1f, 1.8f), i % 2 == 0 ? awning : Color.white);
            var goods = new[] { new Color(0.9f, 0.3f, 0.25f), new Color(0.95f, 0.8f, 0.3f), new Color(0.45f, 0.7f, 0.3f) };
            for (int i = 0; i < 6; i++)
                kit.Prim(PrimitiveType.Sphere, "Good", root, new Vector3(-1.2f + i * 0.48f, 1.2f, 0.1f), Vector3.one * 0.3f, goods[i % 3]);
        }

        public static void Lamp(WorldKit kit, Transform parent, Vector3 pos)
        {
            kit.Box("LampPost", parent, pos + new Vector3(0, 1.3f, 0), new Vector3(0.15f, 2.6f, 0.15f), Palette.WoodDark);
            kit.Box("Lamp", parent, pos + new Vector3(0, 2.7f, 0), new Vector3(0.4f, 0.45f, 0.4f), Palette.WindowLit);
        }

        public static void Bench(WorldKit kit, Transform parent, Vector3 pos, float yaw)
        {
            kit.Box("Seat", parent, pos + new Vector3(0, 0.45f, 0), new Vector3(1.8f, 0.12f, 0.5f), Palette.Wood, false, yaw);
            kit.Box("Legs", parent, pos + new Vector3(0, 0.2f, 0), new Vector3(1.6f, 0.4f, 0.3f), Palette.WoodDark, false, yaw);
        }

        public static void Campfire(WorldKit kit, Transform parent, Vector3 pos)
        {
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                kit.Prim(PrimitiveType.Sphere, "Stone", parent, pos + new Vector3(Mathf.Cos(a) * 0.7f, 0.1f, Mathf.Sin(a) * 0.7f), Vector3.one * 0.3f, Palette.StoneDark);
            }
            kit.Box("Log1", parent, pos + new Vector3(0, 0.15f, 0), new Vector3(0.9f, 0.15f, 0.15f), Palette.Trunk, false, 30);
            kit.Box("Log2", parent, pos + new Vector3(0, 0.15f, 0), new Vector3(0.9f, 0.15f, 0.15f), Palette.Trunk, false, -30);
            kit.Cone("Flame", parent, pos + new Vector3(0, 0.15f, 0), new Vector3(0.5f, 0.8f, 0.5f), Palette.Fire);
            kit.Cone("FlameCore", parent, pos + new Vector3(0, 0.15f, 0), new Vector3(0.28f, 0.55f, 0.28f), new Color(1f, 0.9f, 0.4f));
        }

        public static void Signpost(WorldKit kit, Transform parent, Vector3 pos, float yaw)
        {
            kit.Box("SignPost", parent, pos + new Vector3(0, 1.0f, 0), new Vector3(0.15f, 2.0f, 0.15f), Palette.WoodDark);
            kit.Box("SignBoard", parent, pos + new Vector3(0, 1.7f, 0.08f), new Vector3(1.6f, 0.55f, 0.08f), Palette.Wood, false, yaw);
        }

        /// <summary>A small replica Reels table (the thing everyone plays).</summary>
        public static Transform GameTable(WorldKit kit, Transform parent, Vector3 pos, float scale)
        {
            var root = new GameObject("GameTable").transform;
            root.SetParent(parent, false);
            root.localPosition = pos;
            root.localScale = Vector3.one * scale;
            kit.Box("Top", root, new Vector3(0, 0.95f, 0), new Vector3(2.6f, 0.12f, 1.8f), Palette.Wood, solid: true);
            foreach (var p in new[] { new Vector3(-1.1f, 0, -0.7f), new Vector3(1.1f, 0, -0.7f), new Vector3(-1.1f, 0, 0.7f), new Vector3(1.1f, 0, 0.7f) })
                kit.Box("Leg", root, p + new Vector3(0, 0.45f, 0), new Vector3(0.12f, 0.9f, 0.12f), Palette.WoodDark);
            kit.Box("Felt", root, new Vector3(0, 1.02f, 0), new Vector3(2.3f, 0.02f, 1.5f), new Color(0.22f, 0.4f, 0.3f));
            for (int s = 0; s < 2; s++)
            {
                float z = s == 0 ? -0.55f : 0.55f;
                for (int i = 0; i < 5; i++)
                    kit.Box("Reel", root, new Vector3(-0.6f + i * 0.3f, 1.12f, z), new Vector3(0.22f, 0.18f, 0.22f), i % 2 == 0 ? new Color(0.95f, 0.55f, 0.2f) : new Color(0.3f, 0.8f, 0.85f));
                kit.Prim(PrimitiveType.Cylinder, "Crown", root, new Vector3(0, 1.12f, z * 0.45f), new Vector3(0.25f, 0.08f, 0.25f), Palette.Gold);
            }
            return root;
        }

        public static Transform Chair(WorldKit kit, Transform parent, Vector3 pos, float yaw)
        {
            var root = new GameObject("Chair").transform;
            root.SetParent(parent, false);
            root.localPosition = pos;
            root.localRotation = Quaternion.Euler(0, yaw, 0);
            kit.Box("Seat", root, new Vector3(0, 0.5f, 0), new Vector3(0.8f, 0.1f, 0.8f), Palette.Wood);
            kit.Box("Back", root, new Vector3(0, 1.05f, -0.36f), new Vector3(0.8f, 1.0f, 0.08f), Palette.Wood);
            foreach (var p in new[] { new Vector3(-0.33f, 0, -0.33f), new Vector3(0.33f, 0, -0.33f), new Vector3(-0.33f, 0, 0.33f), new Vector3(0.33f, 0, 0.33f) })
                kit.Box("Leg", root, p + new Vector3(0, 0.25f, 0), new Vector3(0.08f, 0.5f, 0.08f), Palette.WoodDark);
            return root;
        }
    }
}
