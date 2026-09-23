using Tabletop.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Tabletop.EditorTools
{
    /// <summary>
    /// Creates the URP material used by every placeholder 3D piece. Pieces are built at runtime, so without
    /// a referenced material asset a player build falls back to the built-in Standard material (magenta in URP).
    /// </summary>
    public static class BoardMaterialSetup
    {
        public const string MaterialPath = "Assets/Game/Art/Materials/BoardPlaceholder.mat";
        public const string ShaderName = "Universal Render Pipeline/Lit";

        [MenuItem("Tabletop/Setup/6. Create or Refresh Board Material")]
        public static Material CreateMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            var shader = Shader.Find(ShaderName);
            if (shader == null) { Debug.LogError("[Tabletop] Shader not found: " + ShaderName); return null; }
            if (mat == null)
            {
                System.IO.Directory.CreateDirectory("Assets/Game/Art/Materials");
                mat = new Material(shader) { name = "BoardPlaceholder" };
                AssetDatabase.CreateAsset(mat, MaterialPath);
            }
            mat.shader = shader;
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.2f);
            mat.SetFloat("_Metallic", 0f);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        [MenuItem("Tabletop/Setup/7. Assign Board Material To Scene")]
        public static void AssignToOpenScene()
        {
            var mat = CreateMaterial();
            var app = Object.FindAnyObjectByType<MatchApp>();
            if (app == null || mat == null) { Debug.LogError("[Tabletop] Open MatchPrototype.unity first."); return; }
            var so = new SerializedObject(app);
            so.FindProperty("boardMaterial").objectReferenceValue = mat;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(app.gameObject.scene);
            EditorSceneManager.SaveScene(app.gameObject.scene);
            Debug.Log("[Tabletop] Board material assigned to MatchApp and scene saved.");
        }
    }
}
