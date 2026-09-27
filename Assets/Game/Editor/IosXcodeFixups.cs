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
    /// - IL2CPP compiles the game code for iOS 11 whatever the app's target, so Xcode 27's libc++ prints
    ///   "The selected platform is no longer supported by libc++" for every file (1,600+ warnings that bury real
    ///   problems). The IL2CPP script phase gets -Wno-#warnings, which silences only #warning directives.
    /// - Xcode's "Update to recommended settings" turns module verification back on (D-035). UnityFramework is
    ///   therefore built without a module (DEFINES_MODULE = NO), so there is nothing to verify: the app imports
    ///   its header directly and no Swift code needs the module.
    /// - The IL2CPP script phase declares no outputs, so Xcode warns it "will be run during every build". Running
    ///   every build is intended (IL2CPP tracks its own changes), so the phase is marked alwaysOutOfDate, which is
    ///   what unchecking "Based on dependency analysis" does.
    /// - Xcode 27 ran bitcode_strip over UnityFramework after UnityFramework's own target had already signed it, then
    ///   failed to re-sign the rewritten binary ("internal error in Code Signing subsystem", D-036). Bitcode no longer
    ///   exists (ENABLE_BITCODE = NO), so STRIP_BITCODE_FROM_COPIED_FILES is turned off: the embedded copy stays
    ///   byte-identical to the file codesign just produced.
    /// Deliberately not behind #if UNITY_IOS and not using the iOS-only PBXProject API: the iOS build runs while
    /// the editor is still compiled for Windows, where such code would be missing and the fix silently skipped.
    /// </summary>
    public static class IosXcodeFixups
    {
        public static readonly string[] Settings = { "ENABLE_USER_SCRIPT_SANDBOXING", "ENABLE_MODULE_VERIFIER", "STRIP_BITCODE_FROM_COPIED_FILES" };

        /// <summary>The line in Unity's IL2CPP script phase (pbxproj-escaped) that the compiler flag follows.</summary>
        public const string Il2CppArgsAnchor = "--configuration=\\\"$IL2CPP_CONFIG\\\"";
        public const string Il2CppCompilerFlags = "\\n    --compiler-flags=\\\"-Wno-#warnings\\\"";

        [PostProcessBuild(1000)]
        public static void OnPostprocessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            string projPath = Path.Combine(path, "Unity-iPhone.xcodeproj", "project.pbxproj");
            File.WriteAllText(projPath, Apply(File.ReadAllText(projPath)));
        }

        /// <summary>Sets each setting to NO in every buildSettings block and adds the IL2CPP compiler flag, replacing any earlier copy.</summary>
        public static string Apply(string pbxproj)
        {
            string insert = "";
            foreach (var key in Settings)
            {
                pbxproj = Regex.Replace(pbxproj, @"^[ \t]*" + key + @"[ \t]*=[^;]*;[ \t]*\r?\n", "", RegexOptions.Multiline);
                insert += "\n\t\t\t\t" + key + " = NO;";
            }
            pbxproj = Regex.Replace(pbxproj, @"buildSettings = \{", m => m.Value + insert);
            pbxproj = pbxproj.Replace("DEFINES_MODULE = YES;", "DEFINES_MODULE = NO;");
            pbxproj = Regex.Replace(pbxproj, @"^[ \t]*alwaysOutOfDate[ \t]*=[^;]*;[ \t]*\r?\n", "", RegexOptions.Multiline);
            pbxproj = Regex.Replace(pbxproj, @"isa = PBXShellScriptBuildPhase;", m => m.Value + "\n\t\t\talwaysOutOfDate = 1;");
            pbxproj = pbxproj.Replace(Il2CppCompilerFlags, "");
            return pbxproj.Replace(Il2CppArgsAnchor, Il2CppArgsAnchor + Il2CppCompilerFlags);
        }
    }
}
