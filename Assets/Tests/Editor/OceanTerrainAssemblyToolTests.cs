#if UNITY_EDITOR
using System.IO;
using Bitbox.Toymageddon.Nautical;
using Bitbox.Toymageddon.Nautical.Editor;
using NUnit.Framework;
using NUnitAssert = NUnit.Framework.Assert;
using StormBreakers;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BitBox.Toymageddon.Tests.Editor
{
    public sealed class OceanTerrainAssemblyToolTests
    {
        private const string StormOceanPrefabPath = "Assets/Prefabs/StormOcean.prefab";
        private const string TestAssetFolder = "Assets/Temp/OceanTerrainAssemblyTests";
        private const string OutputName = "OceanTerrainAssemblyTest";

        private Scene _scene;
        private GameObject _camera;
        private Material _testLandMaterial;

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(TestAssetFolder);
            _scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _camera = new GameObject("Main Camera");
            _camera.tag = "MainCamera";
            _camera.AddComponent<Camera>();
            _testLandMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        }

        [TearDown]
        public void TearDown()
        {
            if (_testLandMaterial != null)
            {
                Object.DestroyImmediate(_testLandMaterial);
            }

            AssetDatabase.DeleteAsset(TestAssetFolder);
        }

        [Test]
        public void CreateAssembly_CreatesExpectedPrefabReadyHierarchy()
        {
            GameObject sourceRoot = CreateMockTerraForgeRoot();
            OceanTerrainAssemblyResult result = CreateAssembly(sourceRoot);

            NUnitAssert.IsNotNull(result.Root);
            NUnitAssert.IsNotNull(result.Root.transform.Find(OceanTerrainAssemblyUtility.TerrainGeometryChildName));
            NUnitAssert.IsNotNull(result.Root.transform.Find(OceanTerrainAssemblyUtility.ShallowOceanChildName));
            NUnitAssert.IsNotNull(result.Root.transform.Find(OceanTerrainAssemblyUtility.DeepOceanChildName));
            NUnitAssert.IsNotNull(result.Root.transform.Find(OceanTerrainAssemblyUtility.OceanGradientChildName));
            NUnitAssert.IsNotNull(result.Root.transform.Find(OceanTerrainAssemblyUtility.OceanSystemChildName));
            NUnitAssert.IsNotNull(result.Root.GetComponent<StormOceanLayerMaterialSync>());
        }

        [Test]
        public void CreateAssembly_AddsMeshColliderToTerrainGeometryOnly()
        {
            GameObject sourceRoot = CreateMockTerraForgeRoot();
            OceanTerrainAssemblyResult result = CreateAssembly(sourceRoot);

            GameObject terrain = result.Root.transform.Find(OceanTerrainAssemblyUtility.TerrainGeometryChildName).gameObject;
            GameObject shallow = result.Root.transform.Find(OceanTerrainAssemblyUtility.ShallowOceanChildName).gameObject;
            GameObject deep = result.Root.transform.Find(OceanTerrainAssemblyUtility.DeepOceanChildName).gameObject;

            MeshFilter terrainMeshFilter = terrain.GetComponent<MeshFilter>();
            MeshCollider terrainCollider = terrain.GetComponent<MeshCollider>();

            NUnitAssert.IsNotNull(terrainMeshFilter);
            NUnitAssert.IsNotNull(terrainCollider);
            NUnitAssert.AreSame(terrainMeshFilter.sharedMesh, terrainCollider.sharedMesh);
            NUnitAssert.IsNull(shallow.GetComponent<MeshCollider>());
            NUnitAssert.IsNull(deep.GetComponent<MeshCollider>());
        }

        [Test]
        public void CreateAssembly_SavesCopiedMeshesAndTintedWaterMaterials()
        {
            GameObject sourceRoot = CreateMockTerraForgeRoot();
            Color shallowColor = new(0.1f, 0.55f, 0.7f, 1f);
            Color deepColor = new(0.01f, 0.04f, 0.14f, 1f);
            OceanTerrainAssemblyResult result = CreateAssembly(sourceRoot, shallowColor, deepColor);

            NUnitAssert.IsTrue(AssetDatabase.Contains(result.TerrainMesh));
            NUnitAssert.IsTrue(AssetDatabase.Contains(result.ShallowOceanMesh));
            NUnitAssert.IsTrue(AssetDatabase.Contains(result.DeepOceanMesh));
            NUnitAssert.IsTrue(AssetDatabase.Contains(result.ShallowOceanMaterial));
            NUnitAssert.IsTrue(AssetDatabase.Contains(result.DeepOceanMaterial));
            NUnitAssert.IsNotEmpty(result.BlendOceanMeshes);
            NUnitAssert.IsNotEmpty(result.BlendOceanMaterials);

            NUnitAssert.AreEqual(shallowColor, result.ShallowOceanMaterial.GetColor("_waterColor"));
            NUnitAssert.AreEqual(deepColor, result.DeepOceanMaterial.GetColor("_waterColor"));
            NUnitAssert.IsTrue(AssetDatabase.Contains(result.BlendOceanMeshes[0]));
            NUnitAssert.IsTrue(AssetDatabase.Contains(result.BlendOceanMaterials[0]));
        }

        [Test]
        public void CreateAssembly_RerunWithReplace_DoesNotDuplicateOutputRoots()
        {
            GameObject sourceRoot = CreateMockTerraForgeRoot();

            CreateAssembly(sourceRoot);
            CreateAssembly(sourceRoot);

            int outputRootCount = 0;
            GameObject[] roots = _scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == OutputName)
                {
                    outputRootCount++;
                }
            }

            NUnitAssert.AreEqual(1, outputRootCount);
        }

        [Test]
        public void CreateAssembly_DisablesStormOceanTemplateRenderersButKeepsController()
        {
            GameObject sourceRoot = CreateMockTerraForgeRoot();
            OceanTerrainAssemblyResult result = CreateAssembly(sourceRoot);

            GameObject oceanSystem = result.Root.transform.Find(OceanTerrainAssemblyUtility.OceanSystemChildName).gameObject;
            NUnitAssert.IsNotNull(oceanSystem.GetComponent<OceanController>());
            NUnitAssert.IsNotNull(oceanSystem.GetComponent<StormOceanWaterSampler>());

            MeshRenderer[] templateRenderers = oceanSystem.GetComponentsInChildren<MeshRenderer>(includeInactive: true);
            NUnitAssert.IsNotEmpty(templateRenderers);
            for (int i = 0; i < templateRenderers.Length; i++)
            {
                NUnitAssert.IsFalse(templateRenderers[i].enabled, "StormOcean template renderers should not draw over the shaped water meshes.");
            }
        }

        [Test]
        public void TryValidateRequest_ReportsMissingLayer()
        {
            var incompleteRoot = new GameObject("Incomplete TerraForge Root");
            CreateLayer(incompleteRoot.transform, OceanTerrainAssemblyUtility.RealTerrainChildName);

            var request = new OceanTerrainAssemblyRequest(
                incompleteRoot,
                LoadStormOceanPrefab(),
                OutputName,
                TestAssetFolder,
                replaceExistingOutput: true,
                waterSurfaceHeight: 0f,
                blendBandCount: OceanTerrainAssemblyUtility.DefaultBlendBandCount,
                shallowWaterColor: OceanTerrainAssemblyUtility.DefaultShallowWaterColor,
                deepWaterColor: OceanTerrainAssemblyUtility.DefaultDeepWaterColor);

            NUnitAssert.IsFalse(OceanTerrainAssemblyUtility.TryValidateRequest(request, out string error));
            StringAssert.Contains(OceanTerrainAssemblyUtility.ShallowWaterChildName, error);
        }

        private OceanTerrainAssemblyResult CreateAssembly(GameObject sourceRoot)
        {
            return CreateAssembly(
                sourceRoot,
                OceanTerrainAssemblyUtility.DefaultShallowWaterColor,
                OceanTerrainAssemblyUtility.DefaultDeepWaterColor);
        }

        [Test]
        public void CreateAssembly_FlattensWaterMeshesToRequestedSurfaceHeight()
        {
            GameObject sourceRoot = CreateMockTerraForgeRoot();
            const float waterSurfaceHeight = 2.25f;
            OceanTerrainAssemblyResult result = CreateAssembly(
                sourceRoot,
                OceanTerrainAssemblyUtility.DefaultShallowWaterColor,
                OceanTerrainAssemblyUtility.DefaultDeepWaterColor,
                waterSurfaceHeight);

            AssertAllVerticesHaveHeight(result.ShallowOceanMesh, waterSurfaceHeight);
            AssertAllVerticesHaveHeight(result.DeepOceanMesh, waterSurfaceHeight);
        }

        [Test]
        public void CreateAssembly_CreatesGradientBandsBetweenShallowAndDeepOcean()
        {
            GameObject sourceRoot = CreateMockTerraForgeRoot();
            Color shallowColor = new(0.1f, 0.55f, 0.7f, 1f);
            Color deepColor = new(0.01f, 0.04f, 0.14f, 1f);
            OceanTerrainAssemblyResult result = CreateAssembly(sourceRoot, shallowColor, deepColor);

            Transform gradient = result.Root.transform.Find(OceanTerrainAssemblyUtility.OceanGradientChildName);
            NUnitAssert.IsNotNull(gradient);
            NUnitAssert.Greater(gradient.childCount, 0);
            NUnitAssert.AreEqual(gradient.childCount, result.BlendOceanMeshes.Length);
            NUnitAssert.AreEqual(gradient.childCount, result.BlendOceanMaterials.Length);

            Color firstBlendColor = result.BlendOceanMaterials[0].GetColor("_waterColor");
            NUnitAssert.AreNotEqual(shallowColor, firstBlendColor);
            NUnitAssert.AreNotEqual(deepColor, firstBlendColor);
        }

        private OceanTerrainAssemblyResult CreateAssembly(GameObject sourceRoot, Color shallowColor, Color deepColor)
        {
            return CreateAssembly(sourceRoot, shallowColor, deepColor, waterSurfaceHeight: 0f);
        }

        private OceanTerrainAssemblyResult CreateAssembly(
            GameObject sourceRoot,
            Color shallowColor,
            Color deepColor,
            float waterSurfaceHeight)
        {
            var request = new OceanTerrainAssemblyRequest(
                sourceRoot,
                LoadStormOceanPrefab(),
                OutputName,
                TestAssetFolder,
                replaceExistingOutput: true,
                waterSurfaceHeight: waterSurfaceHeight,
                blendBandCount: OceanTerrainAssemblyUtility.DefaultBlendBandCount,
                shallowWaterColor: shallowColor,
                deepWaterColor: deepColor);

            return OceanTerrainAssemblyUtility.CreateAssembly(request);
        }

        private static GameObject LoadStormOceanPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StormOceanPrefabPath);
            NUnitAssert.IsNotNull(prefab, $"Expected StormOcean prefab at {StormOceanPrefabPath}.");
            return prefab;
        }

        private GameObject CreateMockTerraForgeRoot()
        {
            var root = new GameObject("Generated Island Terrain");
            SceneManager.MoveGameObjectToScene(root, _scene);
            CreateLayer(root.transform, OceanTerrainAssemblyUtility.RealTerrainChildName);
            CreateLayer(root.transform, OceanTerrainAssemblyUtility.ShallowWaterChildName);
            CreateLayer(root.transform, OceanTerrainAssemblyUtility.DeepWaterChildName);
            return root;
        }

        private void CreateLayer(Transform parent, string layerName)
        {
            var layer = new GameObject(layerName);
            layer.transform.SetParent(parent, worldPositionStays: false);

            var meshFilter = layer.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = CreateLayerMesh(layerName);

            var meshRenderer = layer.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = _testLandMaterial;
        }

        private static Mesh CreateLayerMesh(string name)
        {
            float height = name switch
            {
                OceanTerrainAssemblyUtility.ShallowWaterChildName => -0.25f,
                OceanTerrainAssemblyUtility.DeepWaterChildName => -2f,
                _ => 0.5f
            };

            const int columns = 4;
            const int rows = 3;
            var vertices = new Vector3[columns * rows];
            var uvs = new Vector2[vertices.Length];
            for (int z = 0; z < rows; z++)
            {
                for (int x = 0; x < columns; x++)
                {
                    int index = z * columns + x;
                    vertices[index] = new Vector3(x - 1.5f, height, z - 1f);
                    uvs[index] = new Vector2(x / (float)(columns - 1), z / (float)(rows - 1));
                }
            }

            var triangles = new System.Collections.Generic.List<int>();
            for (int z = 0; z < rows - 1; z++)
            {
                for (int x = 0; x < columns - 1; x++)
                {
                    bool includeCell = name switch
                    {
                        OceanTerrainAssemblyUtility.ShallowWaterChildName => x <= 1,
                        OceanTerrainAssemblyUtility.DeepWaterChildName => x >= 2,
                        _ => true
                    };

                    if (!includeCell)
                    {
                        continue;
                    }

                    AddCellTriangles(columns, x, z, triangles);
                }
            }

            var mesh = new Mesh
            {
                name = name + " Mesh",
                vertices = vertices,
                triangles = triangles.ToArray(),
                uv = uvs
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddCellTriangles(int columns, int x, int z, System.Collections.Generic.List<int> triangles)
        {
            int bottomLeft = z * columns + x;
            int bottomRight = bottomLeft + 1;
            int topLeft = bottomLeft + columns;
            int topRight = topLeft + 1;
            triangles.Add(bottomLeft);
            triangles.Add(topLeft);
            triangles.Add(bottomRight);
            triangles.Add(bottomRight);
            triangles.Add(topLeft);
            triangles.Add(topRight);
        }

        private static void AssertAllVerticesHaveHeight(Mesh mesh, float expectedHeight)
        {
            Vector3[] vertices = mesh.vertices;
            NUnitAssert.IsNotEmpty(vertices);
            for (int i = 0; i < vertices.Length; i++)
            {
                NUnitAssert.AreEqual(expectedHeight, vertices[i].y, 0.0001f);
            }
        }
    }
}
#endif
