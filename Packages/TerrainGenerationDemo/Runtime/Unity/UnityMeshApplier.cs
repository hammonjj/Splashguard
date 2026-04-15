using BitBox.TerrainGeneration.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace BitBox.TerrainGeneration.Unity
{
    public static class UnityMeshApplier
    {
        public static Mesh ApplyTo(
            MeshFilter meshFilter,
            MeshCollider meshCollider,
            MeshArrays arrays,
            bool updateCollider)
        {
            Mesh mesh = meshFilter.sharedMesh;
            if (mesh == null)
            {
                mesh = new Mesh
                {
                    name = "Generated TerraForge Mesh"
                };
                meshFilter.sharedMesh = mesh;
            }

            ApplyToMesh(mesh, arrays);

            if (updateCollider && meshCollider != null)
            {
                meshCollider.sharedMesh = null;
                meshCollider.sharedMesh = mesh;
            }

            return mesh;
        }

        public static Mesh CreateMesh(MeshArrays arrays, string meshName)
        {
            var mesh = new Mesh
            {
                name = string.IsNullOrWhiteSpace(meshName) ? "Generated TerraForge Mesh" : meshName
            };
            ApplyToMesh(mesh, arrays);
            return mesh;
        }

        public static void ApplyToMesh(Mesh mesh, MeshArrays arrays)
        {
            mesh.Clear();
            if (arrays.Vertices.Length > 65535)
            {
                Debug.LogWarning(
                    $"Generated terrain mesh has {arrays.Vertices.Length} vertices and requires 32-bit indices. " +
                    "Use chunking before targeting platforms that do not support 32-bit mesh indices.");
                mesh.indexFormat = IndexFormat.UInt32;
            }
            else
            {
                mesh.indexFormat = IndexFormat.UInt16;
            }

            mesh.vertices = arrays.Vertices;
            mesh.uv = arrays.Uvs;
            if (arrays.Colors != null)
            {
                mesh.colors = arrays.Colors;
            }

            mesh.triangles = arrays.Triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
        }
    }
}
