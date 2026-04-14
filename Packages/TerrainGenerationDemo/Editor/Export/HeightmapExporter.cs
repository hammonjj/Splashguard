using System.IO;
using BitBox.TerrainGeneration.Core;
using BitBox.TerrainGeneration.Unity;
using UnityEditor;
using UnityEngine;

namespace BitBox.TerrainGeneration.Editor.Export
{
    public static class HeightmapExporter
    {
        public static Texture2D SavePng(Heightfield heightfield, string assetPath)
        {
            Texture2D texture = TexturePreviewBuilder.BuildHeightPreview(heightfield);
            byte[] bytes = texture.EncodeToPNG();
            File.WriteAllBytes(assetPath, bytes);
            AssetDatabase.ImportAsset(assetPath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }
    }
}
