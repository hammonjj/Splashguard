using BitBox.TerrainGeneration.Core;
using UnityEngine;

namespace BitBox.TerrainGeneration.Editor.Export
{
    public static class TerrainDataExporter
    {
        public static TerrainData CreateTerrainData(Heightfield heightfield, float worldSizeX, float worldSizeZ)
        {
            var terrainData = new TerrainData
            {
                heightmapResolution = heightfield.Width,
                size = new Vector3(
                    worldSizeX,
                    Mathf.Max(0.001f, heightfield.MaxHeight - heightfield.MinHeight),
                    worldSizeZ)
            };

            var normalizedHeights = new float[heightfield.Depth, heightfield.Width];
            float range = Mathf.Max(0.001f, heightfield.MaxHeight - heightfield.MinHeight);
            for (int z = 0; z < heightfield.Depth; z++)
            {
                for (int x = 0; x < heightfield.Width; x++)
                {
                    float height = heightfield.GetHeight(x, z);
                    normalizedHeights[z, x] = Mathf.Clamp01((height - heightfield.MinHeight) / range);
                }
            }

            terrainData.SetHeights(0, 0, normalizedHeights);
            return terrainData;
        }
    }
}
