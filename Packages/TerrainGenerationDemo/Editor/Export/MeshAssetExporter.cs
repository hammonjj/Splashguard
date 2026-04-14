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

            Mesh mesh = UnityMeshApplier.CreateMesh(arrays, "Generated Island Mesh");
            AssetDatabase.CreateAsset(mesh, assetPath);
            AssetDatabase.SaveAssets();
            return mesh;
        }
    }
}
