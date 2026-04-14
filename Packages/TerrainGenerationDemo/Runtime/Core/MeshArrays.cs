using System;
using UnityEngine;

namespace BitBox.TerrainGeneration.Core
{
    public sealed class MeshArrays
    {
        public MeshArrays(Vector3[] vertices, int[] triangles, Vector2[] uvs, Color[] colors = null)
        {
            Vertices = vertices ?? throw new ArgumentNullException(nameof(vertices));
            Triangles = triangles ?? throw new ArgumentNullException(nameof(triangles));
            Uvs = uvs ?? throw new ArgumentNullException(nameof(uvs));
            Colors = colors;

            if (Vertices.Length != Uvs.Length)
            {
                throw new ArgumentException("UV count must match vertex count.", nameof(uvs));
            }

            if (Colors != null && Colors.Length != Vertices.Length)
            {
                throw new ArgumentException("Color count must match vertex count.", nameof(colors));
            }
        }

        public Vector3[] Vertices { get; }
        public int[] Triangles { get; }
        public Vector2[] Uvs { get; }
        public Color[] Colors { get; }
    }
}
