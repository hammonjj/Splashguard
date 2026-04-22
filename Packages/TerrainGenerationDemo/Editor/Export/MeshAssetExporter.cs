using System;
using System.Globalization;
using System.IO;
using System.Text;
using BitBox.TerrainGeneration.Core;
using BitBox.TerrainGeneration.Unity;
using UnityEditor;
using UnityEngine;

namespace BitBox.TerrainGeneration.Editor.Export
{
    public static class MeshAssetExporter
    {
        public static Mesh SaveMeshAsset(MeshArrays arrays, string assetPath)
        {
            if (AssetDatabase.LoadAssetAtPath<Mesh>(assetPath) != null)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }

            Mesh mesh = UnityMeshApplier.CreateMesh(arrays, "Generated TerraForge Mesh");
            AssetDatabase.CreateAsset(mesh, assetPath);
            AssetDatabase.SaveAssets();
            return mesh;
        }
    }

    public static class ObjMeshExporter
    {
        public static void SaveObj(LayeredTerrainMeshes meshes, string filePath)
        {
            if (meshes == null)
            {
                throw new ArgumentNullException(nameof(meshes));
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("OBJ export path is required.", nameof(filePath));
            }

            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            Mesh landMesh = null;
            Mesh shallowWaterMesh = null;
            Mesh deepWaterMesh = null;

            try
            {
                landMesh = CreateExportMesh(meshes.Land, "Land");
                shallowWaterMesh = CreateExportMesh(meshes.ShallowWater, "ShallowWater");
                deepWaterMesh = CreateExportMesh(meshes.DeepWater, "DeepWater");

                var builder = new StringBuilder(32768);
                builder.AppendLine("# TerraForge OBJ export");

                int vertexOffset = 1;
                int uvOffset = 1;
                int normalOffset = 1;

                AppendMesh(builder, "Land", landMesh, ref vertexOffset, ref uvOffset, ref normalOffset);
                AppendMesh(builder, "ShallowWater", shallowWaterMesh, ref vertexOffset, ref uvOffset, ref normalOffset);
                AppendMesh(builder, "DeepWater", deepWaterMesh, ref vertexOffset, ref uvOffset, ref normalOffset);

                File.WriteAllText(filePath, builder.ToString());

                if (IsPathInsideProject(filePath))
                {
                    AssetDatabase.Refresh();
                }
            }
            finally
            {
                DestroyImmediateIfNeeded(landMesh);
                DestroyImmediateIfNeeded(shallowWaterMesh);
                DestroyImmediateIfNeeded(deepWaterMesh);
            }
        }

        private static Mesh CreateExportMesh(MeshArrays arrays, string meshName)
        {
            if (arrays == null || arrays.Vertices.Length == 0 || arrays.Triangles.Length == 0)
            {
                return null;
            }

            return UnityMeshApplier.CreateMesh(arrays, meshName);
        }

        private static void AppendMesh(
            StringBuilder builder,
            string objectName,
            Mesh mesh,
            ref int vertexOffset,
            ref int uvOffset,
            ref int normalOffset)
        {
            if (mesh == null)
            {
                return;
            }

            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Vector2[] uvs = mesh.uv;
            int[] triangles = mesh.triangles;

            if (vertices.Length == 0 || triangles.Length == 0)
            {
                return;
            }

            builder.Append("o ").AppendLine(objectName);

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 vertex = vertices[i];
                builder.Append("v ")
                    .Append(Float(-vertex.x)).Append(' ')
                    .Append(Float(vertex.y)).Append(' ')
                    .Append(Float(vertex.z)).AppendLine();
            }

            for (int i = 0; i < uvs.Length; i++)
            {
                Vector2 uv = uvs[i];
                builder.Append("vt ")
                    .Append(Float(uv.x)).Append(' ')
                    .Append(Float(uv.y)).AppendLine();
            }

            for (int i = 0; i < normals.Length; i++)
            {
                Vector3 normal = normals[i];
                builder.Append("vn ")
                    .Append(Float(-normal.x)).Append(' ')
                    .Append(Float(normal.y)).Append(' ')
                    .Append(Float(normal.z)).AppendLine();
            }

            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i] + vertexOffset;
                int b = triangles[i + 1] + vertexOffset;
                int c = triangles[i + 2] + vertexOffset;

                int uvA = triangles[i] + uvOffset;
                int uvB = triangles[i + 1] + uvOffset;
                int uvC = triangles[i + 2] + uvOffset;

                int normalA = triangles[i] + normalOffset;
                int normalB = triangles[i + 1] + normalOffset;
                int normalC = triangles[i + 2] + normalOffset;

                builder.Append("f ")
                    .Append(FormatFaceVertex(a, uvA, normalA)).Append(' ')
                    .Append(FormatFaceVertex(c, uvC, normalC)).Append(' ')
                    .Append(FormatFaceVertex(b, uvB, normalB)).AppendLine();
            }

            vertexOffset += vertices.Length;
            uvOffset += uvs.Length;
            normalOffset += normals.Length;
        }

        private static string FormatFaceVertex(int vertexIndex, int uvIndex, int normalIndex)
        {
            return vertexIndex + "/" + uvIndex + "/" + normalIndex;
        }

        private static string Float(float value)
        {
            return value.ToString("G9", CultureInfo.InvariantCulture);
        }

        private static bool IsPathInsideProject(string filePath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string fullPath = Path.GetFullPath(filePath);
            return fullPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static void DestroyImmediateIfNeeded(UnityEngine.Object obj)
        {
            if (obj != null)
            {
                UnityEngine.Object.DestroyImmediate(obj);
            }
        }
    }
}
