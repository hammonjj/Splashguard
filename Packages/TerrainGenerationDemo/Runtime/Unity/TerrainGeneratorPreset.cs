using BitBox.TerrainGeneration.Core;
using UnityEngine;

namespace BitBox.TerrainGeneration.Unity
{
    [CreateAssetMenu(
        fileName = "TerrainGeneratorPreset",
        menuName = "Terrain Generation/Terrain Generator Preset")]
    public sealed class TerrainGeneratorPreset : ScriptableObject
    {
        [Header("Grid")]
        [SerializeField] private int _seed = 12345;
        [SerializeField, Min(2)] private int _resolutionX = 129;
        [SerializeField, Min(2)] private int _resolutionZ = 129;
        [SerializeField, Min(0.01f)] private float _worldSizeX = 96f;
        [SerializeField, Min(0.01f)] private float _worldSizeZ = 96f;

        [Header("Height")]
        [SerializeField, Min(0f)] private float _heightScale = 14f;
        [SerializeField] private float _seaLevel = 0f;

        [Header("Noise")]
        [SerializeField, Min(0.001f)] private float _noiseScale = 22f;
        [SerializeField, Range(1, 12)] private int _octaves = 5;
        [SerializeField, Range(0f, 1f)] private float _persistence = 0.5f;
        [SerializeField, Min(1f)] private float _lacunarity = 2f;
        [SerializeField] private TerrainNoiseMode _noiseMode = TerrainNoiseMode.Smooth;

        [Header("Island Shape")]
        [SerializeField] private TerrainMaskMode _maskMode = TerrainMaskMode.Radial;
        [SerializeField, Min(0f)] private float _falloffStrength = 9f;
        [SerializeField, Min(0.01f)] private float _falloffExponent = 1.8f;

        [Header("Archipelago")]
        [SerializeField, Range(1, 64)] private int _islandCount = 1;
        [SerializeField, Range(0.01f, 2f)] private float _islandRadius = 0.42f;
        [SerializeField, Range(0f, 1f)] private float _minIslandSeparation = 0.22f;
        [SerializeField] private MultiIslandBlendMode _blendMode = MultiIslandBlendMode.SmoothUnion;

        [Header("Scene Output")]
        [SerializeField] private bool _updateMeshCollider;

        public int Seed => _seed;
        public bool UpdateMeshCollider => _updateMeshCollider;

        public TerrainGenerationRequest ToRequest()
        {
            return new TerrainGenerationRequest(
                _seed,
                _resolutionX,
                _resolutionZ,
                _worldSizeX,
                _worldSizeZ,
                _heightScale,
                _seaLevel,
                _noiseScale,
                _octaves,
                _persistence,
                _lacunarity,
                _noiseMode,
                _maskMode,
                _falloffStrength,
                _falloffExponent,
                _islandCount,
                _islandRadius,
                _minIslandSeparation,
                _blendMode);
        }

        public void RandomizeSeed()
        {
            _seed = unchecked((System.Environment.TickCount * 397) ^ GetInstanceID());
        }
    }
}
