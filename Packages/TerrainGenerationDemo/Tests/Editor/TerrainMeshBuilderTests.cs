using System.Linq;
using BitBox.TerrainGeneration.Core;
using BitBox.TerrainGeneration.Unity;
using NUnit.Framework;
using UnityEngine;

namespace BitBox.TerrainGeneration.Tests.Editor
{
    public sealed class TerrainMeshBuilderTests
    {
        [Test]
        public void Build_FlatGrid_HasExpectedTopology()
        {
            var heightfield = new Heightfield(10, 20, new float[10 * 20], seaLevel: 0f);
            MeshArrays arrays = TerrainMeshBuilder.Build(heightfield, cellSize: 1f);

            Assert.AreEqual(200, arrays.Vertices.Length);
            Assert.AreEqual(9 * 19 * 6, arrays.Triangles.Length);
            Assert.AreEqual(200, arrays.Uvs.Length);
            Assert.IsTrue(arrays.Triangles.All(index => index >= 0 && index < arrays.Vertices.Length));
        }

        [Test]
        public void Build_Uvs_AreNormalized()
        {
            var heightfield = new Heightfield(4, 3, new float[4 * 3], seaLevel: 0f);
            MeshArrays arrays = TerrainMeshBuilder.Build(heightfield, cellSize: 1f);

            foreach (Vector2 uv in arrays.Uvs)
            {
                Assert.GreaterOrEqual(uv.x, 0f);
                Assert.LessOrEqual(uv.x, 1f);
                Assert.GreaterOrEqual(uv.y, 0f);
                Assert.LessOrEqual(uv.y, 1f);
            }

            Assert.AreEqual(Vector2.zero, arrays.Uvs[0]);
            Assert.AreEqual(Vector2.one, arrays.Uvs[arrays.Uvs.Length - 1]);
        }

        [Test]
        public void BuildLayeredMeshes_SplitsLandShallowAndDeepTriangles()
        {
            var heightfield = new Heightfield(3, 3, new float[9], seaLevel: 0f);
            var zoneMap = new TerrainZoneMap(
                3,
                3,
                new[]
                {
                    TerrainZone.DeepWater, TerrainZone.DeepWater, TerrainZone.ShallowWater,
                    TerrainZone.DeepWater, TerrainZone.ShallowWater, TerrainZone.Beach,
                    TerrainZone.Grassland, TerrainZone.Grassland, TerrainZone.Mountain
                });

            LayeredTerrainMeshes meshes = LayeredTerrainMeshBuilder.Build(
                heightfield,
                zoneMap,
                worldSizeX: 2f,
                worldSizeZ: 2f,
                TerrainZoneColorPalette.Default,
                smoothingPasses: 1);

            Assert.Greater(meshes.Land.Triangles.Length, 0);
            Assert.Greater(meshes.ShallowWater.Triangles.Length, 0);
            Assert.Greater(meshes.DeepWater.Triangles.Length, 0);
            Assert.AreEqual(9, meshes.Land.Vertices.Length);
            Assert.AreEqual(9, meshes.ShallowWater.Vertices.Length);
            Assert.AreEqual(9, meshes.DeepWater.Vertices.Length);
            Assert.AreEqual(
                (3 - 1) * (3 - 1) * 6,
                meshes.Land.Triangles.Length + meshes.ShallowWater.Triangles.Length + meshes.DeepWater.Triangles.Length);
        }

        [Test]
        public void BuildLayeredMeshes_WithShorelineWalls_AppendsWaterlineAndFloorVertices()
        {
            const float seaLevel = 0f;
            const float floorHeight = -2f;
            var heightfield = new Heightfield(
                3,
                3,
                new[]
                {
                    1f, floorHeight, floorHeight,
                    1f, floorHeight, floorHeight,
                    1f, floorHeight, floorHeight
                },
                seaLevel);
            var zoneMap = new TerrainZoneMap(
                3,
                3,
                new[]
                {
                    TerrainZone.Beach, TerrainZone.ShallowWater, TerrainZone.ShallowWater,
                    TerrainZone.Beach, TerrainZone.ShallowWater, TerrainZone.ShallowWater,
                    TerrainZone.Beach, TerrainZone.ShallowWater, TerrainZone.ShallowWater
                });

            LayeredTerrainMeshes meshes = LayeredTerrainMeshBuilder.Build(
                heightfield,
                zoneMap,
                worldSizeX: 2f,
                worldSizeZ: 2f,
                TerrainZoneColorPalette.Default,
                smoothingPasses: 0,
                includeShorelineWalls: true,
                shorelineFloorHeight: floorHeight);

            Assert.Greater(meshes.Land.Vertices.Length, heightfield.Width * heightfield.Depth);

            bool hasWaterlineVertex = false;
            bool hasFloorVertex = false;
            for (int i = heightfield.Width * heightfield.Depth; i < meshes.Land.Vertices.Length; i++)
            {
                float y = meshes.Land.Vertices[i].y;
                bool isWaterline = Mathf.Abs(y - seaLevel) <= 0.0001f;
                bool isFloor = Mathf.Abs(y - floorHeight) <= 0.0001f;
                Assert.IsTrue(isWaterline || isFloor);
                hasWaterlineVertex |= isWaterline;
                hasFloorVertex |= isFloor;
            }

            Assert.IsTrue(hasWaterlineVertex);
            Assert.IsTrue(hasFloorVertex);
        }

        [Test]
        public void BuildLayeredMeshes_WithRoundedBasinRequest_AppendsSmoothPoolBorderWallAndFloor()
        {
            const float seaLevel = 0f;
            const float flatFloorDepth = 2f;
            const float poolBorderHeight = 0.35f;
            TerrainGenerationRequest request = new TerrainGenerationRequest(
                seed: 11,
                resolutionX: 65,
                resolutionZ: 65,
                worldSizeX: 64f,
                worldSizeZ: 64f,
                heightScale: 0f,
                seaLevel: seaLevel,
                noiseScale: 500f,
                octaves: 1,
                persistence: 0f,
                lacunarity: 1f,
                noiseMode: TerrainNoiseMode.Smooth,
                maskMode: TerrainMaskMode.RoundedBasin,
                falloffStrength: 8f,
                falloffExponent: 1f,
                islandCount: 1,
                islandRadius: 0.4f,
                minIslandSeparation: 0.2f,
                blendMode: MultiIslandBlendMode.SmoothUnion,
                underwaterProfile: TerrainUnderwaterProfile.FlatFloor,
                flatFloorDepth: flatFloorDepth,
                basinWidth: 0.7f,
                basinDepth: 0.5f,
                basinCornerRadius: 0.16f,
                basinEdgeSoftness: 0.02f,
                poolBorderWidth: 0.06f,
                poolBorderHeight: poolBorderHeight);
            Heightfield heightfield = TerrainGenerator.GenerateHeightfield(request);
            TerrainZoneMap zoneMap = TerrainZoneClassifier.GenerateZoneMap(
                heightfield,
                TerrainZoneSettings.Default,
                request.WorldSizeX,
                request.WorldSizeZ);

            LayeredTerrainMeshes meshes = LayeredTerrainMeshBuilder.Build(
                heightfield,
                zoneMap,
                request.WorldSizeX,
                request.WorldSizeZ,
                TerrainZoneColorPalette.Default,
                smoothingPasses: 0,
                request: request);

            int baseVertexCount = heightfield.Width * heightfield.Depth;
            Assert.Greater(meshes.Land.Vertices.Length, baseVertexCount);
            Assert.IsTrue(meshes.Land.Triangles.All(index => index >= 0 && index < meshes.Land.Vertices.Length));

            int seaLevelVertexCount = 0;
            int floorVertexCount = 0;
            int borderTopVertexCount = 0;
            float floorHeight = seaLevel - flatFloorDepth;
            float borderTopHeight = seaLevel + poolBorderHeight;
            for (int i = baseVertexCount; i < meshes.Land.Vertices.Length; i++)
            {
                float y = meshes.Land.Vertices[i].y;
                bool isSeaLevel = Mathf.Abs(y - seaLevel) <= 0.0001f;
                bool isFloor = Mathf.Abs(y - floorHeight) <= 0.0001f;
                bool isBorderTop = Mathf.Abs(y - borderTopHeight) <= 0.0001f;

                Assert.IsTrue(isSeaLevel || isFloor || isBorderTop);
                seaLevelVertexCount += isSeaLevel ? 1 : 0;
                floorVertexCount += isFloor ? 1 : 0;
                borderTopVertexCount += isBorderTop ? 1 : 0;
            }

            Assert.Greater(seaLevelVertexCount, 80);
            Assert.Greater(floorVertexCount, 80);
            Assert.Greater(borderTopVertexCount, 80);
        }

        [Test]
        public void DemoRunner_Regenerate_ReusesSingleGeneratedChild()
        {
            var root = new GameObject("Terrain Runner Test");
            try
            {
                var runner = root.AddComponent<TerrainGenerationDemoRunner>();
                runner.Generate();
                runner.Generate();

                int generatedChildCount = 0;
                for (int i = 0; i < root.transform.childCount; i++)
                {
                    if (root.transform.GetChild(i).name == "Generated Island Terrain")
                    {
                        generatedChildCount++;
                    }
                }

                Assert.AreEqual(1, generatedChildCount);
                Transform generatedTerrain = root.transform.GetChild(0);
                Assert.IsNull(generatedTerrain.GetComponent<MeshFilter>());

                Transform land = generatedTerrain.Find("Real Terrain");
                Transform shallowWater = generatedTerrain.Find("Shallow Water");
                Transform deepWater = generatedTerrain.Find("Deep Water");
                Assert.IsNotNull(land);
                Assert.IsNotNull(shallowWater);
                Assert.IsNotNull(deepWater);
                Assert.IsNotNull(land.GetComponent<MeshFilter>().sharedMesh);
                Assert.IsNotNull(shallowWater.GetComponent<MeshFilter>().sharedMesh);
                Assert.IsNotNull(deepWater.GetComponent<MeshFilter>().sharedMesh);
                Assert.IsNotNull(land.GetComponent<MeshCollider>());
                Assert.IsNull(shallowWater.GetComponent<MeshCollider>());
                Assert.IsNull(deepWater.GetComponent<MeshCollider>());
                Assert.IsNotNull(generatedTerrain.Find("Props"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
