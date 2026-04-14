using UnityEngine;

namespace BitBox.TerrainGeneration.Core
{
    public static class TerrainZoneClassifier
    {
        public static TerrainZoneMap GenerateZoneMap(Heightfield heightfield, TerrainZoneSettings settings)
        {
            return GenerateZoneMap(heightfield, settings, worldSizeX: heightfield.Width - 1, worldSizeZ: heightfield.Depth - 1);
        }

        public static TerrainZoneMap GenerateZoneMap(
            Heightfield heightfield,
            TerrainZoneSettings settings,
            float worldSizeX,
            float worldSizeZ)
        {
            var zones = new TerrainZone[heightfield.Width * heightfield.Depth];
            float maxLandRange = Mathf.Max(0.001f, heightfield.MaxHeight - heightfield.SeaLevel);

            for (int z = 0; z < heightfield.Depth; z++)
            {
                for (int x = 0; x < heightfield.Width; x++)
                {
                    float height = heightfield.GetHeight(x, z);
                    float slope = CalculateSlope(heightfield, x, z, worldSizeX, worldSizeZ);
                    float normalizedLandHeight = Mathf.Clamp01((height - heightfield.SeaLevel) / maxLandRange);
                    zones[heightfield.IndexOf(x, z)] = Classify(height, slope, normalizedLandHeight, heightfield.SeaLevel, settings);
                }
            }

            return new TerrainZoneMap(heightfield.Width, heightfield.Depth, zones);
        }

        public static float CalculateSlope(Heightfield heightfield, int x, int z, float worldSizeX, float worldSizeZ)
        {
            int x0 = Mathf.Max(0, x - 1);
            int x1 = Mathf.Min(heightfield.Width - 1, x + 1);
            int z0 = Mathf.Max(0, z - 1);
            int z1 = Mathf.Min(heightfield.Depth - 1, z + 1);

            float stepX = heightfield.Width > 1 ? Mathf.Max(0.001f, worldSizeX) / (heightfield.Width - 1) : 1f;
            float stepZ = heightfield.Depth > 1 ? Mathf.Max(0.001f, worldSizeZ) / (heightfield.Depth - 1) : 1f;
            float dxDistance = Mathf.Max(stepX, (x1 - x0) * stepX);
            float dzDistance = Mathf.Max(stepZ, (z1 - z0) * stepZ);

            float dx = (heightfield.GetHeight(x1, z) - heightfield.GetHeight(x0, z)) / dxDistance;
            float dz = (heightfield.GetHeight(x, z1) - heightfield.GetHeight(x, z0)) / dzDistance;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static TerrainZone Classify(
            float height,
            float slope,
            float normalizedLandHeight,
            float seaLevel,
            TerrainZoneSettings settings)
        {
            if (height < seaLevel - settings.ShallowWaterDepth)
            {
                return TerrainZone.DeepWater;
            }

            if (height < seaLevel)
            {
                return TerrainZone.ShallowWater;
            }

            if (height <= seaLevel + settings.BeachHeightBand)
            {
                return TerrainZone.Beach;
            }

            if (normalizedLandHeight >= settings.MountainElevationThreshold)
            {
                return TerrainZone.Mountain;
            }

            if (slope >= settings.RockSlopeThreshold)
            {
                return TerrainZone.Rock;
            }

            return TerrainZone.Grassland;
        }
    }
}
