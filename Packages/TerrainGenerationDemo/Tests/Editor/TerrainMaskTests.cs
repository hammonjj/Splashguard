using BitBox.TerrainGeneration.Core;
using BitBox.TerrainGeneration.Core.Masks;
using BitBox.TerrainGeneration.Core.Noise;
using NUnit.Framework;
using UnityEngine;

namespace BitBox.TerrainGeneration.Tests.Editor
{
    public sealed class TerrainMaskTests
    {
        [Test]
        public void RadialMask_IncreasesAwayFromCenter()
        {
            var mask = new RadialFalloffMask(1f);

            Assert.Less(mask.Evaluate(0.5f, 0.5f), mask.Evaluate(0.5f, 0f));
            Assert.Less(mask.Evaluate(0.5f, 0f), mask.Evaluate(0f, 0f));
        }

        [Test]
        public void DistanceToEdgeMask_IsStrongestAtEdges()
        {
            var mask = new DistanceToEdgeFalloffMask(1f);

            Assert.AreEqual(1f, mask.Evaluate(0f, 0.5f), 0.0001f);
            Assert.AreEqual(0f, mask.Evaluate(0.5f, 0.5f), 0.0001f);
        }

        [Test]
        public void RoundedBasinMask_ProducesSmoothDeterministicRoundedFootprint()
        {
            var mask = new RoundedBasinMask(
                width: 0.6f,
                depth: 0.4f,
                cornerRadius: 0.1f,
                edgeSoftness: 0.03f,
                exponent: 1f);

            float center = mask.Evaluate(0.5f, 0.5f);
            float innerEdge = mask.Evaluate(0.77f, 0.5f);
            float boundary = mask.Evaluate(0.8f, 0.5f);
            float outerEdge = mask.Evaluate(0.83f, 0.5f);

            Assert.Greater(center, 0.95f);
            Assert.Greater(innerEdge, boundary);
            Assert.Greater(boundary, outerEdge);
            Assert.AreEqual(0f, mask.Evaluate(0.05f, 0.5f), 0.0001f);
            Assert.AreEqual(boundary, mask.Evaluate(0.8f, 0.5f), 0.0001f);
        }

        [Test]
        public void StrongFalloff_PushesEdgesBelowSeaLevel()
        {
            var request = new TerrainGenerationRequest(
                seed: 1,
                resolutionX: 33,
                resolutionZ: 33,
                worldSizeX: 32f,
                worldSizeZ: 32f,
                heightScale: 10f,
                seaLevel: 0f,
                noiseScale: 20f,
                octaves: 1,
                persistence: 0.5f,
                lacunarity: 2f,
                noiseMode: TerrainNoiseMode.Smooth,
                maskMode: TerrainMaskMode.Radial,
                falloffStrength: 10f,
                falloffExponent: 1f,
                islandCount: 1,
                islandRadius: 0.4f,
                minIslandSeparation: 0.2f,
                blendMode: MultiIslandBlendMode.Max);

            Heightfield heightfield = TerrainGenerator.GenerateHeightfield(
                request,
                new ConstantNoise(0.75f),
                TerrainGenerator.BuildMask(request));

            Assert.Greater(heightfield.GetHeight(16, 16), request.SeaLevel);
            for (int x = 0; x < heightfield.Width; x++)
            {
                Assert.Less(heightfield.GetHeight(x, 0), request.SeaLevel);
                Assert.Less(heightfield.GetHeight(x, heightfield.Depth - 1), request.SeaLevel);
            }

            for (int z = 0; z < heightfield.Depth; z++)
            {
                Assert.Less(heightfield.GetHeight(0, z), request.SeaLevel);
                Assert.Less(heightfield.GetHeight(heightfield.Width - 1, z), request.SeaLevel);
            }
        }

        [Test]
        public void IslandCenters_AreDeterministicAndSeparated()
        {
            Vector2[] first = IslandCenterGenerator.Generate(99, 8, 0.12f);
            Vector2[] second = IslandCenterGenerator.Generate(99, 8, 0.12f);

            Assert.AreEqual(first.Length, second.Length);
            for (int i = 0; i < first.Length; i++)
            {
                Assert.AreEqual(first[i], second[i]);
            }

            for (int i = 0; i < first.Length; i++)
            {
                for (int j = i + 1; j < first.Length; j++)
                {
                    Assert.GreaterOrEqual(Vector2.Distance(first[i], first[j]), 0.12f);
                }
            }
        }

        [Test]
        public void MultiIslandMask_StaysInRangeAcrossBlendModes()
        {
            Vector2[] centers = IslandCenterGenerator.Generate(4, 4, 0.1f);
            foreach (MultiIslandBlendMode blendMode in System.Enum.GetValues(typeof(MultiIslandBlendMode)))
            {
                var mask = new MultiIslandMask(centers, 0.35f, 1.4f, blendMode);
                for (int y = 0; y <= 10; y++)
                {
                    for (int x = 0; x <= 10; x++)
                    {
                        float value = mask.Evaluate(x / 10f, y / 10f);
                        Assert.GreaterOrEqual(value, 0f);
                        Assert.LessOrEqual(value, 1f);
                    }
                }
            }
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
