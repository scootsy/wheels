using System.IO;
using Tabletop.Infrastructure;
using Tabletop.Presentation;
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
    /// <summary>Reproducible editor setup steps, run from the Tabletop menu (or Unity MCP).</summary>
    public static class ProjectSetup
    {
        public const string ContentPath = "Assets/Game/Content/ContentCatalog.asset";
        public const string InputPath = "Assets/Game/Input/GameInput.inputactions";
        public const string ScenePath = "Assets/Game/Scenes/MatchPrototype.unity";
        public const string UrpPath = "Assets/Game/Settings/URP_Pipeline.asset";
        public const string UrpRendererPath = "Assets/Game/Settings/URP_Renderer.asset";

        [MenuItem("Tabletop/Setup/1. Create or Refresh Content Catalog")]
        public static ContentCatalogAsset CreateContent()
        {
            var asset = AssetDatabase.LoadAssetAtPath<ContentCatalogAsset>(ContentPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<ContentCatalogAsset>();
                asset.PopulateFromReference();
                AssetDatabase.CreateAsset(asset, ContentPath);
            }
            else
            {
                asset.PopulateFromReference();
                EditorUtility.SetDirty(asset);
            }
            AssetDatabase.SaveAssets();
            var catalog = asset.ToCatalog(out var errors);
            Debug.Log("[Tabletop] Content catalog " + (catalog != null ? "valid, hash " + catalog.ContentHash : "INVALID: " + string.Join("; ", errors)));
            return asset;
        }

        [MenuItem("Tabletop/Setup/2. Configure URP")]
        public static void ConfigureUrp()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpPath);
            if (pipeline == null)
            {
                var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, UrpRendererPath);
                pipeline = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(pipeline, UrpPath);
            }
            GraphicsSettings.defaultRenderPipeline = pipeline;
            int levels = QualitySettings.names.Length;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < levels; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(current, false);
            AssetDatabase.SaveAssets();
            Debug.Log("[Tabletop] URP assigned to Graphics and " + levels + " quality levels.");
        }

        [MenuItem("Tabletop/Setup/3. Build MatchPrototype Scene")]
        public static void BuildScene()
        {
            CreateContent();
            IconImport.CreateIconSet();
            BoardMaterialSetup.CreateMaterial();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Load assets after NewScene: opening a scene unloads previously loaded, unreferenced assets.
            var content = AssetDatabase.LoadAssetAtPath<ContentCatalogAsset>(ContentPath);
            var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
            var icons = AssetDatabase.LoadAssetAtPath<IconSet>(IconImport.IconSetPath);
            if (input == null || content == null || icons == null) { Debug.LogError("[Tabletop] Missing input, content, or icon asset"); return; }

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<UniversalAdditionalCameraData>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.12f, 0.10f);
            cam.depth = 0;

            var bgGo = new GameObject("Background Camera");
            var bg = bgGo.AddComponent<Camera>();
            bgGo.AddComponent<UniversalAdditionalCameraData>();
            bg.clearFlags = CameraClearFlags.SolidColor;
            bg.backgroundColor = new Color(0.10f, 0.09f, 0.10f);
            bg.cullingMask = 0;
            bg.depth = -10;

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightGo.transform.rotation = Quaternion.Euler(50, -30, 0);

            var esGo = new GameObject("EventSystem");
            var es = esGo.AddComponent<EventSystem>();
            esGo.AddComponent<InputSystemUIInputModule>();

            var appGo = new GameObject("MatchApp");
            var app = appGo.AddComponent<MatchApp>();
            var so = new SerializedObject(app);
            so.FindProperty("inputActions").objectReferenceValue = input;
            so.FindProperty("content").objectReferenceValue = content;
            so.FindProperty("boardCamera").objectReferenceValue = cam;
            so.FindProperty("eventSystem").objectReferenceValue = es;
            so.FindProperty("icons").objectReferenceValue = icons;
            so.FindProperty("boardMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(BoardMaterialSetup.MaterialPath);
            so.FindProperty("sounds").objectReferenceValue = AssetDatabase.LoadAssetAtPath<SoundBank>(WorldLookSetup.SoundsPath);
            so.FindProperty("figurines").objectReferenceValue = AssetDatabase.LoadAssetAtPath<FigurineSet>(WorldLookSetup.FigurinesPath);
            so.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(WorldSceneSetup.WorldScenePath, true), new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[Tabletop] Scene saved to " + ScenePath + " and set as the only build scene.");
        }
    }
}
