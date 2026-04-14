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
        [SerializeField, Tooltip("Deterministic value used to offset noise and place archipelago island centers. The same seed and settings reproduce the same terrain.")]
        private int _seed = 12345;

        [SerializeField, Min(2), Tooltip("Number of height samples and mesh vertices across the local X axis. Higher values add detail and cost more memory.")]
        private int _resolutionX = 129;

        [SerializeField, Min(2), Tooltip("Number of height samples and mesh vertices across the local Z axis. Higher values add detail and cost more memory.")]
        private int _resolutionZ = 129;

        [SerializeField, Min(0.01f), Tooltip("Generated terrain width in Unity world units.")]
        private float _worldSizeX = 96f;

        [SerializeField, Min(0.01f), Tooltip("Generated terrain depth in Unity world units.")]
        private float _worldSizeZ = 96f;

        [Header("Height")]
        [SerializeField, Min(0f), Tooltip("Vertical multiplier applied to the generated noise before island falloff is subtracted.")]
        private float _heightScale = 14f;

        [SerializeField, Tooltip("World-space height used to classify land and water. Heights greater than or equal to this value are land.")]
        private float _seaLevel = 0f;

        [Header("Noise")]
        [SerializeField, Min(0.001f), Tooltip("Horizontal size of base noise features. Larger values create broader hills and slower terrain changes.")]
        private float _noiseScale = 22f;

        [SerializeField, Range(1, 12), Tooltip("Number of fBm noise layers. More octaves add finer terrain detail.")]
        private int _octaves = 5;

        [SerializeField, Range(0f, 1f), Tooltip("How much amplitude remains for each successive octave. Higher values make fine detail stronger.")]
        private float _persistence = 0.5f;

        [SerializeField, Min(1f), Tooltip("Frequency multiplier between octaves. Higher values make each octave add tighter detail.")]
        private float _lacunarity = 2f;

        [SerializeField, Tooltip("Noise shaping mode. Smooth creates rounded hills; Ridged transforms the noise into sharper ridge lines.")]
        private TerrainNoiseMode _noiseMode = TerrainNoiseMode.Smooth;

        [Header("Island Shape")]
        [SerializeField, Tooltip("Falloff mask used to push terrain down near island edges. Archipelago uses multiple island centers.")]
        private TerrainMaskMode _maskMode = TerrainMaskMode.Radial;

        [SerializeField, Min(0f), Tooltip("Amount subtracted from terrain by the selected island falloff mask. Higher values sink edges and channels more aggressively.")]
        private float _falloffStrength = 9f;

        [SerializeField, Min(0.01f), Tooltip("Curve applied to the falloff mask. Higher values keep more of the center high while concentrating falloff near edges.")]
        private float _falloffExponent = 1.8f;

        [Header("Archipelago")]
        [SerializeField, Range(1, 64), Tooltip("Number of island centers generated when Mask Mode is Archipelago.")]
        private int _islandCount = 1;

        [SerializeField, Range(0.01f, 2f), Tooltip("Normalized radius of each island in archipelago mode. Larger values make islands wider and more likely to merge.")]
        private float _islandRadius = 0.42f;

        [SerializeField, Range(0f, 1f), Tooltip("Minimum normalized spacing attempted between archipelago island centers.")]
        private float _minIslandSeparation = 0.22f;

        [SerializeField, Tooltip("How overlapping archipelago island masks combine. Max separates islands, Sum Clamp merges strongly, Smooth Union blends more naturally.")]
        private MultiIslandBlendMode _blendMode = MultiIslandBlendMode.SmoothUnion;

        [Header("Terrain Zones")]
        [SerializeField, Min(0f), Tooltip("Depth below sea level that still counts as shallow water before becoming deep water.")]
        private float _shallowWaterDepth = 2.2f;

        [SerializeField, Min(0f), Tooltip("Height band above sea level classified as beach.")]
        private float _beachHeightBand = 1.2f;

        [SerializeField, Min(0f), Tooltip("Slope threshold where non-beach land becomes rock. This is a rise-over-run value, not degrees.")]
        private float _rockSlopeThreshold = 0.42f;

        [SerializeField, Range(0f, 1f), Tooltip("Normalized land elevation threshold where terrain becomes mountain.")]
        private float _mountainElevationThreshold = 0.72f;

        [SerializeField, Range(0, 8), Tooltip("Number of color smoothing passes applied to rendered terrain zones. Higher values soften zone transitions without changing placement rules.")]
        private int _zoneColorSmoothingPasses = 2;

        [Header("Props")]
        [SerializeField, Tooltip("When enabled, the demo runner instantiates deterministic placeholder props from the prop library.")]
        private bool _generateProps = true;

        [SerializeField, Tooltip("Prefab library used when placing deterministic island props.")]
        private TerrainPropLibrary _propLibrary;

        [SerializeField, Min(0f), Tooltip("Tree placement density per 1000 square world units on grassland.")]
        private float _treeDensity = 9f;

        [SerializeField, Min(0f), Tooltip("Rock placement density per 1000 square world units on rock and mountain zones.")]
        private float _rockDensity = 10f;

        [SerializeField, Min(0f), Tooltip("Grass patch placement density per 1000 square world units on grassland.")]
        private float _grassPatchDensity = 20f;

        [SerializeField, Min(0f), Tooltip("Driftwood placement density per 1000 square world units on beach.")]
        private float _driftwoodDensity = 3f;

        [SerializeField, Min(0f), Tooltip("Maximum slope allowed for tree placement.")]
        private float _treeMaxSlope = 0.32f;

        [SerializeField, Min(0f), Tooltip("Maximum slope allowed for grass patch placement.")]
        private float _grassPatchMaxSlope = 0.38f;

        [SerializeField, Min(0f), Tooltip("Maximum slope allowed for driftwood placement.")]
        private float _driftwoodMaxSlope = 0.20f;

        [SerializeField, Min(0f), Tooltip("Minimum spacing between generated trees of the same type.")]
        private float _treeMinSpacing = 4.5f;

        [SerializeField, Min(0f), Tooltip("Minimum spacing between generated rocks of the same type.")]
        private float _rockMinSpacing = 3.5f;

        [SerializeField, Min(0f), Tooltip("Minimum spacing between generated grass patches of the same type.")]
        private float _grassPatchMinSpacing = 2.25f;

        [SerializeField, Min(0f), Tooltip("Minimum spacing between generated driftwood props of the same type.")]
        private float _driftwoodMinSpacing = 5f;

        [SerializeField, Min(0.01f), Tooltip("Minimum random scale applied to generated props.")]
        private float _propMinScale = 0.75f;

        [SerializeField, Min(0.01f), Tooltip("Maximum random scale applied to generated props.")]
        private float _propMaxScale = 1.35f;

        [Header("Scene Output")]
        [SerializeField, Tooltip("When enabled, regeneration also rebuilds the MeshCollider. Leave disabled while tuning values unless physics collisions are needed.")]
        private bool _updateMeshCollider;

        public int Seed => _seed;
        public bool GenerateProps => _generateProps;
        public TerrainPropLibrary PropLibrary => _propLibrary;
        public int ZoneColorSmoothingPasses => _zoneColorSmoothingPasses;
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

        public TerrainZoneSettings ToZoneSettings()
        {
            return new TerrainZoneSettings(
                _shallowWaterDepth,
                _beachHeightBand,
                _rockSlopeThreshold,
                _mountainElevationThreshold);
        }

        public TerrainPropPlacementSettings ToPropPlacementSettings()
        {
            return new TerrainPropPlacementSettings(
                _treeDensity,
                _rockDensity,
                _grassPatchDensity,
                _driftwoodDensity,
                _treeMaxSlope,
                _grassPatchMaxSlope,
                _driftwoodMaxSlope,
                _treeMinSpacing,
                _rockMinSpacing,
                _grassPatchMinSpacing,
                _driftwoodMinSpacing,
                _propMinScale,
                _propMaxScale);
        }

        public void RandomizeSeed()
        {
            _seed = unchecked((System.Environment.TickCount * 397) ^ GetInstanceID());
        }
    }
}
