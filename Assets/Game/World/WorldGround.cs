using System;
using UnityEngine;

namespace Tabletop.World
{
    /// <summary>
    /// The shape of the land (D-033). One smooth height field decides where the player stands (<see cref="Walk"/>);
    /// the visible terrain adds hills that rise away from anywhere walkable, keeps the camera side (south) low so
    /// the view is never blocked, and carves the Willow Stream. Pure functions of position: deterministic and
    /// testable, and the player never needs a physics floor.
    /// </summary>
    public static class WorldGround
    {
        public const float OutpostLevel = 12f;
        public const float PitFloor = 8.5f;
        public const float StreamZ = 62f;
        public const float StreamHalfWidth = 2.4f;
        public static readonly Vector2 PitCenter = new Vector2(84f, 205f);
        public static readonly Vector2 KnollCenter = new Vector2(101f, 171f);
        /// <summary>Mirrorwater, the lake at Lanternmere that feeds the Willow Stream (D-038).</summary>
        public static readonly Vector2 LakeCenter = new Vector2(-85f, 72f);
        public const float LakeRadius = 17f;
        /// <summary>Interiors (the Champion's Hall) are built east of this, away from the land, at height 0.</summary>
        public const float InteriorThreshold = 185f;

        /// <summary>Terrain extents (x, z) and the vertical range it can represent.</summary>
        public static readonly Rect Extent = new Rect(-160f, -80f, 330f, 380f);
        public const float MinHeight = -8f;
        public const float MaxHeight = 52f;

        private static readonly Vector2[] Valley =
        {
            new Vector2(-400f, 0f), new Vector2(22f, 0f), new Vector2(38f, 1.2f), new Vector2(48f, 1.2f),
            new Vector2(57f, 0.3f), new Vector2(67f, 0.3f), new Vector2(106f, 2.5f), new Vector2(600f, 2.5f),
        };

        public static float Smooth01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Height of the main valley floor along the journey north (villages flat, the road rolling).</summary>
        public static float ValleyHeight(float z)
        {
            for (int i = 0; i < Valley.Length - 1; i++)
            {
                var a = Valley[i];
                var b = Valley[i + 1];
                if (z <= b.x) return Mathf.Lerp(a.y, b.y, Smooth01((z - a.x) / (b.x - a.x)));
            }
            return Valley[Valley.Length - 1].y;
        }

        /// <summary>Places with a level of their own (D-038): centre, flat radius, blend distance, height.</summary>
        private static readonly (Vector2 c, float r, float fall, float level)[] Levels =
        {
            (new Vector2(-85f, 92f), 26f, 14f, 1.2f),   // Lanternmere and its lake shore
            (new Vector2(-62f, 212f), 16f, 14f, 0.2f),  // Duskhollow, sunk into the pinewood
            (new Vector2(0f, 238f), 34f, 16f, 6.5f),    // Crownhold, on its hill
        };

        /// <summary>How far into the eastern highlands a point is (0 in the valley, 1 on the outpost plateau).</summary>
        public static float Highland(float x) => Smooth01((x - 28f) / 32f);

        /// <summary>Where feet go: the smooth ground everyone walks on (no hills, no stream bed).</summary>
        public static float Walk(float x, float z)
        {
            if (x > InteriorThreshold) return 0f; // interiors (the Champion's Hall) are built away from the land, at 0
            float east = Highland(x);
            float h = Mathf.Lerp(ValleyHeight(z), OutpostLevel, east);
            if (east > 0f)
            {
                float dp = Vector2.Distance(new Vector2(x, z), PitCenter);
                h -= (OutpostLevel - PitFloor) * (1f - Smooth01((dp - 9f) / 6f)) * east;
                float dk = Vector2.Distance(new Vector2(x, z), KnollCenter);
                h += 3f * (1f - Smooth01((dk - 3.5f) / 5f)) * east;
            }
            foreach (var l in Levels)
            {
                float d = Vector2.Distance(new Vector2(x, z), l.c);
                if (d < l.r + l.fall) h = Mathf.Lerp(h, l.level, 1f - Smooth01((d - l.r) / l.fall));
            }
            return h;
        }

        public static Vector3 OnGround(float x, float z) => new Vector3(x, Walk(x, z), z);
        public static Vector3 OnGround(Vector3 p) => new Vector3(p.x, Walk(p.x, p.z), p.z);

        /// <summary>0..1: how much of the stream bed is here (the stream fades out as the land climbs east).</summary>
        public static float StreamMask(float x, float z)
        {
            float across = Mathf.Abs(z - StreamZ);
            // Fades out east as the land climbs, and west where it leaves the lake.
            return (1f - Smooth01((across - StreamHalfWidth) / 2.2f)) * (1f - Smooth01((x - 22f) / 10f)) * Smooth01((x + 76f) / 5f);
        }

        /// <summary>0..1: how much of the lake bed is here.</summary>
        public static float LakeMask(float x, float z)
        {
            float d = Vector2.Distance(new Vector2(x, z), LakeCenter);
            return 1f - Smooth01((d - (LakeRadius - 7f)) / 9f); // a gentle beach, not a bank
        }

        /// <summary>Water too deep to walk into (the stream and the lake), except where a bridge or pier crosses it.</summary>
        public static bool WaterAt(float x, float z) => x < InteriorThreshold && (StreamMask(x, z) > 0.3f || LakeMask(x, z) > 0.3f);

        /// <summary>
        /// Where feet go anywhere in the world (D-038): the smooth walking surface on paths, villages, bridges and
        /// piers; the visible land (hills included) everywhere else the player can wander.
        /// </summary>
        public static float Feet(float x, float z, WalkableArea paths)
        {
            if (x > InteriorThreshold || paths == null || paths.Contains(x, z)) return Walk(x, z);
            return Terrain(x, z, paths);
        }

        /// <summary>Steepness of the visible land in degrees.</summary>
        public static float SlopeDegrees(float x, float z, WalkableArea paths)
        {
            const float e = 0.8f;
            float dx = Terrain(x + e, z, paths) - Terrain(x - e, z, paths);
            float dz = Terrain(x, z + e, paths) - Terrain(x, z - e, paths);
            return Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz) / (2f * e)) * Mathf.Rad2Deg;
        }

        public static float WaterLevel => Walk(-5.5f, StreamZ) - 0.7f;

        /// <summary>The visible land: the walking surface, plus hills away from walkable ground, minus the stream bed.</summary>
        public static float Terrain(float x, float z, WalkableArea walkable)
        {
            float f = Walk(x, z);
            float stream = StreamMask(x, z);
            float carve = 1.8f * stream + 2.8f * LakeMask(x, z);
            float lakeShore = 1f - Smooth01((Vector2.Distance(new Vector2(x, z), LakeCenter) - LakeRadius) / 8f);
            stream = Mathf.Max(stream, lakeShore);
            if (walkable == null) return f - carve;
            float d = walkable.Distance(x, z, out _);
            if (d <= 0f) return f - carve;
            // > 0: walkable ground lies north of this point, so it is in front of it, toward the camera. Measured as how
            // much closer the ground is from 10 m further north: continuous everywhere, so the land has no creases
            // where the nearest path changes (D-038: the player can walk there now).
            float south = d - walkable.Distance(x, z + 10f, out _);
            float n1 = Mathf.PerlinNoise(x * 0.021f + 13.7f, z * 0.021f + 4.1f);
            float n2 = Mathf.PerlinNoise(x * 0.09f + 91.3f, z * 0.09f + 27.9f);
            float amp = 3.5f + 5f * n1;
            float camera = Smooth01(south / 10f);
            amp = Mathf.Lerp(amp, 1.6f, camera);
            // Rolling hills near the paths, real mountains only far away (and never on the camera side).
            float hills = amp * Smooth01((d - 2f) / 20f) + (1f - camera) * 18f * Smooth01((d - 30f) / 45f) * (0.4f + n1);
            hills += 0.6f * (n2 - 0.5f) * Smooth01(d / 4f);
            return f + hills * (1f - stream) - carve;
        }

        /// <summary>
        /// Builds a Unity Terrain for the land. <paramref name="paint"/> returns layer weights (grass, meadow, dirt,
        /// stone, rock) for a point and its slope in degrees.
        /// </summary>
        public static Terrain BuildTerrain(Transform parent, WalkableArea walkable, WorldLook look, Func<float, float, float, float[]> paint)
        {
            const int res = 513;
            const int alphaRes = 256;
            var data = new TerrainData { heightmapResolution = res };
            data.size = new Vector3(Extent.width, MaxHeight - MinHeight, Extent.height);
            var heights = new float[res, res];
            for (int zi = 0; zi < res; zi++)
            {
                float z = Extent.yMin + Extent.height * zi / (res - 1f);
                for (int xi = 0; xi < res; xi++)
                {
                    float x = Extent.xMin + Extent.width * xi / (res - 1f);
                    heights[zi, xi] = Mathf.Clamp01((Terrain(x, z, walkable) - MinHeight) / (MaxHeight - MinHeight));
                }
            }
            data.SetHeights(0, 0, heights);

            var layers = Layers(look);
            data.terrainLayers = layers;
            data.alphamapResolution = alphaRes;
            var alpha = new float[alphaRes, alphaRes, layers.Length];
            for (int zi = 0; zi < alphaRes; zi++)
            {
                float nz = zi / (alphaRes - 1f);
                float z = Extent.yMin + Extent.height * nz;
                for (int xi = 0; xi < alphaRes; xi++)
                {
                    float nx = xi / (alphaRes - 1f);
                    float x = Extent.xMin + Extent.width * nx;
                    float slope = data.GetSteepness(nx, nz);
                    var w = paint(x, z, slope);
                    float sum = 0f;
                    for (int l = 0; l < layers.Length; l++) sum += w[l];
                    if (sum <= 0f) { w[0] = 1f; sum = 1f; }
                    for (int l = 0; l < layers.Length; l++) alpha[zi, xi, l] = w[l] / sum;
                }
            }
            data.SetAlphamaps(0, 0, alpha);

            var go = new GameObject("Terrain");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(Extent.xMin, MinHeight, Extent.yMin);
            var terrain = go.AddComponent<Terrain>();
            terrain.terrainData = data;
            if (look != null && look.terrainMaterial != null) terrain.materialTemplate = look.terrainMaterial;
            terrain.heightmapPixelError = 4f;
            terrain.basemapDistance = 400f;
            // Not instanced: the terrain exists only at runtime, so a build would strip the instanced shader
            // variants and draw the land flat (found by the build self-check).
            terrain.drawInstanced = false;
            terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return terrain;
        }

        /// <summary>The five terrain layers, or plain colours when the packs are missing.</summary>
        private static TerrainLayer[] Layers(WorldLook look)
        {
            TerrainLayer Flat(Color c, string name)
            {
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = name };
                var px = new Color[16];
                for (int i = 0; i < px.Length; i++) px[i] = c;
                tex.SetPixels(px);
                tex.Apply();
                return new TerrainLayer { diffuseTexture = tex, tileSize = new Vector2(4, 4), name = name };
            }
            return new[]
            {
                look != null && look.grass != null ? look.grass : Flat(Palette.Grass, "Grass"),
                look != null && look.meadow != null ? look.meadow : Flat(Palette.GrassDark, "Meadow"),
                look != null && look.dirt != null ? look.dirt : Flat(Palette.Dirt, "Dirt"),
                look != null && look.stone != null ? look.stone : Flat(Palette.Stone, "Stone"),
                look != null && look.rock != null ? look.rock : Flat(Palette.StoneDark, "Rock"),
            };
        }
    }
}
