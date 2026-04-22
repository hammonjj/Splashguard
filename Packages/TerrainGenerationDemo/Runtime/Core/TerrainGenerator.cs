using BitBox.TerrainGeneration.Core.Masks;
using BitBox.TerrainGeneration.Core.Noise;

namespace BitBox.TerrainGeneration.Core
{
    public static class TerrainGenerator
    {
        public static Heightfield GenerateHeightfield(TerrainGenerationRequest request)
        {
            var baseNoise = new UnityPerlinNoise2D(request.Seed);
            var noise = new FbmNoise2D(
                baseNoise,
                request.Octaves,
                request.Persistence,
                request.Lacunarity,
                request.NoiseMode);
            return GenerateHeightfield(request, noise, BuildMask(request));
        }

        public static Heightfield GenerateHeightfield(
            TerrainGenerationRequest request,
            ITerrainNoise2D noise,
            IMask2D mask)
        {
            int width = request.ResolutionX;
            int depth = request.ResolutionZ;
            var heights = new float[width * depth];

            for (int z = 0; z < depth; z++)
            {
                float v = depth <= 1 ? 0f : z / (float)(depth - 1);
                for (int x = 0; x < width; x++)
                {
                    float u = width <= 1 ? 0f : x / (float)(width - 1);
                    float sampleX = (u * request.WorldSizeX) / request.NoiseScale;
                    float sampleZ = (v * request.WorldSizeZ) / request.NoiseScale;
                    float noiseValue = noise.Sample(sampleX, sampleZ);
                    float baseHeight = (noiseValue - 0.35f) * request.HeightScale;
                    float falloff = mask?.Evaluate(u, v) ?? 0f;
                    heights[z * width + x] = baseHeight - falloff * request.FalloffStrength;
                }
            }

            ApplyUnderwaterProfile(request, heights);
            return new Heightfield(width, depth, heights, request.SeaLevel);
        }

        public static IMask2D BuildMask(TerrainGenerationRequest request)
        {
            switch (request.MaskMode)
            {
                case TerrainMaskMode.Radial:
                    return new RadialFalloffMask(request.FalloffExponent);
                case TerrainMaskMode.DistanceToEdge:
                    return new DistanceToEdgeFalloffMask(request.FalloffExponent);
                case TerrainMaskMode.Archipelago:
                    return new MultiIslandMask(
                        IslandCenterGenerator.Generate(request.Seed, request.IslandCount, request.MinIslandSeparation),
                        request.IslandRadius,
                        request.FalloffExponent,
                        request.BlendMode);
                case TerrainMaskMode.RoundedBasin:
                    return new RoundedBasinMask(
                        request.BasinWidth,
                        request.BasinDepth,
                        request.BasinCornerRadius,
                        request.BasinEdgeSoftness,
                        request.FalloffExponent);
                default:
                    return new NoFalloffMask();
            }
        }

        private static void ApplyUnderwaterProfile(TerrainGenerationRequest request, float[] heights)
        {
            if (request.UnderwaterProfile != TerrainUnderwaterProfile.FlatFloor)
            {
                return;
            }

            float floorHeight = request.SeaLevel - request.FlatFloorDepth;
            for (int i = 0; i < heights.Length; i++)
            {
                if (heights[i] < request.SeaLevel)
                {
                    heights[i] = floorHeight;
                }
            }
        }
    }
}
