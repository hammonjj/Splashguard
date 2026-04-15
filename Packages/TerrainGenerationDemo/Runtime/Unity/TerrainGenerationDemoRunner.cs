using System;
using BitBox.TerrainGeneration.Core;
using UnityEngine;

namespace BitBox.TerrainGeneration.Unity
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class TerrainGenerationDemoRunner : MonoBehaviour
    {
        [SerializeField] private TerrainGeneratorPreset _preset;
        [SerializeField] private Material _terrainMaterial;
        [SerializeField] private TerrainPropLibrary _propLibrary;
        [SerializeField] private bool _generateProps = true;
        [SerializeField] private bool _generateOnEnable = true;
        [SerializeField] private bool _updateMeshCollider;
        [SerializeField, Range(0, 8), Tooltip("Fallback zone color smoothing pass count used when no preset is assigned.")]
        private int _zoneColorSmoothingPasses = 2;
        [SerializeField] private string _generatedObjectName = "Generated Island Terrain";

        public Heightfield LastHeightfield { get; private set; }
        public TerrainZoneMap LastZoneMap { get; private set; }
        public MeshArrays LastMeshArrays { get; private set; }
        public LayeredTerrainMeshes LastLayeredMeshes { get; private set; }
        public TerrainPropPlacement[] LastPropPlacements { get; private set; } = Array.Empty<TerrainPropPlacement>();

        public TerrainGeneratorPreset Preset
        {
            get => _preset;
            set => _preset = value;
        }

        public GameObject Generate()
        {
            TerrainGenerationRequest request = GetCurrentRequest();
            LastHeightfield = TerrainGenerator.GenerateHeightfield(request);
            TerrainZoneSettings zoneSettings = _preset != null
                ? _preset.ToZoneSettings()
                : TerrainZoneSettings.Default;
            LastZoneMap = TerrainZoneClassifier.GenerateZoneMap(
                LastHeightfield,
                zoneSettings,
                request.WorldSizeX,
                request.WorldSizeZ);

            int smoothingPasses = _preset != null
                ? _preset.ZoneColorSmoothingPasses
                : _zoneColorSmoothingPasses;
            TerrainZoneColorPalette colorPalette = _preset != null
                ? _preset.ToZoneColorPalette()
                : TerrainZoneColorPalette.Default;
            LastLayeredMeshes = LayeredTerrainMeshBuilder.Build(
                LastHeightfield,
                LastZoneMap,
                request.WorldSizeX,
                request.WorldSizeZ,
                colorPalette,
                smoothingPasses);
            LastMeshArrays = LastLayeredMeshes.Land;

            GameObject generatedObject = ResolveGeneratedObject();
            GameObject landObject = ResolveLayerObject(generatedObject.transform, "Real Terrain", includeCollider: true);
            GameObject shallowWaterObject = ResolveLayerObject(generatedObject.transform, "Shallow Water", includeCollider: false);
            GameObject deepWaterObject = ResolveLayerObject(generatedObject.transform, "Deep Water", includeCollider: false);
            bool updateCollider = _preset != null ? _preset.UpdateMeshCollider : _updateMeshCollider;

            UnityMeshApplier.ApplyTo(
                landObject.GetComponent<MeshFilter>(),
                landObject.GetComponent<MeshCollider>(),
                LastLayeredMeshes.Land,
                updateCollider);
            UnityMeshApplier.ApplyTo(
                shallowWaterObject.GetComponent<MeshFilter>(),
                null,
                LastLayeredMeshes.ShallowWater,
                updateCollider: false);
            UnityMeshApplier.ApplyTo(
                deepWaterObject.GetComponent<MeshFilter>(),
                null,
                LastLayeredMeshes.DeepWater,
                updateCollider: false);

            AssignLayerMaterial(landObject);
            AssignLayerMaterial(shallowWaterObject);
            AssignLayerMaterial(deepWaterObject);
            RegenerateProps(generatedObject, request);

            return generatedObject;
        }

        public TerrainGenerationRequest GetCurrentRequest()
        {
            return _preset != null ? _preset.ToRequest() : TerrainGenerationRequest.Default;
        }

        private void OnEnable()
        {
            if (_generateOnEnable && gameObject.scene.IsValid())
            {
                Generate();
            }
        }

        private void OnValidate()
        {
            if (!isActiveAndEnabled || !gameObject.scene.IsValid())
            {
                return;
            }

            if (_generateOnEnable)
            {
                Generate();
            }
        }

        private GameObject ResolveGeneratedObject()
        {
            string objectName = string.IsNullOrWhiteSpace(_generatedObjectName)
                ? "Generated Island Terrain"
                : _generatedObjectName;

            Transform existing = transform.Find(objectName);
            GameObject generatedObject;
            if (existing != null)
            {
                generatedObject = existing.gameObject;
            }
            else
            {
                generatedObject = new GameObject(objectName);
                generatedObject.transform.SetParent(transform, worldPositionStays: false);
            }

            RemoveContainerMeshComponents(generatedObject);

            return generatedObject;
        }

        private static GameObject ResolveLayerObject(Transform parent, string objectName, bool includeCollider)
        {
            Transform existing = parent.Find(objectName);
            GameObject layerObject;
            if (existing != null)
            {
                layerObject = existing.gameObject;
            }
            else
            {
                layerObject = new GameObject(objectName);
                layerObject.transform.SetParent(parent, worldPositionStays: false);
            }

            if (layerObject.GetComponent<MeshFilter>() == null)
            {
                layerObject.AddComponent<MeshFilter>();
            }

            if (layerObject.GetComponent<MeshRenderer>() == null)
            {
                layerObject.AddComponent<MeshRenderer>();
            }

            MeshCollider meshCollider = layerObject.GetComponent<MeshCollider>();
            if (includeCollider)
            {
                if (meshCollider == null)
                {
                    layerObject.AddComponent<MeshCollider>();
                }
            }
            else if (meshCollider != null)
            {
                DestroyUnityObject(meshCollider);
            }

            return layerObject;
        }

        private void AssignLayerMaterial(GameObject layerObject)
        {
            var meshRenderer = layerObject.GetComponent<MeshRenderer>();
            if (meshRenderer.sharedMaterial == null || _terrainMaterial != null)
            {
                meshRenderer.sharedMaterial = _terrainMaterial != null ? _terrainMaterial : CreateFallbackMaterial();
            }
        }

        private static void RemoveContainerMeshComponents(GameObject generatedObject)
        {
            MeshCollider meshCollider = generatedObject.GetComponent<MeshCollider>();
            if (meshCollider != null)
            {
                DestroyUnityObject(meshCollider);
            }

            MeshRenderer meshRenderer = generatedObject.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                DestroyUnityObject(meshRenderer);
            }

            MeshFilter meshFilter = generatedObject.GetComponent<MeshFilter>();
            if (meshFilter != null)
            {
                DestroyUnityObject(meshFilter);
            }
        }

        private void RegenerateProps(GameObject generatedObject, TerrainGenerationRequest request)
        {
            Transform propsRoot = ResolvePropsRoot(generatedObject.transform);
            ClearChildren(propsRoot);

            bool shouldGenerateProps = _preset != null ? _preset.GenerateProps : _generateProps;
            TerrainPropLibrary library = _preset != null && _preset.PropLibrary != null ? _preset.PropLibrary : _propLibrary;
            if (!shouldGenerateProps || library == null || LastZoneMap == null)
            {
                LastPropPlacements = Array.Empty<TerrainPropPlacement>();
                return;
            }

            TerrainPropPlacementSettings propSettings = _preset != null
                ? _preset.ToPropPlacementSettings()
                : TerrainPropPlacementSettings.Default;
            LastPropPlacements = TerrainPropPlacer.GeneratePlacements(
                LastHeightfield,
                LastZoneMap,
                request,
                propSettings);

            for (int i = 0; i < LastPropPlacements.Length; i++)
            {
                TerrainPropPlacement placement = LastPropPlacements[i];
                if (!library.TryGetPrefab(placement.Type, out GameObject prefab))
                {
                    continue;
                }

                GameObject prop = Instantiate(prefab, propsRoot);
                prop.name = $"{placement.Type} {i + 1:000}";
                prop.transform.localPosition = placement.Position;
                prop.transform.localRotation = Quaternion.Euler(0f, placement.YawDegrees, 0f);
                prop.transform.localScale = Vector3.one * placement.Scale;

                TerrainPlaceholderProp placeholder = prop.GetComponent<TerrainPlaceholderProp>();
                if (placeholder != null)
                {
                    placeholder.EnsureVisuals();
                }
            }
        }

        private static Transform ResolvePropsRoot(Transform generatedObjectTransform)
        {
            Transform existing = generatedObjectTransform.Find("Props");
            if (existing != null)
            {
                return existing;
            }

            var propsRoot = new GameObject("Props");
            propsRoot.transform.SetParent(generatedObjectTransform, worldPositionStays: false);
            return propsRoot.transform;
        }

        private static void ClearChildren(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                DestroyUnityObject(root.GetChild(i).gameObject);
            }
        }

        private static void DestroyUnityObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private static Material CreateFallbackMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            var material = new Material(shader)
            {
                name = "Generated Terrain Material",
                hideFlags = HideFlags.DontSave
            };
            material.color = new Color(0.22f, 0.56f, 0.26f, 1f);
            return material;
        }
    }
}
