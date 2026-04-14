using BitBox.TerrainGeneration.Core;
using UnityEngine;

namespace BitBox.TerrainGeneration.Unity
{
    public static class TexturePreviewBuilder
    {
        public static Texture2D BuildHeightPreview(Heightfield heightfield)
        {
            var texture = new Texture2D(heightfield.Width, heightfield.Depth, TextureFormat.RGBA32, mipChain: false)
            {
                name = "Terrain Height Preview",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color[heightfield.Width * heightfield.Depth];
            for (int z = 0; z < heightfield.Depth; z++)
            {
                for (int x = 0; x < heightfield.Width; x++)
                {
                    int sourceIndex = heightfield.IndexOf(x, z);
                    int textureIndex = z * heightfield.Width + x;
                    pixels[textureIndex] = BuildPixel(
                        heightfield.Heights[sourceIndex],
                        heightfield.SeaLevel,
                        heightfield.MinHeight,
                        heightfield.MaxHeight);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            return texture;
        }

        private static Color BuildPixel(float height, float seaLevel, float minHeight, float maxHeight)
        {
            if (SeaLevelClassifier.IsWater(height, seaLevel))
            {
                float water = Mathf.InverseLerp(minHeight, seaLevel, height);
                return Color.Lerp(new Color(0.02f, 0.10f, 0.22f, 1f), new Color(0.08f, 0.48f, 0.66f, 1f), water);
            }

            float land = Mathf.InverseLerp(seaLevel, Mathf.Max(seaLevel + 0.001f, maxHeight), height);
            if (land > 0.78f)
            {
                return Color.Lerp(new Color(0.46f, 0.48f, 0.42f, 1f), new Color(0.9f, 0.88f, 0.80f, 1f), (land - 0.78f) / 0.22f);
            }

            return Color.Lerp(new Color(0.18f, 0.45f, 0.22f, 1f), new Color(0.50f, 0.58f, 0.32f, 1f), land / 0.78f);
        }
    }
}
