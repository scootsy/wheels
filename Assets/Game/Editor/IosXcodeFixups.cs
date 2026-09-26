#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace Tabletop.EditorTools
{
    /// <summary>
    /// Settings newer Xcode versions switch on that break Unity's generated project (D-032):
    /// - User script sandboxing stops Unity's IL2CPP build script from creating folders and marking its tools
    ///   executable ("Sandbox: mkdir/chmod deny").
    /// - Module verification rejects UnityFramework's umbrella header ("umbrella header does not include",
    ///   "expected a type", "could not build module").
    /// Both are turned off on every target of the generated project.
    /// </summary>
    public static class IosXcodeFixups
    {
        [PostProcessBuild(1000)]
        public static void OnPostprocessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            string projPath = PBXProject.GetPBXProjectPath(path);
            var proj = new PBXProject();
            proj.ReadFromFile(projPath);
            foreach (var guid in new[]
                     {
                         proj.ProjectGuid(),
                         proj.GetUnityMainTargetGuid(),
                         proj.GetUnityFrameworkTargetGuid(),
                         proj.TargetGuidByName("GameAssembly"),
                     })
            {
                if (string.IsNullOrEmpty(guid)) continue;
                proj.SetBuildProperty(guid, "ENABLE_USER_SCRIPT_SANDBOXING", "NO");
                proj.SetBuildProperty(guid, "ENABLE_MODULE_VERIFIER", "NO");
            }
            File.WriteAllText(projPath, proj.WriteToString());
        }
    }
}
#endif
