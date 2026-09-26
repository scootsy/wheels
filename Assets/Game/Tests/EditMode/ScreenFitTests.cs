using NUnit.Framework;
using Tabletop.Presentation;
using UnityEngine;

namespace Tabletop.Tests.EditMode
{
    /// <summary>Screen shapes other than 16:9 (iPad 4:3, iPhone 19.5:9) keep the whole table and interface on screen (D-029).</summary>
    public class ScreenFitTests
    {
        private static float HorizontalFov(float verticalDeg, float aspect) =>
            2f * Mathf.Atan(Mathf.Tan(verticalDeg * 0.5f * Mathf.Deg2Rad) * aspect) * Mathf.Rad2Deg;

        [Test]
        public void TableCamera_KeepsTheWidescreenFieldOfView_AtAndAbove16By9()
        {
            Assert.AreEqual(MechanicalTable.BaseFieldOfView, MechanicalTable.FieldOfViewFor(16f / 9f), 1e-4f);
            Assert.AreEqual(MechanicalTable.BaseFieldOfView, MechanicalTable.FieldOfViewFor(19.5f / 9f), 1e-4f, "iPhone");
        }

        [Test]
        public void TableCamera_OpensUpOnNarrowScreens_SoTheTableIsNeverCutOff()
        {
            float wide = HorizontalFov(MechanicalTable.BaseFieldOfView, 16f / 9f);
            foreach (var aspect in new[] { 4f / 3f, 1.43f, 16f / 10f })
            {
                float v = MechanicalTable.FieldOfViewFor(aspect);
                Assert.Greater(v, MechanicalTable.BaseFieldOfView, "aspect " + aspect);
                Assert.AreEqual(wide, HorizontalFov(v, aspect), 0.01f, "same horizontal view at aspect " + aspect);
            }
        }

        [Test]
        public void InterfaceFrame_MatchesHeightOnWideScreens_AndWidthOnNarrowOnes()
        {
            Assert.AreEqual(1f, FrameFitter.MatchFor(16f / 9f));
            Assert.AreEqual(1f, FrameFitter.MatchFor(19.5f / 9f), "iPhone: side margins");
            Assert.AreEqual(0f, FrameFitter.MatchFor(4f / 3f), "iPad: margins above and below");
            Assert.AreEqual(0f, FrameFitter.MatchFor(16f / 10f));
        }
    }
}
