using NUnit.Framework;
using Tabletop.EditorTools;

namespace Tabletop.Tests.EditMode
{
    /// <summary>The generated Xcode project must build on newer Xcode (D-032).</summary>
    public class IosXcodeFixupsTests
    {
        private const string Pbx =
            "\t\tA1 /* Debug */ = {\n\t\t\tisa = XCBuildConfiguration;\n\t\t\tbuildSettings = {\n\t\t\t\tENABLE_USER_SCRIPT_SANDBOXING = YES;\n\t\t\t\tPRODUCT_NAME = x;\n\t\t\t};\n\t\t};\n"
            + "\t\tA2 /* Release */ = {\n\t\t\tisa = XCBuildConfiguration;\n\t\t\tbuildSettings = {\n\t\t\t\tPRODUCT_NAME = y;\n\t\t\t};\n\t\t};\n";

        [Test]
        public void Apply_TurnsBothSettingsOffInEveryConfiguration_WithoutDuplicates()
        {
            string result = IosXcodeFixups.Apply(Pbx);
            foreach (var key in IosXcodeFixups.Settings)
            {
                Assert.AreEqual(2, Count(result, key + " = NO;"), key);
                Assert.AreEqual(2, Count(result, key), key);
            }
            StringAssert.Contains("PRODUCT_NAME = x;", result);
            StringAssert.Contains("PRODUCT_NAME = y;", result);
            Assert.AreEqual(result, IosXcodeFixups.Apply(result), "applying twice changes nothing");
        }

        private static int Count(string s, string part)
        {
            int n = 0;
            for (int i = s.IndexOf(part); i >= 0; i = s.IndexOf(part, i + part.Length)) n++;
            return n;
        }
    }
}
