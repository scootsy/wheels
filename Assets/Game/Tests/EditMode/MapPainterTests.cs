using NUnit.Framework;
using Tabletop.World;

namespace Tabletop.Tests.EditMode
{
    /// <summary>D-041: the world map is painted from the land's data, north up, the same every time.</summary>
    public class MapPainterTests
    {
        private const int W = 100, H = 120;

        /// <summary>A 100 m x 120 m test land: grass, a pond in the south-west, one house in the north-east.</summary>
        private static MapPainter.Source Land()
        {
            var s = new MapPainter.Source { XMin = -50, ZMin = 0, SizeX = 100, SizeZ = 120 };
            s.Alpha = new float[16, 16, 5];
            for (int z = 0; z < 16; z++) for (int x = 0; x < 16; x++) s.Alpha[z, x, 0] = 1f;
            s.Heights = new float[17, 17];
            s.Water = (x, z) => (x + 30) * (x + 30) + (z - 20) * (z - 20) < 64 ? 1f : 0f;
            s.Deck = (x, z) => false;
            s.Buildings.Add((30f, 100f, 12f, 10f, 0f));
            return s;
        }

        private static (int r, int g, int b, int a) Pixel(byte[] px, float worldX, float worldZ)
        {
            int x = (int)(worldX + 50), z = (int)worldZ; // 1 m per pixel; row 0 is the south edge
            int o = (z * W + x) * 4;
            return (px[o], px[o + 1], px[o + 2], px[o + 3]);
        }

        [Test]
        public void Paint_IsTheSameEveryTime_AndFullyOpaque()
        {
            var a = MapPainter.Paint(Land(), W, H);
            var b = MapPainter.Paint(Land(), W, H);
            Assert.AreEqual(W * H * 4, a.Length);
            CollectionAssert.AreEqual(a, b);
            for (int i = 3; i < a.Length; i += 4) Assert.AreEqual(255, a[i]);
        }

        [Test]
        public void Paint_PutsWaterAndBuildingsWhereTheyAre_NorthUp()
        {
            var px = MapPainter.Paint(Land(), W, H);
            var pond = Pixel(px, -30, 20);
            Assert.Greater(pond.b, pond.r, "the pond (south-west) is blue");
            var grass = Pixel(px, 0, 60);
            Assert.Greater(grass.g, grass.b, "open land is green");
            var roof = Pixel(px, 30, 100);
            Assert.Greater(roof.r, roof.g + 20, "the house (north-east, near the top rows) has a red roof");
            var mirrored = Pixel(px, 30, 20);
            Assert.Greater(mirrored.g, mirrored.r - 10, "nothing painted at the mirror position (the map is not flipped)");
        }

        [Test]
        public void Arrow_PointsNorth()
        {
            const int n = 64;
            var px = MapPainter.Arrow(n);
            int Alpha(int x, int y) => px[(y * n + x) * 4 + 3];
            Assert.AreEqual(255, Alpha(n / 2, n - 8), "the tip is near the top row");
            Assert.AreEqual(0, Alpha(n / 2, 4), "the notch leaves the bottom middle open");
            Assert.AreEqual(0, Alpha(2, n - 3), "corners are clear");
            Assert.AreEqual(255, Alpha(n / 2 - 18, 10), "a wing at the bottom left");
        }
    }
}
