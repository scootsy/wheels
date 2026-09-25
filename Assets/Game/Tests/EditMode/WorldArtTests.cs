using System.Linq;
using NUnit.Framework;
using Tabletop.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tabletop.Tests.EditMode
{
    /// <summary>Imported-model placement and the World Art Set asset (D-026).</summary>
    public class WorldArtTests
    {
        private const string ArtSetPath = "Assets/Game/Art/WorldArt.asset";

        [Test]
        public void PlaceModel_ScalesToHeight_StandsOnTheGround_AndCentres()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                // A "model" that is off-centre, sunk below its pivot, and the wrong size: 2 x 4 x 1 at (3, -1, 5).
                var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
                SceneManager.MoveGameObjectToScene(model, scene);
                model.transform.position = new Vector3(3, -1, 5);
                model.transform.localScale = new Vector3(2, 4, 1);
                var parent = new GameObject("Parent");
                SceneManager.MoveGameObjectToScene(parent, scene);

                var kit = new WorldKit(null);
                var slot = new WorldArtSet.Slot { who = "Test", model = model, turn = 0 };
                var placed = kit.PlaceModel(slot, parent.transform, 1.75f, Vector2.zero, out var size);
                var b = WorldKit.LocalBounds(placed, parent.transform);
                Assert.AreEqual(1.75f, b.size.y, 1e-3f, "height");
                Assert.AreEqual(0f, b.min.y, 1e-3f, "feet on the ground");
                Assert.AreEqual(0f, b.center.x, 1e-3f, "centred x");
                Assert.AreEqual(0f, b.center.z, 1e-3f, "centred z");
                Assert.AreEqual(1.75f, size.y, 1e-3f, "reported size");

                // Buildings fit the placeholder footprint (the tighter axis wins) instead of a height.
                var house = kit.PlaceModel(slot, parent.transform, 0, new Vector2(8, 8), out var houseSize);
                Assert.AreEqual(8f, houseSize.x, 1e-3f, "width fills the footprint");
                Assert.LessOrEqual(houseSize.z, 8.001f);
                Assert.AreEqual(0f, WorldKit.LocalBounds(house, parent.transform).min.y, 1e-3f);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test]
        public void LocalBounds_OfARiggedModel_FollowsEveryParentScale()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                // A 1 m cube skinned to a single bone, under a parent scaled x3 (like a fitted model container),
                // measured from an unscaled root.
                var root = new GameObject("Root");
                SceneManager.MoveGameObjectToScene(root, scene);
                var parent = new GameObject("Parent");
                parent.transform.SetParent(root.transform, false);
                parent.transform.localScale = Vector3.one * 3f;
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var mesh = Object.Instantiate(cube.GetComponent<MeshFilter>().sharedMesh);
                Object.DestroyImmediate(cube);
                var go = new GameObject("Rigged");
                go.transform.SetParent(parent.transform, false);
                var bone = new GameObject("Bone").transform;
                bone.SetParent(go.transform, false);
                mesh.boneWeights = Enumerable.Repeat(new BoneWeight { boneIndex0 = 0, weight0 = 1 }, mesh.vertexCount).ToArray();
                mesh.bindposes = new[] { bone.worldToLocalMatrix * go.transform.localToWorldMatrix };
                var smr = go.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = mesh;
                smr.bones = new[] { bone };
                var b = WorldKit.LocalBounds(go.transform, root.transform);
                Assert.AreEqual(3f, b.size.y, 1e-3f, "a 1 m cube under a x3 parent is 3 m tall");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test]
        public void ArtSet_OnlyNamesPeopleAndBuildingsThatExist_AndUsesUrpMaterials()
        {
            var art = AssetDatabase.LoadAssetAtPath<WorldArtSet>(ArtSetPath);
            Assert.IsNotNull(art, "World Art Set asset exists");
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var holder = new GameObject("Probe");
                SceneManager.MoveGameObjectToScene(holder, scene);
                var layout = new WorldBuilder(new WorldKit(null), null).Build(holder.transform);
                foreach (var s in art.people.Where(s => s.model != null))
                    CollectionAssert.Contains(layout.PersonKeys, s.who, "unknown person name in the art set");
                foreach (var s in art.buildings.Where(s => s.model != null))
                    CollectionAssert.Contains(layout.BuildingKeys, s.who, "unknown building name in the art set");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
            // Anything not on a URP shader renders magenta in the Windows build (D-024).
            foreach (var s in art.people.Concat(art.buildings).Where(s => s.model != null))
                foreach (var r in s.model.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials)
                    {
                        Assert.IsNotNull(m, s.who + ": missing material");
                        StringAssert.StartsWith("Universal Render Pipeline/", m.shader.name, s.who + " uses " + m.shader.name);
                        Assert.IsNotNull(m.GetTexture("_BaseMap"), s.who + ": colour texture not connected (run Tabletop > Art > 1. Prepare Imported Models)");
                    }
        }
    }
}
