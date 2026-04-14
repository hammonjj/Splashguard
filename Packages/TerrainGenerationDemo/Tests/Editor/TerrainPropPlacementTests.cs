using BitBox.TerrainGeneration.Core;
using NUnit.Framework;
using UnityEngine;

namespace BitBox.TerrainGeneration.Tests.Editor
{
    public sealed class TerrainPropPlacementTests
    {
        [Test]
        public void GeneratePlacements_SameSeed_IsDeterministic()
        {
            BuildPropFixture(out Heightfield heightfield, out TerrainZoneMap zoneMap, out TerrainGenerationRequest request);
            TerrainPropPlacementSettings settings = DenseSettings(minSpacing: 3f);

            TerrainPropPlacement[] first = TerrainPropPlacer.GeneratePlacements(heightfield, zoneMap, request, settings);
            TerrainPropPlacement[] second = TerrainPropPlacer.GeneratePlacements(heightfield, zoneMap, request, settings);

            Assert.AreEqual(first.Length, second.Length);
            for (int i = 0; i < first.Length; i++)
            {
                Assert.AreEqual(first[i].Type, second[i].Type);
                Assert.AreEqual(first[i].Position, second[i].Position);
                Assert.AreEqual(first[i].YawDegrees, second[i].YawDegrees);
                Assert.AreEqual(first[i].Scale, second[i].Scale);
                Assert.AreEqual(first[i].SourceZone, second[i].SourceZone);
            }
        }

        [Test]
        public void GeneratePlacements_DoesNotSpawnUnderwater()
        {
            BuildPropFixture(out Heightfield heightfield, out TerrainZoneMap zoneMap, out TerrainGenerationRequest request);
            TerrainPropPlacement[] placements = TerrainPropPlacer.GeneratePlacements(
                heightfield,
                zoneMap,
                request,
                DenseSettings(minSpacing: 2f));

            Assert.IsNotEmpty(placements);
            for (int i = 0; i < placements.Length; i++)
            {
                Assert.GreaterOrEqual(placements[i].Position.y, heightfield.SeaLevel);
                Assert.AreNotEqual(TerrainZone.DeepWater, placements[i].SourceZone);
                Assert.AreNotEqual(TerrainZone.ShallowWater, placements[i].SourceZone);
            }
        }

        [Test]
        public void GeneratePlacements_RespectsZoneRestrictions()
        {
            BuildPropFixture(out Heightfield heightfield, out TerrainZoneMap zoneMap, out TerrainGenerationRequest request);
            TerrainPropPlacement[] placements = TerrainPropPlacer.GeneratePlacements(
                heightfield,
                zoneMap,
                request,
                DenseSettings(minSpacing: 2f));

            Assert.IsNotEmpty(placements);
            for (int i = 0; i < placements.Length; i++)
            {
                TerrainPropPlacement placement = placements[i];
                if (placement.Type == TerrainPropType.Tree || placement.Type == TerrainPropType.GrassPatch)
                {
                    Assert.AreEqual(TerrainZone.Grassland, placement.SourceZone);
                }
                else if (placement.Type == TerrainPropType.Rock)
                {
                    Assert.IsTrue(placement.SourceZone == TerrainZone.Rock || placement.SourceZone == TerrainZone.Mountain);
                }
                else if (placement.Type == TerrainPropType.Driftwood)
                {
                    Assert.AreEqual(TerrainZone.Beach, placement.SourceZone);
                }
            }
        }

        [Test]
        public void GeneratePlacements_EnforcesMinimumSpacingPerType()
        {
            BuildPropFixture(out Heightfield heightfield, out TerrainZoneMap zoneMap, out TerrainGenerationRequest request);
            float minSpacing = 5f;
            TerrainPropPlacement[] placements = TerrainPropPlacer.GeneratePlacements(
                heightfield,
                zoneMap,
                request,
                DenseSettings(minSpacing));

            for (int i = 0; i < placements.Length; i++)
            {
                for (int j = i + 1; j < placements.Length; j++)
                {
                    if (placements[i].Type != placements[j].Type)
                    {
                        continue;
                    }

                    var first = new Vector2(placements[i].Position.x, placements[i].Position.z);
                    var second = new Vector2(placements[j].Position.x, placements[j].Position.z);
                    Assert.GreaterOrEqual(Vector2.Distance(first, second), minSpacing);
                }
            }
        }

        private static void BuildPropFixture(
            out Heightfield heightfield,
            out TerrainZoneMap zoneMap,
            out TerrainGenerationRequest request)
        {
            int width = 9;
            int depth = 9;
            var heights = new float[width * depth];
            var zones = new TerrainZone[width * depth];

            for (int z = 0; z < depth; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = z * width + x;
                    if (z == 0)
                    {
                        heights[index] = -2f;
                        zones[index] = TerrainZone.DeepWater;
                    }
                    else if (z == 1)
                    {
                        heights[index] = -0.5f;
                        zones[index] = TerrainZone.ShallowWater;
                    }
                    else if (z == 2)
                    {
                        heights[index] = 0.2f;
                        zones[index] = TerrainZone.Beach;
                    }
                    else if (z < 6)
                    {
                        heights[index] = 2f;
                        zones[index] = TerrainZone.Grassland;
                    }
                    else if (z < 8)
                    {
                        heights[index] = 5f;
                        zones[index] = TerrainZone.Rock;
                    }
                    else
                    {
                        heights[index] = 8f;
                        zones[index] = TerrainZone.Mountain;
                    }
                }
            }

            heightfield = new Heightfield(width, depth, heights, seaLevel: 0f);
            zoneMap = new TerrainZoneMap(width, depth, zones);
            request = new TerrainGenerationRequest(
                seed: 321,
                resolutionX: width,
                resolutionZ: depth,
                worldSizeX: 32f,
                worldSizeZ: 32f,
                heightScale: 8f,
                seaLevel: 0f,
                noiseScale: 12f,
                octaves: 1,
                persistence: 0.5f,
                lacunarity: 2f,
                noiseMode: TerrainNoiseMode.Smooth,
                maskMode: TerrainMaskMode.None,
                falloffStrength: 0f,
                falloffExponent: 1f,
                islandCount: 1,
                islandRadius: 0.4f,
                minIslandSeparation: 0.2f,
                blendMode: MultiIslandBlendMode.Max);
        }

        private static TerrainPropPlacementSettings DenseSettings(float minSpacing)
        {
            return new TerrainPropPlacementSettings(
                treeDensity: 1000f,
                rockDensity: 1000f,
                grassPatchDensity: 1000f,
                driftwoodDensity: 1000f,
                treeMaxSlope: 999f,
                grassPatchMaxSlope: 999f,
                driftwoodMaxSlope: 999f,
                treeMinSpacing: minSpacing,
                rockMinSpacing: minSpacing,
                grassPatchMinSpacing: minSpacing,
                driftwoodMinSpacing: minSpacing,
                minScale: 0.8f,
                maxScale: 1.2f);
        }
    }
}
