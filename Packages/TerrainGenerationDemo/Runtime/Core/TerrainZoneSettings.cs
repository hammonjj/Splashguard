using UnityEngine;

namespace BitBox.TerrainGeneration.Core
{
    public readonly struct TerrainZoneSettings
    {
        public readonly float ShallowWaterDepth;
        public readonly float BeachHeightBand;
        public readonly float RockSlopeThreshold;
        public readonly float MountainElevationThreshold;

        public TerrainZoneSettings(
            float shallowWaterDepth,
            float beachHeightBand,
            float rockSlopeThreshold,
            float mountainElevationThreshold)
        {
            ShallowWaterDepth = Mathf.Max(0f, shallowWaterDepth);
            BeachHeightBand = Mathf.Max(0f, beachHeightBand);
            RockSlopeThreshold = Mathf.Max(0f, rockSlopeThreshold);
            MountainElevationThreshold = Mathf.Clamp01(mountainElevationThreshold);
        }

        public static TerrainZoneSettings Default => new(
            shallowWaterDepth: 2.2f,
            beachHeightBand: 1.2f,
            rockSlopeThreshold: 0.42f,
            mountainElevationThreshold: 0.72f);
    }
}
