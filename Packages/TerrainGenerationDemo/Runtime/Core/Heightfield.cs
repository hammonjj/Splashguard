using System;

namespace BitBox.TerrainGeneration.Core
{
    public sealed class Heightfield
    {
        public Heightfield(int width, int depth, float[] heights, float seaLevel)
        {
            if (width < TerrainGenerationRequest.MinResolution)
            {
                throw new ArgumentOutOfRangeException(nameof(width));
            }

            if (depth < TerrainGenerationRequest.MinResolution)
            {
                throw new ArgumentOutOfRangeException(nameof(depth));
            }

            if (heights == null)
            {
                throw new ArgumentNullException(nameof(heights));
            }

            if (heights.Length != width * depth)
            {
                throw new ArgumentException("Height array length must equal width * depth.", nameof(heights));
            }

            Width = width;
            Depth = depth;
            Heights = heights;
            SeaLevel = seaLevel;

            float min = heights[0];
            float max = heights[0];
            for (int i = 1; i < heights.Length; i++)
            {
                float height = heights[i];
                if (height < min)
                {
                    min = height;
                }

                if (height > max)
                {
                    max = height;
                }
            }

            MinHeight = min;
            MaxHeight = max;
        }

        public int Width { get; }
        public int Depth { get; }
        public float[] Heights { get; }
        public float SeaLevel { get; }
        public float MinHeight { get; }
        public float MaxHeight { get; }

        public int IndexOf(int x, int z)
        {
            return z * Width + x;
        }

        public float GetHeight(int x, int z)
        {
            return Heights[IndexOf(x, z)];
        }

        public bool IsLand(int x, int z)
        {
            return SeaLevelClassifier.IsLand(GetHeight(x, z), SeaLevel);
        }

        public int CountLandCells(float seaLevel)
        {
            int count = 0;
            for (int i = 0; i < Heights.Length; i++)
            {
                if (SeaLevelClassifier.IsLand(Heights[i], seaLevel))
                {
                    count++;
                }
            }

            return count;
        }
    }
}
