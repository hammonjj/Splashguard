#if UNITY_EDITOR
using Bitbox.Toymageddon.Nautical;
using Bitbox.Toymageddon.Nautical.Editor;
using NUnit.Framework;
using NUnitAssert = NUnit.Framework.Assert;
using StormBreakers;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BitBox.Toymageddon.Tests.Editor
{
    public sealed class ManualOceanAssemblyToolTests
    {
        private const string StormOceanPrefabPath = "Assets/Prefabs/StormOcean.prefab";
        private const string HubWorldIslandAssetPath = "Assets/Models/Terrain/HubWorldIsland.fbx";
        private const string TestAssetFolder = "Assets/Temp/ManualOceanAssemblyTests";
        private const string OutputName = "ManualOceanAssemblyTest";

        private Scene _scene;
        private Material _testLandMaterial;
        private Material _testBoundaryMaterial;

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(TestAssetFolder);
            _scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera");
            camera.tag = "MainCamera";
            camera.AddComponent<Camera>();
            _testLandMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            _testBoundaryMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        }

        [TearDown]
        public void TearDown()
        {
            if (_testLandMaterial != null)
            {
                Object.DestroyImmediate(_testLandMaterial);
            }

            if (_testBoundaryMaterial != null)
            {
                Object.DestroyImmediate(_testBoundaryMaterial);
            }

            AssetDatabase.DeleteAsset(TestAssetFolder);
        }

        [Test]
        public void CreateAssembly_WithDeepOnlySceneSource_GeneratesTerrainDeepOceanAndOceanSystem()
        {
            GameObject sourceRoot = CreateManualSource(includeDeep: true, includeShallow: false, includeBoundary: false);

            ManualOceanAssemblyResult result = ManualOceanAssemblyUtility.CreateAssembly(CreateRequest(sourceRoot));

            NUnitAssert.IsNotNull(result.Root.transform.Find(OceanTerrainAssemblyUtility.TerrainGeometryChildName));
            NUnitAssert.IsNotNull(result.Root.transform.Find(OceanTerrainAssemblyUtility.DeepOceanChildName));
            NUnitAssert.IsNotNull(result.Root.transform.Find(OceanTerrainAssemblyUtility.OceanSystemChildName));
            NUnitAssert.IsNull(result.Root.transform.Find(OceanTerrainAssemblyUtility.ShallowOceanChildName));
            NUnitAssert.IsNull(result.Root.transform.Find(OceanTerrainAssemblyUtility.OceanGradientChildName));
            NUnitAssert.IsNotNull(result.Root.GetComponent<StormOceanLayerMaterialSync>());
        }

        [Test]
        public void CreateAssembly_PreservesLandTransformsMaterialsAndColliders()
        {
            GameObject sourceRoot = CreateManualSource(includeDeep: true, includeShallow: false, includeBoundary: false);
            Transform sourceLand = sourceRoot.transform.Find("LandPivot/Hub");
            NUnitAssert.IsNotNull(sourceLand);

            ManualOceanAssemblyResult result = ManualOceanAssemblyUtility.CreateAssembly(CreateRequest(sourceRoot));
            Transform generatedLand = result.Root.transform.Find($"{OceanTerrainAssemblyUtility.TerrainGeometryChildName}/Hub");

            NUnitAssert.IsNotNull(generatedLand);
            NUnitAssert.AreEqual(sourceLand.position.x, generatedLand.position.x, 0.001f);
            NUnitAssert.AreEqual(sourceLand.position.y, generatedLand.position.y, 0.001f);
            NUnitAssert.AreEqual(sourceLand.position.z, generatedLand.position.z, 0.001f);
            NUnitAssert.Less(Quaternion.Angle(sourceLand.rotation, generatedLand.rotation), 0.1f);
            AssertVectorApproximatelyEqual(sourceLand.lossyScale, generatedLand.lossyScale, 0.001f);

            MeshRenderer generatedRenderer = generatedLand.GetComponent<MeshRenderer>();
            MeshCollider generatedCollider = generatedLand.GetComponent<MeshCollider>();
            NUnitAssert.IsNotNull(generatedRenderer);
            NUnitAssert.IsNotNull(generatedCollider);
            NUnitAssert.AreSame(_testLandMaterial, generatedRenderer.sharedMaterial);
        }

        [Test]
        public void CreateAssembly_PreservesBoundaryAsSeparateTerrainChild()
        {
            GameObject sourceRoot = CreateManualSource(includeDeep: true, includeShallow: false, includeBoundary: true);

            ManualOceanAssemblyResult result = ManualOceanAssemblyUtility.CreateAssembly(CreateRequest(sourceRoot));
            Transform terrainGeometry = result.Root.transform.Find(OceanTerrainAssemblyUtility.TerrainGeometryChildName);
            Transform boundary = terrainGeometry.Find(ManualOceanAssemblyUtility.DefaultBoundarySourceName);

            NUnitAssert.IsNotNull(boundary);
            NUnitAssert.IsNotNull(boundary.GetComponent<MeshCollider>());
            NUnitAssert.AreSame(_testBoundaryMaterial, boundary.GetComponent<MeshRenderer>().sharedMaterial);
        }

        [Test]
        public void CreateAssembly_WithAssetSource_GeneratesIntoActiveSceneAndCleansUpTemporaryInstance()
        {
            GameObject assetSource = AssetDatabase.LoadAssetAtPath<GameObject>(HubWorldIslandAssetPath);
            NUnitAssert.IsNotNull(assetSource);

            ManualOceanAssemblyResult result = ManualOceanAssemblyUtility.CreateAssembly(CreateRequest(assetSource));

            NUnitAssert.AreEqual(_scene, result.Root.scene);

            GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < allObjects.Length; i++)
            {
                StringAssert.DoesNotContain("__ManualOceanAssemblySource", allObjects[i].name);
            }
        }

        [Test]
        public void CreateAssembly_LandOnlySource_DoesNotCreateWaterHierarchyOrOceanSystem()
        {
            GameObject sourceRoot = CreateManualSource(includeDeep: false, includeShallow: false, includeBoundary: true);
            var request = new ManualOceanAssemblyRequest(
                sourceRoot,
                stormOceanPrefab: null,
                OutputName,
                TestAssetFolder,
                replaceExistingOutput: true,
                waterSurfaceHeight: 0f,
                blendBandCount: OceanTerrainAssemblyUtility.DefaultBlendBandCount,
                shallowWaterColor: OceanTerrainAssemblyUtility.DefaultShallowWaterColor,
                deepWaterColor: OceanTerrainAssemblyUtility.DefaultDeepWaterColor,
                ManualOceanAssemblyUtility.DefaultDeepWaterSourceName,
                ManualOceanAssemblyUtility.DefaultShallowWaterSourceName,
                ManualOceanAssemblyUtility.DefaultBoundarySourceName);

            ManualOceanAssemblyResult result = ManualOceanAssemblyUtility.CreateAssembly(request);

            NUnitAssert.IsNull(result.Root.transform.Find(OceanTerrainAssemblyUtility.ShallowOceanChildName));
            NUnitAssert.IsNull(result.Root.transform.Find(OceanTerrainAssemblyUtility.DeepOceanChildName));
            NUnitAssert.IsNull(result.Root.transform.Find(OceanTerrainAssemblyUtility.OceanGradientChildName));
            NUnitAssert.IsNull(result.Root.transform.Find(OceanTerrainAssemblyUtility.OceanSystemChildName));
            NUnitAssert.IsNull(result.Root.GetComponent<StormOceanLayerMaterialSync>());
        }

        [Test]
        public void CreateAssembly_WithShallowAndDeepCompatibleSource_CreatesGradient()
        {
            GameObject sourceRoot = CreateManualSource(includeDeep: true, includeShallow: true, includeBoundary: true);

            ManualOceanAssemblyResult result = ManualOceanAssemblyUtility.CreateAssembly(CreateRequest(sourceRoot));

            Transform gradient = result.Root.transform.Find(OceanTerrainAssemblyUtility.OceanGradientChildName);
            NUnitAssert.IsNotNull(gradient);
            NUnitAssert.Greater(gradient.childCount, 0);
            NUnitAssert.AreEqual(gradient.childCount, result.BlendOceanMeshes.Length);
            NUnitAssert.AreEqual(gradient.childCount, result.BlendOceanMaterials.Length);
        }

        [Test]
        public void CreateAssembly_WithIncompatibleShallowAndDeepTopology_WarnsAndSkipsGradient()
        {
            GameObject sourceRoot = CreateManualSource(includeDeep: true, includeShallow: true, includeBoundary: false, incompatibleDeep: true);
            LogAssert.Expect(
                LogType.Warning,
                "Ocean assembly could not build shallow/deep gradient bands. The shallow and deep meshes must share the same vertex topology with adjacent triangles along the boundary.");

            ManualOceanAssemblyResult result = ManualOceanAssemblyUtility.CreateAssembly(CreateRequest(sourceRoot));

            NUnitAssert.IsNull(result.Root.transform.Find(OceanTerrainAssemblyUtility.OceanGradientChildName));
            NUnitAssert.IsEmpty(result.BlendOceanMeshes);
            NUnitAssert.IsEmpty(result.BlendOceanMaterials);
        }

        [Test]
        public void TryValidateRequest_DuplicateRoleMatchesFail()
        {
            GameObject sourceRoot = CreateManualSource(includeDeep: true, includeShallow: false, includeBoundary: false);
            CreateMeshObject(sourceRoot.transform, ManualOceanAssemblyUtility.DefaultDeepWaterSourceName, CreateCompatibleWaterMesh("DuplicateDeep", isShallow: false), _testLandMaterial);

            bool isValid = ManualOceanAssemblyUtility.TryValidateRequest(CreateRequest(sourceRoot), out string error);

            NUnitAssert.IsFalse(isValid);
            StringAssert.Contains(ManualOceanAssemblyUtility.DefaultDeepWaterSourceName, error);
        }

        [Test]
        public void TryValidateRequest_NoLandGeometryFails()
        {
            var sourceRoot = new GameObject("WaterOnlySource");
            SceneManager.MoveGameObjectToScene(sourceRoot, _scene);
            CreateMeshObject(sourceRoot.transform, ManualOceanAssemblyUtility.DefaultDeepWaterSourceName, CreateCompatibleWaterMesh("DeepOcean", isShallow: false), _testLandMaterial);
            CreateMeshObject(sourceRoot.transform, ManualOceanAssemblyUtility.DefaultShallowWaterSourceName, CreateCompatibleWaterMesh("ShallowOcean", isShallow: true), _testLandMaterial);

            bool isValid = ManualOceanAssemblyUtility.TryValidateRequest(CreateRequest(sourceRoot), out string error);

            NUnitAssert.IsFalse(isValid);
            StringAssert.Contains("No land geometry", error);
        }

        [Test]
        public void CreateAssembly_RerunWithReplace_DoesNotDuplicateOutputRoots()
        {
            GameObject sourceRoot = CreateManualSource(includeDeep: true, includeShallow: true, includeBoundary: true);

            ManualOceanAssemblyUtility.CreateAssembly(CreateRequest(sourceRoot));
            ManualOceanAssemblyUtility.CreateAssembly(CreateRequest(sourceRoot));

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

        private ManualOceanAssemblyRequest CreateRequest(
            GameObject sourceRoot,
            string deepWaterName = ManualOceanAssemblyUtility.DefaultDeepWaterSourceName,
            string shallowWaterName = ManualOceanAssemblyUtility.DefaultShallowWaterSourceName,
            string boundaryName = ManualOceanAssemblyUtility.DefaultBoundarySourceName)
        {
            return new ManualOceanAssemblyRequest(
                sourceRoot,
                LoadStormOceanPrefab(),
                OutputName,
                TestAssetFolder,
                replaceExistingOutput: true,
                waterSurfaceHeight: 0f,
                blendBandCount: OceanTerrainAssemblyUtility.DefaultBlendBandCount,
                shallowWaterColor: OceanTerrainAssemblyUtility.DefaultShallowWaterColor,
                deepWaterColor: OceanTerrainAssemblyUtility.DefaultDeepWaterColor,
                deepWaterName,
                shallowWaterName,
                boundaryName);
        }

        private static GameObject LoadStormOceanPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StormOceanPrefabPath);
            NUnitAssert.IsNotNull(prefab, $"Expected StormOcean prefab at {StormOceanPrefabPath}.");
            return prefab;
        }

        private GameObject CreateManualSource(
            bool includeDeep,
            bool includeShallow,
            bool includeBoundary,
            bool incompatibleDeep = false)
        {
            var root = new GameObject("HubWorldIsland");
            SceneManager.MoveGameObjectToScene(root, _scene);
            root.transform.position = new Vector3(4f, 1.5f, -3f);
            root.transform.rotation = Quaternion.Euler(0f, 35f, 0f);
            root.transform.localScale = Vector3.one * 1.25f;

            var landPivot = new GameObject("LandPivot");
            landPivot.transform.SetParent(root.transform, worldPositionStays: false);
            landPivot.transform.localPosition = new Vector3(1.5f, 0.3f, -0.5f);
            landPivot.transform.localRotation = Quaternion.Euler(0f, 18f, 0f);
            landPivot.transform.localScale = Vector3.one * 0.85f;

            CreateMeshObject(
                landPivot.transform,
                "Hub",
                CreateLandMesh("Hub"),
                _testLandMaterial,
                new Vector3(0.25f, 0.6f, -0.15f),
                Quaternion.Euler(5f, 12f, 0f),
                Vector3.one * 1.1f);

            CreateMeshObject(
                root.transform,
                "CliffShelf",
                CreateLandMesh("CliffShelf"),
                _testLandMaterial,
                new Vector3(-1.75f, 0.2f, 1.4f),
                Quaternion.Euler(0f, -12f, 0f),
                Vector3.one * 0.95f);

            if (includeBoundary)
            {
                CreateMeshObject(
                    root.transform,
                    ManualOceanAssemblyUtility.DefaultBoundarySourceName,
                    CreateLandMesh("HubWorldWall"),
                    _testBoundaryMaterial,
                    new Vector3(0f, 0.4f, 0f),
                    Quaternion.identity,
                    Vector3.one * 1.3f);
            }

            if (includeShallow)
            {
                CreateMeshObject(
                    root.transform,
                    ManualOceanAssemblyUtility.DefaultShallowWaterSourceName,
                    CreateCompatibleWaterMesh("ShallowOcean", isShallow: true),
                    _testLandMaterial,
                    new Vector3(0f, -0.2f, 0f),
                    Quaternion.identity,
                    Vector3.one);
            }

            if (includeDeep)
            {
                CreateMeshObject(
                    root.transform,
                    ManualOceanAssemblyUtility.DefaultDeepWaterSourceName,
                    incompatibleDeep ? CreateIncompatibleWaterMesh("DeepOcean") : CreateCompatibleWaterMesh("DeepOcean", isShallow: false),
                    _testLandMaterial,
                    new Vector3(0f, -0.75f, 0f),
                    Quaternion.identity,
                    Vector3.one);
            }

            return root;
        }

        private static void CreateMeshObject(
            Transform parent,
            string childName,
            Mesh mesh,
            Material material,
            Vector3? localPosition = null,
            Quaternion? localRotation = null,
            Vector3? localScale = null)
        {
            var child = new GameObject(childName);
            child.transform.SetParent(parent, worldPositionStays: false);
            child.transform.localPosition = localPosition ?? Vector3.zero;
            child.transform.localRotation = localRotation ?? Quaternion.identity;
            child.transform.localScale = localScale ?? Vector3.one;

            var meshFilter = child.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            var meshRenderer = child.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
        }

        private static Mesh CreateLandMesh(string name)
        {
            var mesh = new Mesh
            {
                name = $"{name} Mesh",
                vertices = new[]
                {
                    new Vector3(-1f, 0.5f, -1f),
                    new Vector3(1f, 0.65f, -1f),
                    new Vector3(-1f, 0.55f, 1f),
                    new Vector3(1f, 0.7f, 1f)
                },
                triangles = new[] { 0, 2, 1, 2, 3, 1 },
                uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f)
                }
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateCompatibleWaterMesh(string name, bool isShallow)
        {
            float height = isShallow ? -0.2f : -1.1f;
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
                    bool includeCell = isShallow ? x <= 1 : x >= 2;
                    if (!includeCell)
                    {
                        continue;
                    }

                    AddCellTriangles(columns, x, z, triangles);
                }
            }

            var mesh = new Mesh
            {
                name = $"{name} Mesh",
                vertices = vertices,
                triangles = triangles.ToArray(),
                uv = uvs
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateIncompatibleWaterMesh(string name)
        {
            var mesh = new Mesh
            {
                name = $"{name} Mesh",
                vertices = new[]
                {
                    new Vector3(-2f, -1.2f, -1f),
                    new Vector3(2f, -1.2f, -1f),
                    new Vector3(-2f, -1.2f, 1f),
                    new Vector3(2f, -1.2f, 1f)
                },
                triangles = new[] { 0, 2, 1, 2, 3, 1 },
                uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f)
                }
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddCellTriangles(int columns, int x, int z, System.Collections.Generic.List<int> triangles)
        {
            int topLeft = z * columns + x;
            int topRight = topLeft + 1;
            int bottomLeft = topLeft + columns;
            int bottomRight = bottomLeft + 1;

            triangles.Add(topLeft);
            triangles.Add(bottomLeft);
            triangles.Add(topRight);

            triangles.Add(bottomLeft);
            triangles.Add(bottomRight);
            triangles.Add(topRight);
        }

        private static void AssertVectorApproximatelyEqual(Vector3 expected, Vector3 actual, float tolerance)
        {
            NUnitAssert.AreEqual(expected.x, actual.x, tolerance);
            NUnitAssert.AreEqual(expected.y, actual.y, tolerance);
            NUnitAssert.AreEqual(expected.z, actual.z, tolerance);
        }
    }
}
#endif
