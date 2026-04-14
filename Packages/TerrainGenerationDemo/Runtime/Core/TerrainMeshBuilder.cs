using UnityEngine;

namespace BitBox.TerrainGeneration.Core
{
    public static class TerrainMeshBuilder
    {
        public static MeshArrays Build(Heightfield heightfield, float cellSize)
        {
            float sizeX = (heightfield.Width - 1) * Mathf.Max(0.001f, cellSize);
            float sizeZ = (heightfield.Depth - 1) * Mathf.Max(0.001f, cellSize);
            return Build(heightfield, sizeX, sizeZ, includeClassificationColors: true);
        }

        public static MeshArrays Build(
            Heightfield heightfield,
            float worldSizeX,
            float worldSizeZ,
            bool includeClassificationColors)
        {
            int width = heightfield.Width;
            int depth = heightfield.Depth;
            var vertices = new Vector3[width * depth];
            var uvs = new Vector2[vertices.Length];
            var colors = includeClassificationColors ? new Color[vertices.Length] : null;

            float stepX = width > 1 ? worldSizeX / (width - 1) : 0f;
            float stepZ = depth > 1 ? worldSizeZ / (depth - 1) : 0f;
            float originX = -worldSizeX * 0.5f;
            float originZ = -worldSizeZ * 0.5f;

            for (int z = 0; z < depth; z++)
            {
                float v = depth <= 1 ? 0f : z / (float)(depth - 1);
                for (int x = 0; x < width; x++)
                {
                    float u = width <= 1 ? 0f : x / (float)(width - 1);
                    int i = heightfield.IndexOf(x, z);
                    float y = heightfield.Heights[i];
                    vertices[i] = new Vector3(originX + x * stepX, y, originZ + z * stepZ);
                    uvs[i] = new Vector2(u, v);

                    if (colors != null)
                    {
                        colors[i] = BuildClassificationColor(y, heightfield.SeaLevel, heightfield.MinHeight, heightfield.MaxHeight);
                    }
                }
            }

            var triangles = new int[(width - 1) * (depth - 1) * 6];
            int t = 0;
            for (int z = 0; z < depth - 1; z++)
            {
                for (int x = 0; x < width - 1; x++)
                {
                    int i0 = heightfield.IndexOf(x, z);
                    int i1 = i0 + 1;
                    int i2 = i0 + width;
                    int i3 = i2 + 1;

                    triangles[t++] = i0;
                    triangles[t++] = i2;
                    triangles[t++] = i1;
                    triangles[t++] = i1;
                    triangles[t++] = i2;
                    triangles[t++] = i3;
                }
            }

            return new MeshArrays(vertices, triangles, uvs, colors);
        }

        private static Color BuildClassificationColor(float height, float seaLevel, float minHeight, float maxHeight)
        {
            if (SeaLevelClassifier.IsWater(height, seaLevel))
            {
                float waterDepth = Mathf.InverseLerp(seaLevel, minHeight, height);
                return Color.Lerp(new Color(0.04f, 0.19f, 0.34f, 1f), new Color(0.08f, 0.42f, 0.62f, 1f), waterDepth);
            }

            float landHeight = Mathf.InverseLerp(seaLevel, Mathf.Max(seaLevel + 0.001f, maxHeight), height);
            return Color.Lerp(new Color(0.25f, 0.55f, 0.24f, 1f), new Color(0.82f, 0.78f, 0.68f, 1f), landHeight);
        }
    }
}
