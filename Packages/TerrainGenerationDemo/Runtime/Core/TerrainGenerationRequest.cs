using UnityEngine;

namespace BitBox.TerrainGeneration.Core
{
    public readonly struct TerrainGenerationRequest
    {
        public const int MinResolution = 2;
        public const int MaxReasonableResolution = 4097;

        public readonly int Seed;
        public readonly int ResolutionX;
        public readonly int ResolutionZ;
        public readonly float WorldSizeX;
        public readonly float WorldSizeZ;
        public readonly float HeightScale;
        public readonly float SeaLevel;
        public readonly float NoiseScale;
        public readonly int Octaves;
        public readonly float Persistence;
        public readonly float Lacunarity;
        public readonly TerrainNoiseMode NoiseMode;
        public readonly TerrainMaskMode MaskMode;
        public readonly float FalloffStrength;
        public readonly float FalloffExponent;
        public readonly int IslandCount;
        public readonly float IslandRadius;
        public readonly float MinIslandSeparation;
        public readonly MultiIslandBlendMode BlendMode;

        public TerrainGenerationRequest(
            int seed,
            int resolutionX,
            int resolutionZ,
            float worldSizeX,
            float worldSizeZ,
            float heightScale,
            float seaLevel,
            float noiseScale,
            int octaves,
            float persistence,
            float lacunarity,
            TerrainNoiseMode noiseMode,
            TerrainMaskMode maskMode,
            float falloffStrength,
            float falloffExponent,
            int islandCount,
            float islandRadius,
            float minIslandSeparation,
            MultiIslandBlendMode blendMode)
        {
            Seed = seed;
            ResolutionX = Mathf.Clamp(resolutionX, MinResolution, MaxReasonableResolution);
            ResolutionZ = Mathf.Clamp(resolutionZ, MinResolution, MaxReasonableResolution);
            WorldSizeX = Mathf.Max(0.01f, worldSizeX);
            WorldSizeZ = Mathf.Max(0.01f, worldSizeZ);
            HeightScale = Mathf.Max(0f, heightScale);
            SeaLevel = seaLevel;
            NoiseScale = Mathf.Max(0.001f, noiseScale);
            Octaves = Mathf.Clamp(octaves, 1, 12);
            Persistence = Mathf.Clamp01(persistence);
            Lacunarity = Mathf.Max(1f, lacunarity);
            NoiseMode = noiseMode;
            MaskMode = maskMode;
            FalloffStrength = Mathf.Max(0f, falloffStrength);
            FalloffExponent = Mathf.Max(0.01f, falloffExponent);
            IslandCount = Mathf.Clamp(islandCount, 1, 64);
            IslandRadius = Mathf.Clamp(islandRadius, 0.01f, 2f);
            MinIslandSeparation = Mathf.Clamp01(minIslandSeparation);
            BlendMode = blendMode;
        }

        public static TerrainGenerationRequest Default => new(
            seed: 12345,
            resolutionX: 129,
            resolutionZ: 129,
            worldSizeX: 96f,
            worldSizeZ: 96f,
            heightScale: 14f,
            seaLevel: 0f,
            noiseScale: 22f,
            octaves: 5,
            persistence: 0.5f,
            lacunarity: 2f,
            noiseMode: TerrainNoiseMode.Smooth,
            maskMode: TerrainMaskMode.Radial,
            falloffStrength: 9f,
            falloffExponent: 1.8f,
            islandCount: 1,
            islandRadius: 0.42f,
            minIslandSeparation: 0.22f,
            blendMode: MultiIslandBlendMode.SmoothUnion);
    }
}
