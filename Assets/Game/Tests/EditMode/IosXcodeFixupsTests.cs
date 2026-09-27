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

        [Test]
        public void Apply_AddsCompilerFlagToIl2CppScriptOnce()
        {
            // As Unity writes it in project.pbxproj: quotes as \" and line breaks as \n inside one string.
            const string script = "\t\t\tshellScript = \"ARGS=(\\n    --outputpath=\\\"x\\\"\\n    --configuration=\\\"$IL2CPP_CONFIG\\\"\\n    )\\n\";\n";
            string result = IosXcodeFixups.Apply(Pbx + script);
            StringAssert.Contains("--configuration=\\\"$IL2CPP_CONFIG\\\"\\n    --compiler-flags=\\\"-Wno-#warnings\\\"\\n    )", result);
            Assert.AreEqual(1, Count(result, "--compiler-flags"));
            Assert.AreEqual(result, IosXcodeFixups.Apply(result), "applying twice changes nothing");
        }

        [Test]
        public void Apply_BuildsUnityFrameworkWithoutAModule_AndRunsScriptEveryBuild()
        {
            const string extra = "\t\tB1 /* Release */ = {\n\t\t\tisa = XCBuildConfiguration;\n\t\t\tbuildSettings = {\n\t\t\t\tDEFINES_MODULE = YES;\n\t\t\t};\n\t\t};\n"
                + "\t\tC1 /* ShellScript */ = {\n\t\t\tisa = PBXShellScriptBuildPhase;\n\t\t\tbuildActionMask = 2147483647;\n\t\t};\n";
            string result = IosXcodeFixups.Apply(Pbx + extra);
            StringAssert.DoesNotContain("DEFINES_MODULE = YES;", result);
            StringAssert.Contains("DEFINES_MODULE = NO;", result);
            StringAssert.Contains("isa = PBXShellScriptBuildPhase;\n\t\t\talwaysOutOfDate = 1;", result);
            Assert.AreEqual(1, Count(result, "alwaysOutOfDate"));
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
