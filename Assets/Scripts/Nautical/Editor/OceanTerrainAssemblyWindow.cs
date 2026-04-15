#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Bitbox.Toymageddon.Nautical;
using StormBreakers;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Bitbox.Toymageddon.Nautical.Editor
{
    public sealed class OceanTerrainAssemblyWindow : EditorWindow
    {
        private const string DefaultStormOceanPrefabPath = "Assets/Prefabs/StormOcean.prefab";
        private const string DefaultOutputAssetFolder = "Assets/Data/Terrain/OceanAssemblies";

        private GameObject _terraForgeGeneratedRoot;
        private GameObject _stormOceanPrefab;
        private string _outputName = string.Empty;
        private string _outputAssetFolder = DefaultOutputAssetFolder;
        private bool _replaceExistingOutput = true;
        private float _waterSurfaceHeight;
        private int _blendBandCount = OceanTerrainAssemblyUtility.DefaultBlendBandCount;
        private Color _shallowWaterColor = OceanTerrainAssemblyUtility.DefaultShallowWaterColor;
        private Color _deepWaterColor = OceanTerrainAssemblyUtility.DefaultDeepWaterColor;

        [MenuItem("Tools/BitBox Arcade/Ocean Terrain Assembly")]
        public static void ShowWindow()
        {
            var window = GetWindow<OceanTerrainAssemblyWindow>();
            window.titleContent = new GUIContent("Ocean Terrain Assembly");
            window.minSize = new Vector2(440f, 360f);
            window.Show();
        }

        private void OnEnable()
        {
            if (_stormOceanPrefab == null)
            {
                _stormOceanPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultStormOceanPrefabPath);
            }

            if (_terraForgeGeneratedRoot == null && Selection.activeGameObject != null)
            {
                _terraForgeGeneratedRoot = Selection.activeGameObject;
                RefreshOutputNameFromRoot();
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
            using (new EditorGUI.ChangeCheckScope())
            {
                GameObject selectedRoot = (GameObject)EditorGUILayout.ObjectField(
                    new GUIContent("TerraForge Generated Root", "Scene object containing Real Terrain, Shallow Water, and Deep Water children."),
                    _terraForgeGeneratedRoot,
                    typeof(GameObject),
                    allowSceneObjects: true);

                if (selectedRoot != _terraForgeGeneratedRoot)
                {
                    _terraForgeGeneratedRoot = selectedRoot;
                    RefreshOutputNameFromRoot();
                    RefreshWaterSurfaceHeightFromRoot();
                }
            }

            _stormOceanPrefab = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent("StormOcean Prefab", "Prefab used as the Storm-Breakers wave/settings/sampler template."),
                _stormOceanPrefab,
                typeof(GameObject),
                allowSceneObjects: false);

            if (GUILayout.Button("Use Selected Scene Object"))
            {
                _terraForgeGeneratedRoot = Selection.activeGameObject;
                RefreshOutputNameFromRoot();
                RefreshWaterSurfaceHeightFromRoot();
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            _outputName = EditorGUILayout.TextField(
                new GUIContent("Output Name", "Name of the generated scene root and output asset folder."),
                _outputName);
            _outputAssetFolder = EditorGUILayout.TextField(
                new GUIContent("Output Asset Folder", "Project folder where copied meshes and ocean material assets will be saved."),
                _outputAssetFolder);
            _replaceExistingOutput = EditorGUILayout.Toggle(
                new GUIContent("Replace Existing Output", "When enabled, an existing scene root with the same output name is replaced."),
                _replaceExistingOutput);
            _waterSurfaceHeight = EditorGUILayout.FloatField(
                new GUIContent("Water Surface Height", "Local Y height used to flatten the generated shallow/deep ocean meshes."),
                _waterSurfaceHeight);
            _blendBandCount = EditorGUILayout.IntSlider(
                new GUIContent("Shallow/Deep Blend Bands", "Number of extra transition rings generated across the shallow/deep ocean boundary. Higher values create a broader gradient and more child meshes."),
                _blendBandCount,
                0,
                OceanTerrainAssemblyUtility.MaxBlendBandCount);

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Water Colors", EditorStyles.boldLabel);
            _shallowWaterColor = EditorGUILayout.ColorField(
                new GUIContent("Shallow Water Color", "Tint applied to the generated shallow ocean material."),
                _shallowWaterColor);
            _deepWaterColor = EditorGUILayout.ColorField(
                new GUIContent("Deep Water Color", "Tint applied to the generated deep ocean material."),
                _deepWaterColor);

            EditorGUILayout.Space(12f);
            var request = BuildRequest();
            bool isValid = OceanTerrainAssemblyUtility.TryValidateRequest(request, out string validationError);
            if (!isValid)
            {
                EditorGUILayout.HelpBox(validationError, MessageType.Warning);
            }

            using (new EditorGUI.DisabledScope(!isValid))
            {
                if (GUILayout.Button("Create Ocean Terrain Assembly", GUILayout.Height(32f)))
                {
                    OceanTerrainAssemblyResult result = OceanTerrainAssemblyUtility.CreateAssembly(request);
                    Selection.activeGameObject = result.Root;
                    EditorGUIUtility.PingObject(result.Root);
                }
            }
        }

        private OceanTerrainAssemblyRequest BuildRequest()
        {
            return new OceanTerrainAssemblyRequest(
                _terraForgeGeneratedRoot,
                _stormOceanPrefab,
                _outputName,
                _outputAssetFolder,
                _replaceExistingOutput,
                _waterSurfaceHeight,
                _blendBandCount,
                _shallowWaterColor,
                _deepWaterColor);
        }

        private void RefreshOutputNameFromRoot()
        {
            if (_terraForgeGeneratedRoot == null)
            {
                return;
            }

            _outputName = OceanTerrainAssemblyUtility.BuildDefaultOutputName(_terraForgeGeneratedRoot.name);
        }

        private void RefreshWaterSurfaceHeightFromRoot()
        {
            if (_terraForgeGeneratedRoot != null
                && OceanTerrainAssemblyUtility.TryEstimateWaterSurfaceHeight(_terraForgeGeneratedRoot, out float waterSurfaceHeight))
            {
                _waterSurfaceHeight = waterSurfaceHeight;
            }
        }
    }

    public readonly struct OceanTerrainAssemblyRequest
    {
        public OceanTerrainAssemblyRequest(
            GameObject terraForgeGeneratedRoot,
            GameObject stormOceanPrefab,
            string outputName,
            string outputAssetFolder,
            bool replaceExistingOutput,
            float waterSurfaceHeight,
            int blendBandCount,
            Color shallowWaterColor,
            Color deepWaterColor)
        {
            TerraForgeGeneratedRoot = terraForgeGeneratedRoot;
            StormOceanPrefab = stormOceanPrefab;
            OutputName = outputName;
            OutputAssetFolder = outputAssetFolder;
            ReplaceExistingOutput = replaceExistingOutput;
            WaterSurfaceHeight = waterSurfaceHeight;
            BlendBandCount = Mathf.Clamp(blendBandCount, 0, OceanTerrainAssemblyUtility.MaxBlendBandCount);
            ShallowWaterColor = shallowWaterColor;
            DeepWaterColor = deepWaterColor;
        }

        public GameObject TerraForgeGeneratedRoot { get; }
        public GameObject StormOceanPrefab { get; }
        public string OutputName { get; }
        public string OutputAssetFolder { get; }
        public bool ReplaceExistingOutput { get; }
        public float WaterSurfaceHeight { get; }
        public int BlendBandCount { get; }
        public Color ShallowWaterColor { get; }
        public Color DeepWaterColor { get; }
    }

    public readonly struct OceanTerrainAssemblyResult
    {
        public OceanTerrainAssemblyResult(
            GameObject root,
            string assetFolder,
            Mesh terrainMesh,
            Mesh shallowOceanMesh,
            Mesh deepOceanMesh,
            Mesh[] blendOceanMeshes,
            Material oceanTemplateMaterial,
            Material shallowOceanMaterial,
            Material deepOceanMaterial,
            Material[] blendOceanMaterials)
        {
            Root = root;
            AssetFolder = assetFolder;
            TerrainMesh = terrainMesh;
            ShallowOceanMesh = shallowOceanMesh;
            DeepOceanMesh = deepOceanMesh;
            BlendOceanMeshes = blendOceanMeshes ?? Array.Empty<Mesh>();
            OceanTemplateMaterial = oceanTemplateMaterial;
            ShallowOceanMaterial = shallowOceanMaterial;
            DeepOceanMaterial = deepOceanMaterial;
            BlendOceanMaterials = blendOceanMaterials ?? Array.Empty<Material>();
        }

        public GameObject Root { get; }
        public string AssetFolder { get; }
        public Mesh TerrainMesh { get; }
        public Mesh ShallowOceanMesh { get; }
        public Mesh DeepOceanMesh { get; }
        public Mesh[] BlendOceanMeshes { get; }
        public Material OceanTemplateMaterial { get; }
        public Material ShallowOceanMaterial { get; }
        public Material DeepOceanMaterial { get; }
        public Material[] BlendOceanMaterials { get; }
    }

    public static class OceanTerrainAssemblyUtility
    {
        public const string RealTerrainChildName = "Real Terrain";
        public const string ShallowWaterChildName = "Shallow Water";
        public const string DeepWaterChildName = "Deep Water";
        public const string TerrainGeometryChildName = "Terrain Geometry";
        public const string ShallowOceanChildName = "Shallow Ocean";
        public const string DeepOceanChildName = "Deep Ocean";
        public const string OceanGradientChildName = "Ocean Gradient";
        public const string OceanSystemChildName = "Ocean System";
        public const int DefaultBlendBandCount = 8;
        public const int MaxBlendBandCount = 12;

        private const string WaterColorPropertyName = "_waterColor";
        private const string MinimalTransparencyPropertyName = "_minimalTransparency";
        private const string DefaultOutputFolder = "Assets/Data/Terrain/OceanAssemblies";
        private const int WaterLayer = 4;
        private const float BlendBandSurfaceOffset = 0.015f;
        private const float BlendBandMinimalTransparency = 0.68f;

        public static readonly Color DefaultShallowWaterColor = new(0.1f, 0.62f, 0.7f, 1f);
        public static readonly Color DefaultDeepWaterColor = new(0.03f, 0.25f, 0.43f, 1f);

        public static bool TryValidateRequest(OceanTerrainAssemblyRequest request, out string error)
        {
            if (request.TerraForgeGeneratedRoot == null)
            {
                error = "Assign a TerraForge generated root with Real Terrain, Shallow Water, and Deep Water children.";
                return false;
            }

            if (!request.TerraForgeGeneratedRoot.scene.IsValid())
            {
                error = "The TerraForge generated root must be a scene object, not a prefab asset.";
                return false;
            }

            if (!TryResolveLayer(request.TerraForgeGeneratedRoot, RealTerrainChildName, out _, out _, out string terrainError))
            {
                error = terrainError;
                return false;
            }

            if (!TryResolveLayer(request.TerraForgeGeneratedRoot, ShallowWaterChildName, out _, out _, out string shallowError))
            {
                error = shallowError;
                return false;
            }

            if (!TryResolveLayer(request.TerraForgeGeneratedRoot, DeepWaterChildName, out _, out _, out string deepError))
            {
                error = deepError;
                return false;
            }

            if (request.StormOceanPrefab == null)
            {
                error = "Assign the StormOcean prefab that should provide the ocean settings.";
                return false;
            }

            if (request.StormOceanPrefab.GetComponent<OceanController>() == null)
            {
                error = "The StormOcean prefab must have an OceanController on its root object.";
                return false;
            }

            if (ResolveOceanTemplateRenderer(request.StormOceanPrefab) == null)
            {
                error = "The StormOcean prefab must have a MeshRenderer with an ocean material.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.OutputName))
            {
                error = "Enter an output name.";
                return false;
            }

            string sanitizedOutputName = SanitizeName(request.OutputName);
            if (request.TerraForgeGeneratedRoot.transform.parent == null
                && string.Equals(request.TerraForgeGeneratedRoot.name, sanitizedOutputName, StringComparison.Ordinal))
            {
                error = "Output Name must differ from the source root name so the source TerraForge object is not replaced.";
                return false;
            }

            if (!IsProjectAssetFolderPath(ResolveOutputAssetFolder(request.OutputAssetFolder)))
            {
                error = "Output Asset Folder must be inside the Assets folder.";
                return false;
            }

            if (!request.ReplaceExistingOutput && FindSceneRoot(request.TerraForgeGeneratedRoot.scene, sanitizedOutputName) != null)
            {
                error = $"A scene root named '{sanitizedOutputName}' already exists. Enable Replace Existing Output or choose another name.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public static OceanTerrainAssemblyResult CreateAssembly(OceanTerrainAssemblyRequest request)
        {
            if (!TryValidateRequest(request, out string validationError))
            {
                throw new InvalidOperationException(validationError);
            }

            TryResolveLayer(request.TerraForgeGeneratedRoot, RealTerrainChildName, out MeshLayerSource terrainSource, out Mesh terrainSourceMesh, out _);
            TryResolveLayer(request.TerraForgeGeneratedRoot, ShallowWaterChildName, out MeshLayerSource shallowSource, out Mesh shallowSourceMesh, out _);
            TryResolveLayer(request.TerraForgeGeneratedRoot, DeepWaterChildName, out MeshLayerSource deepSource, out Mesh deepSourceMesh, out _);

            string outputName = SanitizeName(request.OutputName);
            string assetRoot = EnsureOutputAssetFolder(ResolveOutputAssetFolder(request.OutputAssetFolder), outputName);

            Mesh terrainMesh = CreateOrReplaceMeshAsset(terrainSourceMesh, $"{assetRoot}/{outputName}_TerrainGeometry.asset", $"{outputName}_TerrainGeometry");
            Mesh shallowMesh = CreateOrReplaceMeshAsset(
                shallowSourceMesh,
                $"{assetRoot}/{outputName}_ShallowOceanMesh.asset",
                $"{outputName}_ShallowOceanMesh",
                request.WaterSurfaceHeight,
                shallowSource.Transform.localPosition.y);
            Mesh deepMesh = CreateOrReplaceMeshAsset(
                deepSourceMesh,
                $"{assetRoot}/{outputName}_DeepOceanMesh.asset",
                $"{outputName}_DeepOceanMesh",
                request.WaterSurfaceHeight,
                deepSource.Transform.localPosition.y);

            MeshRenderer stormOceanTemplateRenderer = ResolveOceanTemplateRenderer(request.StormOceanPrefab);
            Material baseOceanMaterial = stormOceanTemplateRenderer.sharedMaterial;
            Material oceanTemplateMaterial = CreateOrReplaceMaterialAsset(baseOceanMaterial, $"{assetRoot}/{outputName}_OceanTemplate.mat", $"{outputName}_OceanTemplate");
            Material shallowMaterial = CreateOrReplaceMaterialAsset(baseOceanMaterial, $"{assetRoot}/{outputName}_ShallowOcean.mat", $"{outputName}_ShallowOcean");
            Material deepMaterial = CreateOrReplaceMaterialAsset(baseOceanMaterial, $"{assetRoot}/{outputName}_DeepOcean.mat", $"{outputName}_DeepOcean");
            SetWaterColor(oceanTemplateMaterial, request.DeepWaterColor);
            SetWaterColor(shallowMaterial, request.ShallowWaterColor);
            SetWaterColor(deepMaterial, request.DeepWaterColor);

            OceanBlendBand[] blendBands = CreateOrReplaceBlendBands(
                shallowSourceMesh,
                deepSourceMesh,
                shallowSource,
                deepSource,
                baseOceanMaterial,
                assetRoot,
                outputName,
                request.WaterSurfaceHeight,
                request.BlendBandCount,
                request.ShallowWaterColor,
                request.DeepWaterColor);
            if (request.BlendBandCount > 0 && blendBands.Length == 0)
            {
                Debug.LogWarning(
                    "Ocean Terrain Assembly could not build shallow/deep gradient bands. The shallow and deep meshes must share the same vertex topology with adjacent triangles along the boundary.",
                    request.TerraForgeGeneratedRoot);
            }

            GameObject existingOutput = FindSceneRoot(request.TerraForgeGeneratedRoot.scene, outputName);
            if (existingOutput != null)
            {
                UnityEngine.Object.DestroyImmediate(existingOutput);
            }

            GameObject root = new GameObject(outputName);
            SceneManager.MoveGameObjectToScene(root, request.TerraForgeGeneratedRoot.scene);
            root.transform.SetPositionAndRotation(
                request.TerraForgeGeneratedRoot.transform.position,
                request.TerraForgeGeneratedRoot.transform.rotation);
            root.transform.localScale = request.TerraForgeGeneratedRoot.transform.lossyScale;

            GameObject terrainObject = CreateMeshChild(
                root.transform,
                TerrainGeometryChildName,
                terrainSource,
                terrainMesh,
                terrainSource.Renderer.sharedMaterials,
                includeCollider: true,
                forceWaterLayer: false);
            GameObject shallowObject = CreateMeshChild(
                root.transform,
                ShallowOceanChildName,
                shallowSource,
                shallowMesh,
                new[] { shallowMaterial },
                includeCollider: false,
                forceWaterLayer: true);
            GameObject deepObject = CreateMeshChild(
                root.transform,
                DeepOceanChildName,
                deepSource,
                deepMesh,
                new[] { deepMaterial },
                includeCollider: false,
                forceWaterLayer: true);
            GameObject gradientObject = CreateGradientChild(root.transform, shallowSource);
            MeshRenderer[] blendRenderers = CreateBlendBandChildren(gradientObject.transform, shallowSource, blendBands);
            GameObject oceanSystem = CreateOceanSystem(root.transform, request.StormOceanPrefab, oceanTemplateMaterial);

            var sync = root.AddComponent<StormOceanLayerMaterialSync>();
            sync.Configure(
                ResolveOceanTemplateRenderer(oceanSystem),
                shallowObject.GetComponent<MeshRenderer>(),
                deepObject.GetComponent<MeshRenderer>(),
                request.ShallowWaterColor,
                request.DeepWaterColor,
                blendRenderers,
                ExtractBlendColors(blendBands),
                BlendBandMinimalTransparency);

            EditorSceneManager.MarkSceneDirty(root.scene);
            AssetDatabase.SaveAssets();

            return new OceanTerrainAssemblyResult(
                root,
                assetRoot,
                terrainMesh,
                shallowMesh,
                deepMesh,
                ExtractBlendMeshes(blendBands),
                oceanTemplateMaterial,
                shallowMaterial,
                deepMaterial,
                ExtractBlendMaterials(blendBands));
        }

        public static string BuildDefaultOutputName(string sourceRootName)
        {
            string sourceName = string.IsNullOrWhiteSpace(sourceRootName) ? "TerraForgeTerrain" : sourceRootName.Trim();
            if (sourceName.Equals("Generated Island Terrain", StringComparison.OrdinalIgnoreCase))
            {
                sourceName = "TerraForgeTerrain";
            }

            if (sourceName.EndsWith("OceanAssembly", StringComparison.OrdinalIgnoreCase))
            {
                return SanitizeName(sourceName);
            }

            return SanitizeName($"{sourceName}OceanAssembly");
        }

        public static bool TryEstimateWaterSurfaceHeight(GameObject terraForgeGeneratedRoot, out float waterSurfaceHeight)
        {
            bool hasHeight = false;
            float maxHeight = float.NegativeInfinity;
            AccumulateMaxLayerHeight(terraForgeGeneratedRoot, ShallowWaterChildName, ref hasHeight, ref maxHeight);
            AccumulateMaxLayerHeight(terraForgeGeneratedRoot, DeepWaterChildName, ref hasHeight, ref maxHeight);

            waterSurfaceHeight = hasHeight ? maxHeight : 0f;
            return hasHeight;
        }

        private static OceanBlendBand[] CreateOrReplaceBlendBands(
            Mesh shallowSourceMesh,
            Mesh deepSourceMesh,
            MeshLayerSource shallowSource,
            MeshLayerSource deepSource,
            Material baseOceanMaterial,
            string assetRoot,
            string outputName,
            float waterSurfaceHeight,
            int blendBandCount,
            Color shallowWaterColor,
            Color deepWaterColor)
        {
            int clampedBandCount = Mathf.Clamp(blendBandCount, 0, MaxBlendBandCount);
            if (clampedBandCount == 0
                || shallowSourceMesh.vertexCount == 0
                || shallowSourceMesh.vertexCount != deepSourceMesh.vertexCount)
            {
                return Array.Empty<OceanBlendBand>();
            }

            WaterTriangle[] waterTriangles = BuildWaterTriangles(
                shallowSourceMesh.triangles,
                deepSourceMesh.triangles,
                shallowSourceMesh.vertexCount);
            if (waterTriangles.Length == 0)
            {
                return Array.Empty<OceanBlendBand>();
            }

            Dictionary<EdgeKey, List<int>> edgeMap = BuildEdgeMap(waterTriangles);
            int[] ringDistances = BuildBoundaryRingDistances(waterTriangles, edgeMap, clampedBandCount);
            var blendBands = new List<OceanBlendBand>(clampedBandCount * 2);
            for (int ring = 0; ring < clampedBandCount; ring++)
            {
                TryCreateBlendBand(
                    OceanBlendSide.Shallow,
                    ring,
                    clampedBandCount,
                    shallowSourceMesh,
                    shallowSource.Transform,
                    shallowSource.Transform,
                    waterTriangles,
                    ringDistances,
                    baseOceanMaterial,
                    assetRoot,
                    outputName,
                    waterSurfaceHeight,
                    shallowWaterColor,
                    deepWaterColor,
                    blendBands);

                TryCreateBlendBand(
                    OceanBlendSide.Deep,
                    ring,
                    clampedBandCount,
                    deepSourceMesh,
                    deepSource.Transform,
                    shallowSource.Transform,
                    waterTriangles,
                    ringDistances,
                    baseOceanMaterial,
                    assetRoot,
                    outputName,
                    waterSurfaceHeight,
                    shallowWaterColor,
                    deepWaterColor,
                    blendBands);
            }

            return blendBands.ToArray();
        }

        private static void TryCreateBlendBand(
            OceanBlendSide side,
            int ring,
            int blendBandCount,
            Mesh sourceMesh,
            Transform sourceTransform,
            Transform blendTransform,
            WaterTriangle[] waterTriangles,
            int[] ringDistances,
            Material baseOceanMaterial,
            string assetRoot,
            string outputName,
            float waterSurfaceHeight,
            Color shallowWaterColor,
            Color deepWaterColor,
            List<OceanBlendBand> blendBands)
        {
            string sideName = side == OceanBlendSide.Shallow ? "Shallow" : "Deep";
            string childName = $"{sideName} Gradient {ring + 1:00}";
            string assetName = $"{outputName}_{sideName}OceanGradient{ring + 1:00}";
            Mesh blendMesh = CreateBlendBandMesh(
                sourceMesh,
                sourceTransform,
                blendTransform,
                waterTriangles,
                ringDistances,
                side,
                ring,
                waterSurfaceHeight + BlendBandSurfaceOffset + ring * 0.001f,
                assetName + "Mesh");

            if (blendMesh == null)
            {
                return;
            }

            string meshAssetPath = $"{assetRoot}/{assetName}Mesh.asset";
            DeleteExistingAsset(meshAssetPath);
            AssetDatabase.CreateAsset(blendMesh, meshAssetPath);

            Material blendMaterial = CreateOrReplaceMaterialAsset(baseOceanMaterial, $"{assetRoot}/{assetName}.mat", assetName);
            Color waterColor = CalculateBlendBandColor(side, ring, blendBandCount, shallowWaterColor, deepWaterColor);
            SetWaterColor(blendMaterial, waterColor);
            SetMinimalTransparency(blendMaterial, BlendBandMinimalTransparency);
            blendBands.Add(new OceanBlendBand(childName, blendMesh, blendMaterial, waterColor));
        }

        private static Mesh CreateBlendBandMesh(
            Mesh sourceMesh,
            Transform sourceTransform,
            Transform blendTransform,
            WaterTriangle[] waterTriangles,
            int[] ringDistances,
            OceanBlendSide side,
            int ring,
            float waterSurfaceHeight,
            string meshName)
        {
            var triangles = new List<int>();
            for (int i = 0; i < waterTriangles.Length; i++)
            {
                if (ringDistances[i] == ring && waterTriangles[i].Side == side)
                {
                    triangles.Add(waterTriangles[i].A);
                    triangles.Add(waterTriangles[i].B);
                    triangles.Add(waterTriangles[i].C);
                }
            }

            if (triangles.Count == 0)
            {
                return null;
            }

            Vector3[] sourceVertices = sourceMesh.vertices;
            var vertices = new Vector3[sourceVertices.Length];
            float localWaterHeight = waterSurfaceHeight - blendTransform.localPosition.y;
            for (int i = 0; i < sourceVertices.Length; i++)
            {
                Vector3 localVertex = sourceVertices[i];
                if (sourceTransform != blendTransform)
                {
                    localVertex = blendTransform.InverseTransformPoint(sourceTransform.TransformPoint(localVertex));
                }

                localVertex.y = localWaterHeight;
                vertices[i] = localVertex;
            }

            var mesh = new Mesh
            {
                name = meshName,
                indexFormat = vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16,
                vertices = vertices,
                triangles = triangles.ToArray()
            };

            Vector2[] uvs = sourceMesh.uv;
            if (uvs != null && uvs.Length == vertices.Length)
            {
                mesh.uv = uvs;
            }

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static WaterTriangle[] BuildWaterTriangles(int[] shallowTriangles, int[] deepTriangles, int vertexCount)
        {
            var triangles = new List<WaterTriangle>((shallowTriangles.Length + deepTriangles.Length) / 3);
            AppendWaterTriangles(shallowTriangles, vertexCount, OceanBlendSide.Shallow, triangles);
            AppendWaterTriangles(deepTriangles, vertexCount, OceanBlendSide.Deep, triangles);
            return triangles.ToArray();
        }

        private static void AppendWaterTriangles(
            int[] sourceTriangles,
            int vertexCount,
            OceanBlendSide side,
            List<WaterTriangle> triangles)
        {
            for (int i = 0; i + 2 < sourceTriangles.Length; i += 3)
            {
                int a = sourceTriangles[i];
                int b = sourceTriangles[i + 1];
                int c = sourceTriangles[i + 2];
                if (a >= 0 && a < vertexCount && b >= 0 && b < vertexCount && c >= 0 && c < vertexCount)
                {
                    triangles.Add(new WaterTriangle(a, b, c, side));
                }
            }
        }

        private static Dictionary<EdgeKey, List<int>> BuildEdgeMap(WaterTriangle[] waterTriangles)
        {
            var edgeMap = new Dictionary<EdgeKey, List<int>>(waterTriangles.Length * 3);
            for (int i = 0; i < waterTriangles.Length; i++)
            {
                AddTriangleEdge(edgeMap, new EdgeKey(waterTriangles[i].A, waterTriangles[i].B), i);
                AddTriangleEdge(edgeMap, new EdgeKey(waterTriangles[i].B, waterTriangles[i].C), i);
                AddTriangleEdge(edgeMap, new EdgeKey(waterTriangles[i].C, waterTriangles[i].A), i);
            }

            return edgeMap;
        }

        private static void AddTriangleEdge(Dictionary<EdgeKey, List<int>> edgeMap, EdgeKey edgeKey, int triangleIndex)
        {
            if (!edgeMap.TryGetValue(edgeKey, out List<int> triangleIndices))
            {
                triangleIndices = new List<int>(2);
                edgeMap.Add(edgeKey, triangleIndices);
            }

            triangleIndices.Add(triangleIndex);
        }

        private static int[] BuildBoundaryRingDistances(
            WaterTriangle[] waterTriangles,
            Dictionary<EdgeKey, List<int>> edgeMap,
            int blendBandCount)
        {
            var ringDistances = new int[waterTriangles.Length];
            for (int i = 0; i < ringDistances.Length; i++)
            {
                ringDistances[i] = -1;
            }
            var queue = new Queue<int>();

            foreach (KeyValuePair<EdgeKey, List<int>> edge in edgeMap)
            {
                bool hasShallow = false;
                bool hasDeep = false;
                List<int> triangleIndices = edge.Value;
                for (int i = 0; i < triangleIndices.Count; i++)
                {
                    if (waterTriangles[triangleIndices[i]].Side == OceanBlendSide.Shallow)
                    {
                        hasShallow = true;
                    }
                    else
                    {
                        hasDeep = true;
                    }
                }

                if (!hasShallow || !hasDeep)
                {
                    continue;
                }

                for (int i = 0; i < triangleIndices.Count; i++)
                {
                    EnqueueRingTriangle(triangleIndices[i], 0, ringDistances, queue);
                }
            }

            while (queue.Count > 0)
            {
                int triangleIndex = queue.Dequeue();
                int distance = ringDistances[triangleIndex];
                if (distance >= blendBandCount - 1)
                {
                    continue;
                }

                EnqueueNeighborTriangles(waterTriangles[triangleIndex].A, waterTriangles[triangleIndex].B);
                EnqueueNeighborTriangles(waterTriangles[triangleIndex].B, waterTriangles[triangleIndex].C);
                EnqueueNeighborTriangles(waterTriangles[triangleIndex].C, waterTriangles[triangleIndex].A);

                void EnqueueNeighborTriangles(int a, int b)
                {
                    if (!edgeMap.TryGetValue(new EdgeKey(a, b), out List<int> neighbors))
                    {
                        return;
                    }

                    for (int i = 0; i < neighbors.Count; i++)
                    {
                        EnqueueRingTriangle(neighbors[i], distance + 1, ringDistances, queue);
                    }
                }
            }

            return ringDistances;
        }

        private static void EnqueueRingTriangle(int triangleIndex, int distance, int[] ringDistances, Queue<int> queue)
        {
            if (ringDistances[triangleIndex] != -1)
            {
                return;
            }

            ringDistances[triangleIndex] = distance;
            queue.Enqueue(triangleIndex);
        }

        private static Color CalculateBlendBandColor(
            OceanBlendSide side,
            int ring,
            int blendBandCount,
            Color shallowWaterColor,
            Color deepWaterColor)
        {
            float blendAmount = 0.55f * (1f - ring / Mathf.Max(1f, blendBandCount));
            return side == OceanBlendSide.Shallow
                ? Color.Lerp(shallowWaterColor, deepWaterColor, blendAmount)
                : Color.Lerp(deepWaterColor, shallowWaterColor, blendAmount);
        }

        private static GameObject CreateGradientChild(Transform parent, MeshLayerSource blendSource)
        {
            var child = new GameObject(OceanGradientChildName);
            child.transform.SetParent(parent, worldPositionStays: false);
            child.transform.localPosition = blendSource.Transform.localPosition;
            child.transform.localRotation = blendSource.Transform.localRotation;
            child.transform.localScale = blendSource.Transform.localScale;
            child.layer = WaterLayer;
            return child;
        }

        private static MeshRenderer[] CreateBlendBandChildren(
            Transform parent,
            MeshLayerSource blendSource,
            OceanBlendBand[] blendBands)
        {
            var renderers = new MeshRenderer[blendBands.Length];
            for (int i = 0; i < blendBands.Length; i++)
            {
                var child = new GameObject(blendBands[i].ChildName);
                child.transform.SetParent(parent, worldPositionStays: false);
                child.layer = WaterLayer;

                var meshFilter = child.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = blendBands[i].Mesh;

                var meshRenderer = child.AddComponent<MeshRenderer>();
                CopyRendererSettings(blendSource.Renderer, meshRenderer);
                meshRenderer.sharedMaterial = blendBands[i].Material;
                meshRenderer.sortingOrder = 10 + i;
                renderers[i] = meshRenderer;
            }

            return renderers;
        }

        private static Mesh[] ExtractBlendMeshes(OceanBlendBand[] blendBands)
        {
            var meshes = new Mesh[blendBands.Length];
            for (int i = 0; i < blendBands.Length; i++)
            {
                meshes[i] = blendBands[i].Mesh;
            }

            return meshes;
        }

        private static Material[] ExtractBlendMaterials(OceanBlendBand[] blendBands)
        {
            var materials = new Material[blendBands.Length];
            for (int i = 0; i < blendBands.Length; i++)
            {
                materials[i] = blendBands[i].Material;
            }

            return materials;
        }

        private static Color[] ExtractBlendColors(OceanBlendBand[] blendBands)
        {
            var colors = new Color[blendBands.Length];
            for (int i = 0; i < blendBands.Length; i++)
            {
                colors[i] = blendBands[i].WaterColor;
            }

            return colors;
        }

        private static GameObject CreateOceanSystem(Transform parent, GameObject stormOceanPrefab, Material oceanTemplateMaterial)
        {
            var oceanSystem = PrefabUtility.InstantiatePrefab(stormOceanPrefab, parent) as GameObject;
            if (oceanSystem == null)
            {
                throw new InvalidOperationException("Could not instantiate the StormOcean prefab.");
            }

            oceanSystem.name = OceanSystemChildName;
            oceanSystem.transform.localPosition = Vector3.zero;
            oceanSystem.transform.localRotation = Quaternion.identity;
            oceanSystem.transform.localScale = Vector3.one;
            SetLayerRecursively(oceanSystem, WaterLayer);

            MeshRenderer[] renderers = oceanSystem.GetComponentsInChildren<MeshRenderer>(includeInactive: true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].sharedMaterial = oceanTemplateMaterial;
                renderers[i].enabled = false;
            }

            OceanController oceanController = oceanSystem.GetComponent<OceanController>();
            if (oceanController != null)
            {
                oceanController.waterColor = oceanTemplateMaterial.HasProperty(WaterColorPropertyName)
                    ? oceanTemplateMaterial.GetColor(WaterColorPropertyName)
                    : oceanController.waterColor;
                oceanController.UpdateWaves();
                oceanController.UpdateWind();
                oceanController.UpdateLighting();
            }

            return oceanSystem;
        }

        private static GameObject CreateMeshChild(
            Transform parent,
            string childName,
            MeshLayerSource source,
            Mesh mesh,
            Material[] materials,
            bool includeCollider,
            bool forceWaterLayer)
        {
            var child = new GameObject(childName);
            child.transform.SetParent(parent, worldPositionStays: false);
            child.transform.localPosition = source.Transform.localPosition;
            child.transform.localRotation = source.Transform.localRotation;
            child.transform.localScale = source.Transform.localScale;
            child.layer = forceWaterLayer ? WaterLayer : source.GameObject.layer;

            var meshFilter = child.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            var meshRenderer = child.AddComponent<MeshRenderer>();
            CopyRendererSettings(source.Renderer, meshRenderer);
            meshRenderer.sharedMaterials = materials;

            if (includeCollider)
            {
                var meshCollider = child.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = mesh;
            }

            return child;
        }

        private static void CopyRendererSettings(MeshRenderer source, MeshRenderer destination)
        {
            destination.shadowCastingMode = source.shadowCastingMode;
            destination.receiveShadows = source.receiveShadows;
            destination.lightProbeUsage = source.lightProbeUsage;
            destination.reflectionProbeUsage = source.reflectionProbeUsage;
            destination.motionVectorGenerationMode = source.motionVectorGenerationMode;
            destination.allowOcclusionWhenDynamic = source.allowOcclusionWhenDynamic;
        }

        private static Mesh CreateOrReplaceMeshAsset(Mesh sourceMesh, string assetPath, string meshName)
        {
            DeleteExistingAsset(assetPath);
            Mesh mesh = UnityEngine.Object.Instantiate(sourceMesh);
            mesh.name = meshName;
            AssetDatabase.CreateAsset(mesh, assetPath);
            return mesh;
        }

        private static Mesh CreateOrReplaceMeshAsset(
            Mesh sourceMesh,
            string assetPath,
            string meshName,
            float flattenedRootY,
            float childLocalY)
        {
            DeleteExistingAsset(assetPath);
            Mesh mesh = UnityEngine.Object.Instantiate(sourceMesh);
            mesh.name = meshName;
            float flattenedMeshY = flattenedRootY - childLocalY;
            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i].y = flattenedMeshY;
            }

            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, assetPath);
            return mesh;
        }

        private static Material CreateOrReplaceMaterialAsset(Material sourceMaterial, string assetPath, string materialName)
        {
            DeleteExistingAsset(assetPath);
            var material = new Material(sourceMaterial)
            {
                name = materialName
            };
            AssetDatabase.CreateAsset(material, assetPath);
            return material;
        }

        private static void DeleteExistingAsset(string assetPath)
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
        }

        private static void SetWaterColor(Material material, Color color)
        {
            if (material != null && material.HasProperty(WaterColorPropertyName))
            {
                material.SetColor(WaterColorPropertyName, color);
            }
        }

        private static void SetMinimalTransparency(Material material, float minimalTransparency)
        {
            if (material != null && material.HasProperty(MinimalTransparencyPropertyName))
            {
                material.SetFloat(MinimalTransparencyPropertyName, minimalTransparency);
            }
        }

        private static MeshRenderer ResolveOceanTemplateRenderer(GameObject stormOceanPrefab)
        {
            if (stormOceanPrefab == null)
            {
                return null;
            }

            MeshRenderer rootRenderer = stormOceanPrefab.GetComponent<MeshRenderer>();
            if (rootRenderer != null && rootRenderer.sharedMaterial != null)
            {
                return rootRenderer;
            }

            MeshRenderer[] renderers = stormOceanPrefab.GetComponentsInChildren<MeshRenderer>(includeInactive: true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].sharedMaterial != null)
                {
                    return renderers[i];
                }
            }

            return null;
        }

        private static bool TryResolveLayer(
            GameObject root,
            string childName,
            out MeshLayerSource source,
            out Mesh mesh,
            out string error)
        {
            source = default;
            mesh = null;

            Transform child = root != null ? root.transform.Find(childName) : null;
            if (child == null)
            {
                error = $"The TerraForge generated root is missing a '{childName}' child.";
                return false;
            }

            MeshFilter meshFilter = child.GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = child.GetComponent<MeshRenderer>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                error = $"The '{childName}' child must have a MeshFilter with a mesh.";
                return false;
            }

            if (meshRenderer == null)
            {
                error = $"The '{childName}' child must have a MeshRenderer.";
                return false;
            }

            mesh = meshFilter.sharedMesh;
            source = new MeshLayerSource(child.gameObject, child, meshRenderer);
            error = string.Empty;
            return true;
        }

        private static void AccumulateMaxLayerHeight(
            GameObject root,
            string childName,
            ref bool hasHeight,
            ref float maxHeight)
        {
            Transform child = root != null ? root.transform.Find(childName) : null;
            MeshFilter meshFilter = child != null ? child.GetComponent<MeshFilter>() : null;
            Mesh mesh = meshFilter != null ? meshFilter.sharedMesh : null;
            if (mesh == null)
            {
                return;
            }

            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i++)
            {
                int vertexIndex = triangles[i];
                if (vertexIndex < 0 || vertexIndex >= vertices.Length)
                {
                    continue;
                }

                float localHeight = vertices[vertexIndex].y + child.localPosition.y;
                if (!hasHeight || localHeight > maxHeight)
                {
                    hasHeight = true;
                    maxHeight = localHeight;
                }
            }
        }

        private static string EnsureOutputAssetFolder(string baseFolder, string outputName)
        {
            string normalizedBaseFolder = ResolveOutputAssetFolder(baseFolder);
            EnsureProjectFolder(normalizedBaseFolder);
            string outputFolder = $"{normalizedBaseFolder}/{SanitizeName(outputName)}";
            EnsureProjectFolder(outputFolder);
            return outputFolder;
        }

        private static string ResolveOutputAssetFolder(string folder)
        {
            return string.IsNullOrWhiteSpace(folder)
                ? DefaultOutputFolder
                : folder.Replace("\\", "/").TrimEnd('/');
        }

        private static bool IsProjectAssetFolderPath(string folder)
        {
            return !string.IsNullOrWhiteSpace(folder)
                && (folder.Equals("Assets", StringComparison.Ordinal)
                    || folder.StartsWith("Assets/", StringComparison.Ordinal));
        }

        private static void EnsureProjectFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder)?.Replace("\\", "/");
            string folderName = Path.GetFileName(folder);
            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(folderName))
            {
                throw new InvalidOperationException($"Invalid asset folder path '{folder}'.");
            }

            EnsureProjectFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }

        private static GameObject FindSceneRoot(Scene scene, string rootName)
        {
            if (!scene.IsValid() || string.IsNullOrWhiteSpace(rootName))
            {
                return null;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] != null && roots[i].name == rootName)
                {
                    return roots[i];
                }
            }

            return null;
        }

        private static string SanitizeName(string value)
        {
            string trimmed = string.IsNullOrWhiteSpace(value) ? "OceanTerrainAssembly" : value.Trim();
            char[] invalidChars = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalidChars.Length; i++)
            {
                trimmed = trimmed.Replace(invalidChars[i], '_');
            }

            return trimmed.Replace(' ', '_');
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            for (int i = 0; i < root.transform.childCount; i++)
            {
                SetLayerRecursively(root.transform.GetChild(i).gameObject, layer);
            }
        }

        private enum OceanBlendSide
        {
            Shallow,
            Deep
        }

        private readonly struct OceanBlendBand
        {
            public OceanBlendBand(string childName, Mesh mesh, Material material, Color waterColor)
            {
                ChildName = childName;
                Mesh = mesh;
                Material = material;
                WaterColor = waterColor;
            }

            public string ChildName { get; }
            public Mesh Mesh { get; }
            public Material Material { get; }
            public Color WaterColor { get; }
        }

        private readonly struct WaterTriangle
        {
            public WaterTriangle(int a, int b, int c, OceanBlendSide side)
            {
                A = a;
                B = b;
                C = c;
                Side = side;
            }

            public int A { get; }
            public int B { get; }
            public int C { get; }
            public OceanBlendSide Side { get; }
        }

        private readonly struct EdgeKey : IEquatable<EdgeKey>
        {
            public EdgeKey(int a, int b)
            {
                if (a < b)
                {
                    A = a;
                    B = b;
                }
                else
                {
                    A = b;
                    B = a;
                }
            }

            public int A { get; }
            public int B { get; }

            public bool Equals(EdgeKey other)
            {
                return A == other.A && B == other.B;
            }

            public override bool Equals(object obj)
            {
                return obj is EdgeKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (A * 397) ^ B;
                }
            }
        }

        private readonly struct MeshLayerSource
        {
            public MeshLayerSource(GameObject gameObject, Transform transform, MeshRenderer renderer)
            {
                GameObject = gameObject;
                Transform = transform;
                Renderer = renderer;
            }

            public GameObject GameObject { get; }
            public Transform Transform { get; }
            public MeshRenderer Renderer { get; }
        }
    }
}
#endif
