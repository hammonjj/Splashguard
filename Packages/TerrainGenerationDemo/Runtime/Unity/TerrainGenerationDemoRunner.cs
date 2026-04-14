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
        [SerializeField] private bool _generateOnEnable = true;
        [SerializeField] private bool _updateMeshCollider;
        [SerializeField] private string _generatedObjectName = "Generated Island Terrain";

        public Heightfield LastHeightfield { get; private set; }
        public MeshArrays LastMeshArrays { get; private set; }

        public TerrainGeneratorPreset Preset
        {
            get => _preset;
            set => _preset = value;
        }

        public GameObject Generate()
        {
            TerrainGenerationRequest request = GetCurrentRequest();
            LastHeightfield = TerrainGenerator.GenerateHeightfield(request);
            LastMeshArrays = TerrainMeshBuilder.Build(
                LastHeightfield,
                request.WorldSizeX,
                request.WorldSizeZ,
                includeClassificationColors: true);

            GameObject generatedObject = ResolveGeneratedObject();
            var meshFilter = generatedObject.GetComponent<MeshFilter>();
            var meshCollider = generatedObject.GetComponent<MeshCollider>();
            bool updateCollider = _preset != null ? _preset.UpdateMeshCollider : _updateMeshCollider;
            UnityMeshApplier.ApplyTo(meshFilter, meshCollider, LastMeshArrays, updateCollider);

            var meshRenderer = generatedObject.GetComponent<MeshRenderer>();
            if (meshRenderer.sharedMaterial == null || _terrainMaterial != null)
            {
                meshRenderer.sharedMaterial = _terrainMaterial != null ? _terrainMaterial : CreateFallbackMaterial();
            }

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

            if (generatedObject.GetComponent<MeshFilter>() == null)
            {
                generatedObject.AddComponent<MeshFilter>();
            }

            if (generatedObject.GetComponent<MeshRenderer>() == null)
            {
                generatedObject.AddComponent<MeshRenderer>();
            }

            if (generatedObject.GetComponent<MeshCollider>() == null)
            {
                generatedObject.AddComponent<MeshCollider>();
            }

            return generatedObject;
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
