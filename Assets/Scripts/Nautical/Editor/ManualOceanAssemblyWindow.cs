#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Bitbox.Toymageddon.Nautical;
using StormBreakers;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bitbox.Toymageddon.Nautical.Editor
{
    public sealed class ManualOceanAssemblyWindow : EditorWindow
    {
        private const string DefaultStormOceanPrefabPath = "Assets/Prefabs/StormOcean.prefab";

        private GameObject _sourceRoot;
        private GameObject _stormOceanPrefab;
        private string _outputName = string.Empty;
        private string _outputAssetFolder = OceanAssemblySharedUtility.DefaultOutputAssetFolder;
        private bool _replaceExistingOutput = true;
        private float _waterSurfaceHeight;
        private int _blendBandCount = OceanTerrainAssemblyUtility.DefaultBlendBandCount;
        private Color _shallowWaterColor = OceanTerrainAssemblyUtility.DefaultShallowWaterColor;
        private Color _deepWaterColor = OceanTerrainAssemblyUtility.DefaultDeepWaterColor;
        private string _deepWaterSourceName = ManualOceanAssemblyUtility.DefaultDeepWaterSourceName;
        private string _shallowWaterSourceName = ManualOceanAssemblyUtility.DefaultShallowWaterSourceName;
        private string _boundarySourceName = ManualOceanAssemblyUtility.DefaultBoundarySourceName;

        [MenuItem("Tools/BitBox Arcade/Manual Ocean Assembly")]
        public static void ShowWindow()
        {
            var window = GetWindow<ManualOceanAssemblyWindow>();
            window.titleContent = new GUIContent("Manual Ocean Assembly");
            window.minSize = new Vector2(460f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            if (_stormOceanPrefab == null)
            {
                _stormOceanPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultStormOceanPrefabPath);
            }

            if (_sourceRoot == null)
            {
                _sourceRoot = Selection.activeObject as GameObject;
                RefreshOutputNameFromSource();
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
            GameObject selectedRoot = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent("Source Root", "Scene object, prefab asset, or imported model root containing manual land and water meshes."),
                _sourceRoot,
                typeof(GameObject),
                allowSceneObjects: true);

            if (selectedRoot != _sourceRoot)
            {
                _sourceRoot = selectedRoot;
                RefreshOutputNameFromSource();
            }

            _stormOceanPrefab = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent("StormOcean Prefab", "Prefab used as the Storm-Breakers wave/settings/sampler template for visible water layers."),
                _stormOceanPrefab,
                typeof(GameObject),
                allowSceneObjects: false);

            if (GUILayout.Button("Use Selected Object"))
            {
                _sourceRoot = Selection.activeObject as GameObject;
                RefreshOutputNameFromSource();
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Roles", EditorStyles.boldLabel);
            _deepWaterSourceName = EditorGUILayout.TextField(
                new GUIContent("Deep Water Name", "Optional authored mesh object name used for the deep ocean surface."),
                _deepWaterSourceName);
            _shallowWaterSourceName = EditorGUILayout.TextField(
                new GUIContent("Shallow Water Name", "Optional authored mesh object name used for the shallow ocean surface."),
                _shallowWaterSourceName);
            _boundarySourceName = EditorGUILayout.TextField(
                new GUIContent("Boundary Name", "Optional authored mesh object name preserved as a visible collidable boundary under Terrain Geometry."),
                _boundarySourceName);

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
                new GUIContent("Water Surface Height", "Local Y height used to flatten generated shallow/deep water meshes."),
                _waterSurfaceHeight);
            _blendBandCount = EditorGUILayout.IntSlider(
                new GUIContent("Shallow/Deep Blend Bands", "Number of extra transition rings generated across the shallow/deep ocean boundary."),
                _blendBandCount,
                0,
                OceanTerrainAssemblyUtility.MaxBlendBandCount);

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Water Colors", EditorStyles.boldLabel);
            _shallowWaterColor = EditorGUILayout.ColorField(
                new GUIContent("Shallow Water Color", "Tint applied to generated shallow ocean materials."),
                _shallowWaterColor);
            _deepWaterColor = EditorGUILayout.ColorField(
                new GUIContent("Deep Water Color", "Tint applied to generated deep ocean materials."),
                _deepWaterColor);

            EditorGUILayout.Space(12f);
            ManualOceanAssemblyRequest request = BuildRequest();
            bool isValid = ManualOceanAssemblyUtility.TryValidateRequest(request, out string validationError);
            if (!isValid)
            {
                EditorGUILayout.HelpBox(validationError, MessageType.Warning);
            }

            using (new EditorGUI.DisabledScope(!isValid))
            {
                if (GUILayout.Button("Create Manual Ocean Assembly", GUILayout.Height(32f)))
                {
                    ManualOceanAssemblyResult result = ManualOceanAssemblyUtility.CreateAssembly(request);
                    Selection.activeGameObject = result.Root;
                    EditorGUIUtility.PingObject(result.Root);
                }
            }
        }

        private ManualOceanAssemblyRequest BuildRequest()
        {
            return new ManualOceanAssemblyRequest(
                _sourceRoot,
                _stormOceanPrefab,
                _outputName,
                _outputAssetFolder,
                _replaceExistingOutput,
                _waterSurfaceHeight,
                _blendBandCount,
                _shallowWaterColor,
                _deepWaterColor,
                _deepWaterSourceName,
                _shallowWaterSourceName,
                _boundarySourceName);
        }

        private void RefreshOutputNameFromSource()
        {
            if (_sourceRoot == null)
            {
                return;
            }

            _outputName = ManualOceanAssemblyUtility.BuildDefaultOutputName(_sourceRoot.name);
        }
    }

    public readonly struct ManualOceanAssemblyRequest
    {
        public ManualOceanAssemblyRequest(
            GameObject sourceRoot,
            GameObject stormOceanPrefab,
            string outputName,
            string outputAssetFolder,
            bool replaceExistingOutput,
            float waterSurfaceHeight,
            int blendBandCount,
            Color shallowWaterColor,
            Color deepWaterColor,
            string deepWaterSourceName,
            string shallowWaterSourceName,
            string boundarySourceName)
        {
            SourceRoot = sourceRoot;
            StormOceanPrefab = stormOceanPrefab;
            OutputName = outputName;
            OutputAssetFolder = outputAssetFolder;
            ReplaceExistingOutput = replaceExistingOutput;
            WaterSurfaceHeight = waterSurfaceHeight;
            BlendBandCount = Mathf.Clamp(blendBandCount, 0, OceanTerrainAssemblyUtility.MaxBlendBandCount);
            ShallowWaterColor = shallowWaterColor;
            DeepWaterColor = deepWaterColor;
            DeepWaterSourceName = deepWaterSourceName;
            ShallowWaterSourceName = shallowWaterSourceName;
            BoundarySourceName = boundarySourceName;
        }

        public GameObject SourceRoot { get; }
        public GameObject StormOceanPrefab { get; }
        public string OutputName { get; }
        public string OutputAssetFolder { get; }
        public bool ReplaceExistingOutput { get; }
        public float WaterSurfaceHeight { get; }
        public int BlendBandCount { get; }
        public Color ShallowWaterColor { get; }
        public Color DeepWaterColor { get; }
        public string DeepWaterSourceName { get; }
        public string ShallowWaterSourceName { get; }
        public string BoundarySourceName { get; }
    }

    public readonly struct ManualOceanAssemblyResult
    {
        public ManualOceanAssemblyResult(
            GameObject root,
            string assetFolder,
            Mesh[] terrainMeshes,
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
            TerrainMeshes = terrainMeshes ?? Array.Empty<Mesh>();
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
        public Mesh[] TerrainMeshes { get; }
        public Mesh ShallowOceanMesh { get; }
        public Mesh DeepOceanMesh { get; }
        public Mesh[] BlendOceanMeshes { get; }
        public Material OceanTemplateMaterial { get; }
        public Material ShallowOceanMaterial { get; }
        public Material DeepOceanMaterial { get; }
        public Material[] BlendOceanMaterials { get; }
    }

    public static class ManualOceanAssemblyUtility
    {
        public const string DefaultDeepWaterSourceName = "DeepOcean";
        public const string DefaultShallowWaterSourceName = "ShallowOcean";
        public const string DefaultBoundarySourceName = "HubWorldWall";

        public static string BuildDefaultOutputName(string sourceRootName)
        {
            return OceanTerrainAssemblyUtility.BuildDefaultOutputName(sourceRootName);
        }

        public static bool TryValidateRequest(ManualOceanAssemblyRequest request, out string error)
        {
            if (request.SourceRoot == null)
            {
                error = "Assign a manual island source root from the scene or Project window.";
                return false;
            }

            Scene targetScene = ResolveTargetScene(request.SourceRoot);
            if (!targetScene.IsValid())
            {
                error = "Open a target scene before creating a manual ocean assembly.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.OutputName))
            {
                error = "Enter an output name.";
                return false;
            }

            string sanitizedOutputName = OceanAssemblySharedUtility.SanitizeName(request.OutputName, "ManualOceanAssembly");
            if (request.SourceRoot.scene.IsValid()
                && request.SourceRoot.transform.parent == null
                && string.Equals(request.SourceRoot.name, sanitizedOutputName, StringComparison.Ordinal))
            {
                error = "Output Name must differ from the source root name so the source object is not replaced.";
                return false;
            }

            if (!OceanAssemblySharedUtility.IsProjectAssetFolderPath(OceanAssemblySharedUtility.ResolveOutputAssetFolder(request.OutputAssetFolder)))
            {
                error = "Output Asset Folder must be inside the Assets folder.";
                return false;
            }

            if (!request.ReplaceExistingOutput && OceanAssemblySharedUtility.FindSceneRoot(targetScene, sanitizedOutputName) != null)
            {
                error = $"A scene root named '{sanitizedOutputName}' already exists. Enable Replace Existing Output or choose another name.";
                return false;
            }

            if (!TryResolveSourceLayout(
                    request.SourceRoot,
                    request.DeepWaterSourceName,
                    request.ShallowWaterSourceName,
                    request.BoundarySourceName,
                    out ManualOceanSourceLayout layout,
                    out error))
            {
                return false;
            }

            if (!layout.HasWater)
            {
                error = string.Empty;
                return true;
            }

            if (request.StormOceanPrefab == null)
            {
                error = "Assign the StormOcean prefab when the source includes shallow or deep water meshes.";
                return false;
            }

            if (request.StormOceanPrefab.GetComponent<OceanController>() == null)
            {
                error = "The StormOcean prefab must have an OceanController on its root object.";
                return false;
            }

            if (OceanAssemblySharedUtility.ResolveOceanTemplateRenderer(request.StormOceanPrefab) == null)
            {
                error = "The StormOcean prefab must have a MeshRenderer with an ocean material.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public static ManualOceanAssemblyResult CreateAssembly(ManualOceanAssemblyRequest request)
        {
            if (!TryValidateRequest(request, out string validationError))
            {
                throw new InvalidOperationException(validationError);
            }

            using var sourceContext = CreateSourceContext(request.SourceRoot);
            if (!TryResolveSourceLayout(
                    sourceContext.SourceRoot,
                    request.DeepWaterSourceName,
                    request.ShallowWaterSourceName,
                    request.BoundarySourceName,
                    out ManualOceanSourceLayout layout,
                    out string sourceError))
            {
                throw new InvalidOperationException(sourceError);
            }

            string outputName = OceanAssemblySharedUtility.SanitizeName(request.OutputName, "ManualOceanAssembly");
            string assetRoot = OceanAssemblySharedUtility.EnsureOutputAssetFolder(request.OutputAssetFolder, outputName);

            GameObject existingOutput = OceanAssemblySharedUtility.FindSceneRoot(sourceContext.TargetScene, outputName);
            if (existingOutput != null)
            {
                UnityEngine.Object.DestroyImmediate(existingOutput);
            }

            var root = new GameObject(outputName);
            SceneManager.MoveGameObjectToScene(root, sourceContext.TargetScene);
            root.transform.SetPositionAndRotation(
                sourceContext.SourceRoot.transform.position,
                sourceContext.SourceRoot.transform.rotation);
            root.transform.localScale = sourceContext.SourceRoot.transform.lossyScale;

            var terrainGeometry = new GameObject(OceanTerrainAssemblyUtility.TerrainGeometryChildName);
            terrainGeometry.transform.SetParent(root.transform, worldPositionStays: false);

            var terrainMeshes = new List<Mesh>(layout.LandSources.Length + (layout.BoundarySource.HasValue ? 1 : 0));
            CreateTerrainChildren(terrainGeometry.transform, assetRoot, outputName, layout.LandSources, terrainMeshes);
            if (layout.BoundarySource.HasValue)
            {
                CreateTerrainChild(terrainGeometry.transform, assetRoot, outputName, layout.BoundarySource.Value, terrainMeshes);
            }

            OceanAssemblyWaterOutputResult waterOutput = default;
            if (layout.HasWater)
            {
                waterOutput = OceanAssemblySharedUtility.CreateWaterOutput(
                    root.transform,
                    request.SourceRoot,
                    request.StormOceanPrefab,
                    assetRoot,
                    outputName,
                    request.WaterSurfaceHeight,
                    request.BlendBandCount,
                    request.ShallowWaterColor,
                    request.DeepWaterColor,
                    layout.ShallowSource,
                    layout.DeepSource);
            }

            EditorSceneManager.MarkSceneDirty(root.scene);
            AssetDatabase.SaveAssets();

            return new ManualOceanAssemblyResult(
                root,
                assetRoot,
                terrainMeshes.ToArray(),
                waterOutput.ShallowMesh,
                waterOutput.DeepMesh,
                waterOutput.BlendMeshes,
                waterOutput.OceanTemplateMaterial,
                waterOutput.ShallowMaterial,
                waterOutput.DeepMaterial,
                waterOutput.BlendMaterials);
        }

        private static void CreateTerrainChildren(
            Transform terrainParent,
            string assetRoot,
            string outputName,
            OceanAssemblyMeshSource[] sources,
            List<Mesh> createdMeshes)
        {
            for (int i = 0; i < sources.Length; i++)
            {
                CreateTerrainChild(terrainParent, assetRoot, outputName, sources[i], createdMeshes);
            }
        }

        private static void CreateTerrainChild(
            Transform terrainParent,
            string assetRoot,
            string outputName,
            OceanAssemblyMeshSource source,
            List<Mesh> createdMeshes)
        {
            string meshName = $"{outputName}_{source.AssetKey}_TerrainGeometry";
            Mesh terrainMesh = OceanAssemblySharedUtility.CreateOrReplaceMeshAsset(
                source.Mesh,
                $"{assetRoot}/{meshName}.asset",
                meshName);
            OceanAssemblySharedUtility.CreateCopiedMeshChild(
                terrainParent,
                source.GameObject.name,
                source,
                terrainMesh,
                source.Renderer.sharedMaterials,
                includeCollider: true,
                forceWaterLayer: false);
            createdMeshes.Add(terrainMesh);
        }

        private static ManualOceanSourceContext CreateSourceContext(GameObject sourceRoot)
        {
            if (sourceRoot.scene.IsValid())
            {
                return new ManualOceanSourceContext(sourceRoot, sourceRoot.scene, null);
            }

            Scene targetScene = ResolveTargetScene(sourceRoot);
            if (!targetScene.IsValid())
            {
                throw new InvalidOperationException("Open a target scene before creating a manual ocean assembly.");
            }

            var temporaryRoot = PrefabUtility.InstantiatePrefab(sourceRoot, targetScene) as GameObject;
            if (temporaryRoot == null)
            {
                throw new InvalidOperationException("Could not instantiate the selected source asset.");
            }

            temporaryRoot.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor;
            temporaryRoot.name = $"{sourceRoot.name}__ManualOceanAssemblySource";
            return new ManualOceanSourceContext(temporaryRoot, targetScene, temporaryRoot);
        }

        private static Scene ResolveTargetScene(GameObject sourceRoot)
        {
            if (sourceRoot != null && sourceRoot.scene.IsValid())
            {
                return sourceRoot.scene;
            }

            return SceneManager.GetActiveScene();
        }

        private static bool TryResolveSourceLayout(
            GameObject sourceRoot,
            string deepWaterSourceName,
            string shallowWaterSourceName,
            string boundarySourceName,
            out ManualOceanSourceLayout layout,
            out string error)
        {
            layout = default;
            OceanAssemblyMeshSource[] meshSources = CollectMeshSources(sourceRoot);
            if (meshSources.Length == 0)
            {
                error = "The selected source root does not contain any mesh-bearing descendants.";
                return false;
            }

            if (!TryValidateDistinctRoleNames(deepWaterSourceName, shallowWaterSourceName, boundarySourceName, out error))
            {
                return false;
            }

            if (!TryResolveOptionalRole(meshSources, "deep water", deepWaterSourceName, out OceanAssemblyMeshSource? deepSource, out error))
            {
                return false;
            }

            if (!TryResolveOptionalRole(meshSources, "shallow water", shallowWaterSourceName, out OceanAssemblyMeshSource? shallowSource, out error))
            {
                return false;
            }

            if (!TryResolveOptionalRole(meshSources, "boundary", boundarySourceName, out OceanAssemblyMeshSource? boundarySource, out error))
            {
                return false;
            }

            var landSources = new List<OceanAssemblyMeshSource>(meshSources.Length);
            for (int i = 0; i < meshSources.Length; i++)
            {
                OceanAssemblyMeshSource candidate = meshSources[i];
                if ((deepSource.HasValue && candidate.GameObject == deepSource.Value.GameObject)
                    || (shallowSource.HasValue && candidate.GameObject == shallowSource.Value.GameObject)
                    || (boundarySource.HasValue && candidate.GameObject == boundarySource.Value.GameObject))
                {
                    continue;
                }

                landSources.Add(candidate);
            }

            if (landSources.Count == 0)
            {
                error = "No land geometry was found after removing configured water and boundary meshes.";
                return false;
            }

            layout = new ManualOceanSourceLayout(
                landSources.ToArray(),
                boundarySource,
                shallowSource,
                deepSource);
            error = string.Empty;
            return true;
        }

        private static bool TryValidateDistinctRoleNames(
            string deepWaterSourceName,
            string shallowWaterSourceName,
            string boundarySourceName,
            out string error)
        {
            string normalizedDeep = NormalizeRoleName(deepWaterSourceName);
            string normalizedShallow = NormalizeRoleName(shallowWaterSourceName);
            string normalizedBoundary = NormalizeRoleName(boundarySourceName);

            if (!string.IsNullOrEmpty(normalizedDeep)
                && normalizedDeep == normalizedShallow)
            {
                error = "Deep Water Name and Shallow Water Name must not target the same source object.";
                return false;
            }

            if (!string.IsNullOrEmpty(normalizedDeep)
                && normalizedDeep == normalizedBoundary)
            {
                error = "Deep Water Name and Boundary Name must not target the same source object.";
                return false;
            }

            if (!string.IsNullOrEmpty(normalizedShallow)
                && normalizedShallow == normalizedBoundary)
            {
                error = "Shallow Water Name and Boundary Name must not target the same source object.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool TryResolveOptionalRole(
            OceanAssemblyMeshSource[] meshSources,
            string roleLabel,
            string roleName,
            out OceanAssemblyMeshSource? source,
            out string error)
        {
            source = null;
            string normalizedRoleName = NormalizeRoleName(roleName);
            if (string.IsNullOrEmpty(normalizedRoleName))
            {
                error = string.Empty;
                return true;
            }

            int matchCount = 0;
            OceanAssemblyMeshSource matchedSource = default;
            for (int i = 0; i < meshSources.Length; i++)
            {
                if (!string.Equals(meshSources[i].GameObject.name, normalizedRoleName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                matchedSource = meshSources[i];
                matchCount++;
            }

            if (matchCount > 1)
            {
                error = $"The configured {roleLabel} source name '{normalizedRoleName}' matched more than one mesh-bearing object.";
                return false;
            }

            if (matchCount == 1)
            {
                source = matchedSource;
            }

            error = string.Empty;
            return true;
        }

        private static string NormalizeRoleName(string roleName)
        {
            return string.IsNullOrWhiteSpace(roleName) ? string.Empty : roleName.Trim();
        }

        private static OceanAssemblyMeshSource[] CollectMeshSources(GameObject sourceRoot)
        {
            if (sourceRoot == null)
            {
                return Array.Empty<OceanAssemblyMeshSource>();
            }

            MeshFilter[] meshFilters = sourceRoot.GetComponentsInChildren<MeshFilter>(includeInactive: true);
            var meshSources = new List<OceanAssemblyMeshSource>(meshFilters.Length);
            for (int i = 0; i < meshFilters.Length; i++)
            {
                MeshFilter meshFilter = meshFilters[i];
                if (meshFilter == null || meshFilter.sharedMesh == null)
                {
                    continue;
                }

                MeshRenderer renderer = meshFilter.GetComponent<MeshRenderer>();
                if (renderer == null)
                {
                    continue;
                }

                GetRelativeTransform(sourceRoot.transform, meshFilter.transform, out Vector3 localPosition, out Quaternion localRotation, out Vector3 localScale);
                meshSources.Add(new OceanAssemblyMeshSource(
                    meshFilter.gameObject,
                    meshFilter.transform,
                    renderer,
                    meshFilter.sharedMesh,
                    localPosition,
                    localRotation,
                    localScale,
                    BuildAssetKey(sourceRoot.transform, meshFilter.transform)));
            }

            return meshSources.ToArray();
        }

        private static string BuildAssetKey(Transform root, Transform child)
        {
            var segments = new List<string>();
            Transform current = child;
            while (current != null)
            {
                segments.Add($"{OceanAssemblySharedUtility.SanitizeName(current.name, "Node")}_{current.GetSiblingIndex():D2}");
                if (current == root)
                {
                    break;
                }

                current = current.parent;
            }

            segments.Reverse();
            return string.Join("__", segments);
        }

        private static void GetRelativeTransform(
            Transform root,
            Transform child,
            out Vector3 localPosition,
            out Quaternion localRotation,
            out Vector3 localScale)
        {
            Matrix4x4 relativeMatrix = root.worldToLocalMatrix * child.localToWorldMatrix;
            localPosition = relativeMatrix.GetColumn(3);

            Vector3 right = relativeMatrix.GetColumn(0);
            Vector3 up = relativeMatrix.GetColumn(1);
            Vector3 forward = relativeMatrix.GetColumn(2);

            localScale = new Vector3(right.magnitude, up.magnitude, forward.magnitude);
            if (Vector3.Dot(Vector3.Cross(right, up), forward) < 0f)
            {
                localScale.x = -localScale.x;
                right = -right;
            }

            Vector3 normalizedForward = localScale.z > 0.0001f ? forward / Mathf.Abs(localScale.z) : Vector3.forward;
            Vector3 normalizedUp = localScale.y > 0.0001f ? up / Mathf.Abs(localScale.y) : Vector3.up;
            if (normalizedForward.sqrMagnitude < 0.0001f || normalizedUp.sqrMagnitude < 0.0001f)
            {
                localRotation = Quaternion.identity;
                return;
            }

            localRotation = Quaternion.LookRotation(normalizedForward, normalizedUp);
        }
    }

    internal readonly struct ManualOceanSourceLayout
    {
        public ManualOceanSourceLayout(
            OceanAssemblyMeshSource[] landSources,
            OceanAssemblyMeshSource? boundarySource,
            OceanAssemblyMeshSource? shallowSource,
            OceanAssemblyMeshSource? deepSource)
        {
            LandSources = landSources ?? Array.Empty<OceanAssemblyMeshSource>();
            BoundarySource = boundarySource;
            ShallowSource = shallowSource;
            DeepSource = deepSource;
        }

        public OceanAssemblyMeshSource[] LandSources { get; }
        public OceanAssemblyMeshSource? BoundarySource { get; }
        public OceanAssemblyMeshSource? ShallowSource { get; }
        public OceanAssemblyMeshSource? DeepSource { get; }
        public bool HasWater => ShallowSource.HasValue || DeepSource.HasValue;
    }

    internal sealed class ManualOceanSourceContext : IDisposable
    {
        private readonly GameObject _temporaryRoot;

        public ManualOceanSourceContext(GameObject sourceRoot, Scene targetScene, GameObject temporaryRoot)
        {
            SourceRoot = sourceRoot;
            TargetScene = targetScene;
            _temporaryRoot = temporaryRoot;
        }

        public GameObject SourceRoot { get; }
        public Scene TargetScene { get; }

        public void Dispose()
        {
            if (_temporaryRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(_temporaryRoot);
            }
        }
    }
}
#endif
