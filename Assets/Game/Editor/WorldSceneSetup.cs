using System.IO;
using Tabletop.Presentation;
using Tabletop.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Tabletop.EditorTools
{
    /// <summary>Creates World.unity (the explorable village slice) and makes it the first scene in builds.</summary>
    public static class WorldSceneSetup
    {
        public const string WorldScenePath = "Assets/Game/Scenes/World.unity";

        [MenuItem("Tabletop/Setup/8. Build World Scene")]
        public static void BuildWorldScene()
        {
            BoardMaterialSetup.CreateMaterial();
            IconImport.CreateIconSet();
            ModelImportTools.UpdateArtSet();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Load assets after NewScene (opening a scene unloads unreferenced assets).
            var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ProjectSetup.InputPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(BoardMaterialSetup.MaterialPath);
            var icons = AssetDatabase.LoadAssetAtPath<IconSet>(IconImport.IconSetPath);
            var art = AssetDatabase.LoadAssetAtPath<WorldArtSet>(ModelImportTools.ArtSetPath);
            if (input == null || material == null || icons == null) { Debug.LogError("[Tabletop] Missing input, material, or icon asset"); return; }

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.52f, 0.54f, 0.6f);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<UniversalAdditionalCameraData>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.75f, 0.9f);
            cam.fieldOfView = 40f;
            cam.farClipPlane = 200f;
            cam.transform.position = new Vector3(0, 12.5f, -18f);
            cam.transform.rotation = Quaternion.Euler(50, 0, 0);

            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.color = new Color(1f, 0.95f, 0.85f);
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(52, -35, 0);

            var esGo = new GameObject("EventSystem");
            var es = esGo.AddComponent<EventSystem>();
            esGo.AddComponent<InputSystemUIInputModule>();

            var appGo = new GameObject("WorldApp");
            var app = appGo.AddComponent<WorldApp>();
            var so = new SerializedObject(app);
            so.FindProperty("inputActions").objectReferenceValue = input;
            so.FindProperty("worldMaterial").objectReferenceValue = material;
            so.FindProperty("icons").objectReferenceValue = icons;
            so.FindProperty("worldCamera").objectReferenceValue = cam;
            so.FindProperty("eventSystem").objectReferenceValue = es;
            so.FindProperty("art").objectReferenceValue = art;
            so.FindProperty("look").objectReferenceValue = AssetDatabase.LoadAssetAtPath<WorldLook>(WorldLookSetup.LookPath);
            so.FindProperty("sounds").objectReferenceValue = AssetDatabase.LoadAssetAtPath<SoundBank>(WorldLookSetup.SoundsPath);
            so.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(WorldScenePath));
            EditorSceneManager.SaveScene(scene, WorldScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(WorldScenePath, true),
                new EditorBuildSettingsScene(ProjectSetup.ScenePath, true),
            };
            AssetDatabase.SaveAssets();
            Debug.Log("[Tabletop] World scene saved; build order is World, MatchPrototype.");
        }
    }
}
