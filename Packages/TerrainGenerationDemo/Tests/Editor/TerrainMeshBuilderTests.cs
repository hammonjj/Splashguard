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
                var meshFilter = root.transform.GetChild(0).GetComponent<MeshFilter>();
                Assert.IsNotNull(meshFilter);
                Assert.IsNotNull(meshFilter.sharedMesh);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
