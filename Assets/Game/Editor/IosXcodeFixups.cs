using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Callbacks;

namespace Tabletop.EditorTools
{
    /// <summary>
    /// Settings newer Xcode versions switch on that break Unity's generated project (D-032):
    /// - User script sandboxing stops Unity's IL2CPP build script from creating folders and marking its tools
    ///   executable ("Sandbox: mkdir/chmod deny").
    /// - Module verification rejects UnityFramework's umbrella header ("umbrella header does not include",
    ///   "expected a type", "could not build module").
    /// Both are turned off in every build configuration of the generated project.
    /// Deliberately not behind #if UNITY_IOS and not using the iOS-only PBXProject API: the iOS build runs while
    /// the editor is still compiled for Windows, where such code would be missing and the fix silently skipped.
    /// </summary>
    public static class IosXcodeFixups
    {
        public static readonly string[] Settings = { "ENABLE_USER_SCRIPT_SANDBOXING", "ENABLE_MODULE_VERIFIER" };

        [PostProcessBuild(1000)]
        public static void OnPostprocessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            string projPath = Path.Combine(path, "Unity-iPhone.xcodeproj", "project.pbxproj");
            File.WriteAllText(projPath, Apply(File.ReadAllText(projPath)));
        }

        /// <summary>Removes any existing value of each setting, then sets it to NO in every buildSettings block.</summary>
        public static string Apply(string pbxproj)
        {
            string insert = "";
            foreach (var key in Settings)
            {
                pbxproj = Regex.Replace(pbxproj, @"^[ \t]*" + key + @"[ \t]*=[^;]*;[ \t]*\r?\n", "", RegexOptions.Multiline);
                insert += "\n\t\t\t\t" + key + " = NO;";
            }
            return Regex.Replace(pbxproj, @"buildSettings = \{", m => m.Value + insert);
        }
    }
}
