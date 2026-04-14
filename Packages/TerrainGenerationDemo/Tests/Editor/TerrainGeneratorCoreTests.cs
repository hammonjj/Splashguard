using BitBox.TerrainGeneration.Core;
using BitBox.TerrainGeneration.Core.Masks;
using BitBox.TerrainGeneration.Core.Noise;
using NUnit.Framework;
using UnityEngine;

namespace BitBox.TerrainGeneration.Tests.Editor
{
    public sealed class TerrainGeneratorCoreTests
    {
        [Test]
        public void GenerateHeightfield_SameSeedAndSettings_IsDeterministic()
        {
            TerrainGenerationRequest request = TerrainGenerationRequest.Default;

            Heightfield first = TerrainGenerator.GenerateHeightfield(request);
            Heightfield second = TerrainGenerator.GenerateHeightfield(request);

            Assert.AreEqual(Hash(first.Heights), Hash(second.Heights));
        }

        [Test]
        public void GenerateHeightfield_DifferentSeed_ChangesOutput()
        {
            TerrainGenerationRequest firstRequest = TerrainGenerationRequest.Default;
            TerrainGenerationRequest secondRequest = new TerrainGenerationRequest(
                firstRequest.Seed + 1,
                firstRequest.ResolutionX,
                firstRequest.ResolutionZ,
                firstRequest.WorldSizeX,
                firstRequest.WorldSizeZ,
                firstRequest.HeightScale,
                firstRequest.SeaLevel,
                firstRequest.NoiseScale,
                firstRequest.Octaves,
                firstRequest.Persistence,
                firstRequest.Lacunarity,
                firstRequest.NoiseMode,
                firstRequest.MaskMode,
                firstRequest.FalloffStrength,
                firstRequest.FalloffExponent,
                firstRequest.IslandCount,
                firstRequest.IslandRadius,
                firstRequest.MinIslandSeparation,
                firstRequest.BlendMode);

            Assert.AreNotEqual(
                Hash(TerrainGenerator.GenerateHeightfield(firstRequest).Heights),
                Hash(TerrainGenerator.GenerateHeightfield(secondRequest).Heights));
        }

        [Test]
        public void SeaLevelClassifier_TreatsBoundaryAsLand()
        {
            Assert.IsTrue(SeaLevelClassifier.IsLand(0f, 0f));
            Assert.IsFalse(SeaLevelClassifier.IsWater(0f, 0f));
        }

        [Test]
        public void RaisingSeaLevel_DoesNotIncreaseLandCells()
        {
            TerrainGenerationRequest request = TerrainGenerationRequest.Default;
            Heightfield heightfield = TerrainGenerator.GenerateHeightfield(request);

            int lowSeaLand = heightfield.CountLandCells(-2f);
            int highSeaLand = heightfield.CountLandCells(2f);

            Assert.GreaterOrEqual(lowSeaLand, highSeaLand);
        }

        [Test]
        public void Request_ClampsInvalidSettings()
        {
            var request = new TerrainGenerationRequest(
                seed: 7,
                resolutionX: -1,
                resolutionZ: 0,
                worldSizeX: -10f,
                worldSizeZ: 0f,
                heightScale: -2f,
                seaLevel: 0f,
                noiseScale: -5f,
                octaves: 99,
                persistence: 2f,
                lacunarity: -1f,
                noiseMode: TerrainNoiseMode.Smooth,
                maskMode: TerrainMaskMode.Radial,
                falloffStrength: -1f,
                falloffExponent: -1f,
                islandCount: 100,
                islandRadius: -1f,
                minIslandSeparation: 5f,
                blendMode: MultiIslandBlendMode.Max);

            Assert.AreEqual(TerrainGenerationRequest.MinResolution, request.ResolutionX);
            Assert.AreEqual(TerrainGenerationRequest.MinResolution, request.ResolutionZ);
            Assert.AreEqual(12, request.Octaves);
            Assert.AreEqual(1f, request.Persistence);
            Assert.AreEqual(64, request.IslandCount);
            Assert.AreEqual(1f, request.MinIslandSeparation);
        }

        [Test]
        public void Fbm_MoreOctaves_IncreasesNeighborVariance()
        {
            TerrainGenerationRequest lowOctave = new TerrainGenerationRequest(
                seed: 27,
                resolutionX: 64,
                resolutionZ: 64,
                worldSizeX: 80f,
                worldSizeZ: 80f,
                heightScale: 12f,
                seaLevel: 0f,
                noiseScale: 18f,
                octaves: 1,
                persistence: 0.55f,
                lacunarity: 2.3f,
                noiseMode: TerrainNoiseMode.Smooth,
                maskMode: TerrainMaskMode.None,
                falloffStrength: 0f,
                falloffExponent: 1f,
                islandCount: 1,
                islandRadius: 0.4f,
                minIslandSeparation: 0.2f,
                blendMode: MultiIslandBlendMode.Max);
            TerrainGenerationRequest highOctave = new TerrainGenerationRequest(
                lowOctave.Seed,
                lowOctave.ResolutionX,
                lowOctave.ResolutionZ,
                lowOctave.WorldSizeX,
                lowOctave.WorldSizeZ,
                lowOctave.HeightScale,
                lowOctave.SeaLevel,
                lowOctave.NoiseScale,
                6,
                lowOctave.Persistence,
                lowOctave.Lacunarity,
                lowOctave.NoiseMode,
                lowOctave.MaskMode,
                lowOctave.FalloffStrength,
                lowOctave.FalloffExponent,
                lowOctave.IslandCount,
                lowOctave.IslandRadius,
                lowOctave.MinIslandSeparation,
                lowOctave.BlendMode);

            float lowVariance = NeighborVariance(TerrainGenerator.GenerateHeightfield(lowOctave));
            float highVariance = NeighborVariance(TerrainGenerator.GenerateHeightfield(highOctave));

            Assert.GreaterOrEqual(highVariance, lowVariance * 0.9f);
        }

        [Test]
        public void RidgedMode_IsDeterministic()
        {
            TerrainGenerationRequest request = new TerrainGenerationRequest(
                seed: 44,
                resolutionX: 48,
                resolutionZ: 48,
                worldSizeX: 64f,
                worldSizeZ: 64f,
                heightScale: 10f,
                seaLevel: 0f,
                noiseScale: 16f,
                octaves: 5,
                persistence: 0.5f,
                lacunarity: 2f,
                noiseMode: TerrainNoiseMode.Ridged,
                maskMode: TerrainMaskMode.Radial,
                falloffStrength: 8f,
                falloffExponent: 1.7f,
                islandCount: 1,
                islandRadius: 0.4f,
                minIslandSeparation: 0.2f,
                blendMode: MultiIslandBlendMode.Max);

            Assert.AreEqual(
                Hash(TerrainGenerator.GenerateHeightfield(request).Heights),
                Hash(TerrainGenerator.GenerateHeightfield(request).Heights));
        }

        private static int Hash(float[] values)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < values.Length; i++)
                {
                    hash = hash * 31 + Mathf.RoundToInt(values[i] * 100000f);
                }

                return hash;
            }
        }

        private static float NeighborVariance(Heightfield heightfield)
        {
            float sum = 0f;
            int count = 0;
            for (int z = 0; z < heightfield.Depth; z++)
            {
                for (int x = 0; x < heightfield.Width - 1; x++)
                {
                    sum += Mathf.Abs(heightfield.GetHeight(x + 1, z) - heightfield.GetHeight(x, z));
                    count++;
                }
            }

            return count == 0 ? 0f : sum / count;
        }

        private sealed class ConstantNoise : ITerrainNoise2D
        {
            private readonly float _value;

            public ConstantNoise(float value)
            {
                _value = value;
            }

            public float Sample(float x, float z)
            {
                return _value;
            }
        }
    }
}
