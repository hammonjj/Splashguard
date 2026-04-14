using UnityEditor;
using UnityEngine;

namespace BitBox.TerrainGeneration.Editor.Export
{
    public static class PrefabExporter
    {
        public static GameObject SavePrefab(GameObject root, string assetPath)
        {
            return PrefabUtility.SaveAsPrefabAsset(root, assetPath);
        }
    }
}
