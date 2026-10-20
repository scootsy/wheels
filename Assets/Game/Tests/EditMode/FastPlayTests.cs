using NUnit.Framework;
using Tabletop.Application;
using Tabletop.World;
using UnityEngine;

namespace Tabletop.Tests.EditMode
{
    /// <summary>D-042: fast play is a saved player preference that only the player switches.</summary>
    public class FastPlayTests
    {
        [SetUp]
        public void Reset() => GameFlow.Reset();

        [TearDown]
        public void TearDown() => GameFlow.Reset();

        [Test]
        public void FastPlay_IsSavedWithTheJourney_AndOlderSavesPlayAtNormalSpeed()
        {
            var dir = System.IO.Path.Combine(UnityEngine.Application.temporaryCachePath, "TabletopFastPlaySaves");
            if (System.IO.Directory.Exists(dir)) System.IO.Directory.Delete(dir, true);
            SaveGame.DirectoryOverride = dir;
            try
            {
                GameFlow.TitleShown = true;
                GameFlow.FastPlay = true;
                Assert.IsTrue(SaveGame.Save(Vector3.zero, 0));
                GameFlow.Reset();
                Assert.IsFalse(GameFlow.FastPlay, "a new journey starts at normal speed");
                Assert.IsTrue(SaveGame.TryLoad(out var data));
                Assert.AreEqual(6, data.version);
                Assert.IsTrue(data.fastPlay);
                SaveGame.Apply(data);
                Assert.IsTrue(GameFlow.FastPlay, "resuming keeps fast play on");

                System.IO.File.WriteAllText(SaveGame.FilePath, "{\"version\":5,\"defeated\":[],\"coins\":30,\"x\":0,\"z\":0}");
                Assert.IsTrue(SaveGame.TryLoad(out var old));
                SaveGame.Apply(old);
                Assert.IsFalse(GameFlow.FastPlay);
            }
            finally
            {
                SaveGame.DirectoryOverride = null;
                if (System.IO.Directory.Exists(dir)) System.IO.Directory.Delete(dir, true);
            }
        }
    }
}
