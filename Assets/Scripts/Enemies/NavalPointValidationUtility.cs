using Bitbox.Toymageddon.Nautical;
using UnityEngine;

namespace Bitbox.Splashguard.Enemies
{
    public readonly struct NavalPointValidationSettings
    {
        public NavalPointValidationSettings(
            int terrainLayerMask,
            float terrainProbeHeight,
            float terrainProbeDepth,
            float terrainClearance,
            float shorelinePerimeterRadius,
            int perimeterSampleCount)
        {
            TerrainLayerMask = terrainLayerMask;
            TerrainProbeHeight = Mathf.Max(0.1f, terrainProbeHeight);
            TerrainProbeDepth = Mathf.Max(0.1f, terrainProbeDepth);
            TerrainClearance = Mathf.Max(0f, terrainClearance);
            ShorelinePerimeterRadius = Mathf.Max(0f, shorelinePerimeterRadius);
            PerimeterSampleCount = Mathf.Max(0, perimeterSampleCount);
        }

        public int TerrainLayerMask { get; }
        public float TerrainProbeHeight { get; }
        public float TerrainProbeDepth { get; }
        public float TerrainClearance { get; }
        public float ShorelinePerimeterRadius { get; }
        public int PerimeterSampleCount { get; }
    }

    public readonly struct NavalPointValidationResult
    {
        public NavalPointValidationResult(bool isValid, Vector3 surfacePoint, float waterHeight)
        {
            IsValid = isValid;
            SurfacePoint = surfacePoint;
            WaterHeight = waterHeight;
        }

        public bool IsValid { get; }
        public Vector3 SurfacePoint { get; }
        public float WaterHeight { get; }
    }

    public static class NavalPointValidationUtility
    {
        private const string TerrainLayerName = "Terrain";
        private const float MinimumAbsoluteTerrainProbeHeight = 64f;
        private const float MinimumAbsoluteTerrainProbeDepth = 128f;

        public static bool IsPointNavigable(Vector3 point, NavalPointValidationSettings settings)
        {
            return ValidateCandidate(point, settings).IsValid;
        }

        public static NavalPointValidationResult ValidateCandidate(Vector3 point, NavalPointValidationSettings settings)
        {
            if (!WaterQuery.TrySample(point, out WaterSample waterSample))
            {
                return default;
            }

            if (IsTerrainAboveWater(point, waterSample.Height, settings))
            {
                return default;
            }

            if (settings.ShorelinePerimeterRadius > 0.01f
                && settings.PerimeterSampleCount > 0
                && !HasClearWaterPerimeter(point, settings))
            {
                return default;
            }

            Vector3 surfacePoint = point;
            surfacePoint.y = waterSample.Height;
            return new NavalPointValidationResult(true, surfacePoint, waterSample.Height);
        }

        public static bool IsTerrainAboveWater(
            Vector3 point,
            float waterHeight,
            NavalPointValidationSettings settings)
        {
            int terrainLayerMask = settings.TerrainLayerMask != 0
                ? settings.TerrainLayerMask
                : LayerMask.GetMask(TerrainLayerName);
            if (terrainLayerMask == 0)
            {
                return false;
            }

            float probeOriginHeight = Mathf.Max(
                point.y + settings.TerrainProbeHeight,
                waterHeight + settings.TerrainProbeHeight,
                waterHeight + MinimumAbsoluteTerrainProbeHeight);
            float probeDepth = Mathf.Max(
                settings.TerrainProbeDepth,
                MinimumAbsoluteTerrainProbeDepth);
            float probeDistance = (probeOriginHeight - waterHeight) + probeDepth;
            Vector3 probeOrigin = new(point.x, probeOriginHeight, point.z);

            if (!Physics.Raycast(
                    probeOrigin,
                    Vector3.down,
                    out RaycastHit hit,
                    probeDistance,
                    terrainLayerMask,
                    QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            return hit.point.y >= waterHeight - settings.TerrainClearance;
        }

        private static bool HasClearWaterPerimeter(Vector3 center, NavalPointValidationSettings settings)
        {
            int sampleCount = Mathf.Max(4, settings.PerimeterSampleCount);
            for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
            {
                float angle = Mathf.PI * 2f * sampleIndex / sampleCount;
                Vector3 perimeterPoint = center + new Vector3(
                    Mathf.Cos(angle) * settings.ShorelinePerimeterRadius,
                    0f,
                    Mathf.Sin(angle) * settings.ShorelinePerimeterRadius);

                if (!WaterQuery.TrySample(perimeterPoint, out WaterSample perimeterSample))
                {
                    return false;
                }

                if (IsTerrainAboveWater(perimeterPoint, perimeterSample.Height, settings))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
