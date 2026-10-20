using System;
using System.Collections.Generic;

namespace Tabletop.World
{
    /// <summary>
    /// Paints the world map (D-041) from the land's own data: the terrain paint (grass, meadow, dirt, stone, rock),
    /// the heights (hill shading), water, building footprints and trees. Plain C# with no engine types, so it is
    /// deterministic and can be checked anywhere; <see cref="WorldMap"/> feeds it from the built world once at load.
    /// Pixels are RGBA, row 0 at the south edge, x east.
    /// </summary>
    public static class MapPainter
    {
        /// <summary>Everything the painter reads. Coordinates are world metres on the XZ plane.</summary>
        public sealed class Source
        {
            public float XMin, ZMin, SizeX, SizeZ;
            /// <summary>Terrain layer weights [z, x, layer] (grass, meadow, dirt, stone, rock) over the extent, or null.</summary>
            public float[,,] Alpha;
            /// <summary>Land height in metres [z, x] over the extent, or null.</summary>
            public float[,] Heights;
            /// <summary>0..1: how much water is at (x, z).</summary>
            public Func<float, float, float> Water;
            /// <summary>True where a bridge or pier crosses water.</summary>
            public Func<float, float, bool> Deck;
            public List<(float x, float z, float width, float depth, float yaw)> Buildings = new List<(float, float, float, float, float)>();
            public List<(float x, float z)> Trees = new List<(float, float)>();
        }

        // Map inks: a warm, slightly faded version of the terrain layers.
        private static readonly float[][] LayerInk =
        {
            new[] { 0.56f, 0.66f, 0.39f }, // grass
            new[] { 0.64f, 0.65f, 0.40f }, // meadow / heather
            new[] { 0.80f, 0.68f, 0.48f }, // dirt: paths
            new[] { 0.78f, 0.75f, 0.67f }, // stone: plazas
            new[] { 0.55f, 0.51f, 0.47f }, // rock
        };
        private static readonly float[] WaterInk = { 0.36f, 0.56f, 0.70f };
        private static readonly float[] ShoreInk = { 0.24f, 0.38f, 0.48f };
        private static readonly float[] DeckInk = { 0.55f, 0.40f, 0.25f };
        private static readonly float[] OutlineInk = { 0.24f, 0.15f, 0.10f };
        private static readonly float[] TreeInk = { 0.20f, 0.36f, 0.20f };
        private static readonly float[] TreeLight = { 0.33f, 0.50f, 0.28f };
        private static readonly float[][] RoofInks =
        {
            new[] { 0.70f, 0.34f, 0.26f },
            new[] { 0.52f, 0.36f, 0.25f },
            new[] { 0.40f, 0.46f, 0.58f },
        };

        /// <summary>Map pixels for <paramref name="src"/>: <paramref name="width"/> x <paramref name="height"/> RGBA bytes.</summary>
        public static byte[] Paint(Source src, int width, int height)
        {
            var rgb = new float[width * height * 3];
            float mppX = src.SizeX / width, mppZ = src.SizeZ / height;
            for (int zi = 0; zi < height; zi++)
            {
                float v = (zi + 0.5f) / height;
                float z = src.ZMin + v * src.SizeZ;
                for (int xi = 0; xi < width; xi++)
                {
                    float u = (xi + 0.5f) / width;
                    float x = src.XMin + u * src.SizeX;
                    var c = Ground(src, u, v);
                    float shade = Shade(src, u, v, mppX, mppZ);
                    for (int k = 0; k < 3; k++) c[k] *= shade;
                    float water = src.Water != null ? src.Water(x, z) : 0f;
                    if (water > 0.22f)
                    {
                        bool deck = water > 0.3f && src.Deck != null && src.Deck(x, z);
                        var ink = deck ? DeckInk : Mix(ShoreInk, WaterInk, Smooth((water - 0.22f) / 0.2f));
                        float a = Smooth((water - 0.22f) / 0.05f);
                        for (int k = 0; k < 3; k++) c[k] = Lerp(c[k], ink[k], a);
                    }
                    int i = (zi * width + xi) * 3;
                    rgb[i] = c[0]; rgb[i + 1] = c[1]; rgb[i + 2] = c[2];
                }
            }

            // Trees: a soft shadow to the south-east, a dark crown, a lit edge to the north-west.
            foreach (var t in src.Trees)
            {
                Disc(rgb, width, height, src, t.x + 0.6f, t.z - 0.6f, 1.7f, new[] { 0.12f, 0.16f, 0.10f }, 0.35f);
                Disc(rgb, width, height, src, t.x, t.z, 1.55f, TreeInk, 0.92f);
                Disc(rgb, width, height, src, t.x - 0.45f, t.z + 0.45f, 0.75f, TreeLight, 0.8f);
            }

            // Buildings: a roof with a dark outline and a shadow to the south-east.
            for (int b = 0; b < src.Buildings.Count; b++)
            {
                var h = src.Buildings[b];
                Box(rgb, width, height, src, h.x + 0.9f, h.z - 0.9f, h.width, h.depth, h.yaw, new[] { 0.1f, 0.08f, 0.06f }, null, 0.35f);
                Box(rgb, width, height, src, h.x, h.z, h.width, h.depth, h.yaw, RoofInks[b % RoofInks.Length], OutlineInk, 1f);
            }

            // Old paper: a warm cast, a faint grain, and darker edges.
            var bytes = new byte[width * height * 4];
            float edge = Math.Max(6f, width * 0.03f);
            for (int zi = 0; zi < height; zi++)
                for (int xi = 0; xi < width; xi++)
                {
                    int i = (zi * width + xi) * 3;
                    float grain = 0.97f + 0.06f * Hash(xi, zi);
                    float d = Math.Min(Math.Min(xi, width - 1 - xi), Math.Min(zi, height - 1 - zi));
                    float rim = 0.72f + 0.28f * Smooth(d / edge);
                    float r = rgb[i] * 1.02f, g = rgb[i + 1] * 0.98f, bl = rgb[i + 2] * 0.88f;
                    int o = (zi * width + xi) * 4;
                    bytes[o] = ToByte(r * grain * rim);
                    bytes[o + 1] = ToByte(g * grain * rim);
                    bytes[o + 2] = ToByte(bl * grain * rim);
                    bytes[o + 3] = 255;
                }
            return bytes;
        }

        /// <summary>A gilt arrowhead pointing north (up) with a dark outline, <paramref name="size"/> square, RGBA.</summary>
        public static byte[] Arrow(int size)
        {
            var bytes = new byte[size * size * 4];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float px = (x + 0.5f) / size * 2f - 1f, py = (y + 0.5f) / size * 2f - 1f;
                    // Inside an arrowhead: tip at the top, two wings, a notch at the bottom.
                    float inside = ArrowField(px, py);
                    float outer = ArrowField(px * 0.86f, py * 0.86f + 0.02f);
                    int o = (y * size + x) * 4;
                    if (inside > 0f)
                    {
                        bytes[o] = 242; bytes[o + 1] = 190; bytes[o + 2] = 92; bytes[o + 3] = 255;
                    }
                    else if (outer > 0f)
                    {
                        bytes[o] = 40; bytes[o + 1] = 22; bytes[o + 2] = 12; bytes[o + 3] = 255;
                    }
                }
            return bytes;
        }

        private static float ArrowField(float x, float y)
        {
            // Tip at (0, 0.8), wings at (+-0.62, -0.7), notch at (0, -0.32).
            float left = (x + 0.62f) * (0.8f + 0.7f) - (y + 0.7f) * (0f + 0.62f);  // > 0 right of the left edge
            float right = (0.62f - x) * (0.8f + 0.7f) - (y + 0.7f) * 0.62f;       // > 0 left of the right edge
            float notch = y + 0.32f + Math.Abs(x) * (0.38f / 0.62f);              // > 0 above the notch
            return Math.Min(left, Math.Min(right, notch));
        }

        // ------------------------------------------------------------------ sampling

        private static float[] Ground(Source s, float u, float v)
        {
            var c = new float[3];
            if (s.Alpha == null) { Array.Copy(LayerInk[0], c, 3); return c; }
            int layers = Math.Min(s.Alpha.GetLength(2), LayerInk.Length);
            float sum = 0f;
            for (int l = 0; l < layers; l++)
            {
                float w = Sample(s.Alpha, l, u, v);
                sum += w;
                for (int k = 0; k < 3; k++) c[k] += w * LayerInk[l][k];
            }
            if (sum > 0.001f) for (int k = 0; k < 3; k++) c[k] /= sum;
            else Array.Copy(LayerInk[0], c, 3);
            return c;
        }

        /// <summary>Hill shading lit from the north-west, plus a lighter tint the higher the land.</summary>
        private static float Shade(Source s, float u, float v, float mppX, float mppZ)
        {
            if (s.Heights == null) return 1f;
            float du = 1.5f / s.Heights.GetLength(1), dv = 1.5f / s.Heights.GetLength(0);
            float dhdx = (Sample(s.Heights, u + du, v) - Sample(s.Heights, u - du, v)) / (2f * du * s.SizeX);
            float dhdz = (Sample(s.Heights, u, v + dv) - Sample(s.Heights, u, v - dv)) / (2f * dv * s.SizeZ);
            // Normal ~ (-dh/dx, 1, -dh/dz); light comes from (-x, +z).
            float lit = 0.7071f * (dhdx - dhdz);
            float shade = Clamp(1f + 0.9f * lit, 0.62f, 1.25f);
            float height = Sample(s.Heights, u, v);
            return shade * (0.94f + 0.12f * Clamp(height / 24f, 0f, 1f));
        }

        private static float Sample(float[,] a, float u, float v)
        {
            int h = a.GetLength(0), w = a.GetLength(1);
            float fx = Clamp(u, 0f, 1f) * (w - 1), fz = Clamp(v, 0f, 1f) * (h - 1);
            int x0 = (int)fx, z0 = (int)fz;
            int x1 = Math.Min(x0 + 1, w - 1), z1 = Math.Min(z0 + 1, h - 1);
            float tx = fx - x0, tz = fz - z0;
            return Lerp(Lerp(a[z0, x0], a[z0, x1], tx), Lerp(a[z1, x0], a[z1, x1], tx), tz);
        }

        private static float Sample(float[,,] a, int layer, float u, float v)
        {
            int h = a.GetLength(0), w = a.GetLength(1);
            float fx = Clamp(u, 0f, 1f) * (w - 1), fz = Clamp(v, 0f, 1f) * (h - 1);
            int x0 = (int)fx, z0 = (int)fz;
            int x1 = Math.Min(x0 + 1, w - 1), z1 = Math.Min(z0 + 1, h - 1);
            float tx = fx - x0, tz = fz - z0;
            return Lerp(Lerp(a[z0, x0, layer], a[z0, x1, layer], tx), Lerp(a[z1, x0, layer], a[z1, x1, layer], tx), tz);
        }

        // ------------------------------------------------------------------ stamping

        private static void Disc(float[] rgb, int w, int h, Source s, float cx, float cz, float radius, float[] ink, float opacity)
        {
            float mpp = s.SizeX / w;
            int px = (int)((cx - s.XMin) / s.SizeX * w), pz = (int)((cz - s.ZMin) / s.SizeZ * h);
            int r = (int)Math.Ceiling(radius / mpp) + 1;
            for (int z = pz - r; z <= pz + r; z++)
                for (int x = px - r; x <= px + r; x++)
                {
                    if (x < 0 || z < 0 || x >= w || z >= h) continue;
                    float wx = s.XMin + (x + 0.5f) / w * s.SizeX, wz = s.ZMin + (z + 0.5f) / h * s.SizeZ;
                    float d = (float)Math.Sqrt((wx - cx) * (wx - cx) + (wz - cz) * (wz - cz));
                    float a = opacity * (1f - Smooth((d - radius + mpp) / mpp));
                    if (a <= 0f) continue;
                    Blend(rgb, (z * w + x) * 3, ink, a);
                }
        }

        private static void Box(float[] rgb, int w, int h, Source s, float cx, float cz, float width, float depth, float yaw,
            float[] ink, float[] outline, float opacity)
        {
            float mpp = s.SizeX / w;
            float rad = yaw * (float)Math.PI / 180f, cos = (float)Math.Cos(rad), sin = (float)Math.Sin(rad);
            float reach = 0.5f * (float)Math.Sqrt(width * width + depth * depth) + mpp;
            int x0 = (int)((cx - reach - s.XMin) / s.SizeX * w), x1 = (int)((cx + reach - s.XMin) / s.SizeX * w) + 1;
            int z0 = (int)((cz - reach - s.ZMin) / s.SizeZ * h), z1 = (int)((cz + reach - s.ZMin) / s.SizeZ * h) + 1;
            float line = Math.Max(0.45f, mpp * 1.2f);
            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                {
                    if (x < 0 || z < 0 || x >= w || z >= h) continue;
                    float dx = s.XMin + (x + 0.5f) / w * s.SizeX - cx, dz = s.ZMin + (z + 0.5f) / h * s.SizeZ - cz;
                    // Into the building's own axes (Unity yaw turns +x toward -z).
                    float lx = dx * cos - dz * sin, lz = dx * sin + dz * cos;
                    float ex = width / 2f - Math.Abs(lx), ez = depth / 2f - Math.Abs(lz);
                    float inner = Math.Min(ex, ez);
                    if (inner < 0f) continue;
                    int i = (z * w + x) * 3;
                    Blend(rgb, i, outline != null && inner < line ? outline : ink, opacity);
                }
        }

        private static void Blend(float[] rgb, int i, float[] ink, float a)
        {
            for (int k = 0; k < 3; k++) rgb[i + k] = Lerp(rgb[i + k], ink[k], a);
        }

        // ------------------------------------------------------------------ helpers

        private static float[] Mix(float[] a, float[] b, float t) => new[] { Lerp(a[0], b[0], t), Lerp(a[1], b[1], t), Lerp(a[2], b[2], t) };
        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
        private static float Smooth(float t) { t = Clamp(t, 0f, 1f); return t * t * (3f - 2f * t); }
        private static byte ToByte(float v) => (byte)Math.Round(Clamp(v, 0f, 1f) * 255f);

        /// <summary>Deterministic 0..1 grain for a pixel.</summary>
        private static float Hash(int x, int y)
        {
            unchecked
            {
                uint n = (uint)(x * 374761393 + y * 668265263);
                n = (n ^ (n >> 13)) * 1274126177u;
                return ((n ^ (n >> 16)) & 0xFFFF) / 65535f;
            }
        }
    }
}
