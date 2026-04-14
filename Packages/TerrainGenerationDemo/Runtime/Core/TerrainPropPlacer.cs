using System.Collections.Generic;
using UnityEngine;

namespace BitBox.TerrainGeneration.Core
{
    public static class TerrainPropPlacer
    {
        public static TerrainPropPlacement[] GeneratePlacements(
            Heightfield heightfield,
            TerrainZoneMap zoneMap,
            TerrainGenerationRequest request,
            TerrainPropPlacementSettings settings)
        {
            var placements = new List<TerrainPropPlacement>();
            var random = new DeterministicRandom(request.Seed ^ unchecked((int)0x71f4a7c1));

            float stepX = request.WorldSizeX / Mathf.Max(1, heightfield.Width - 1);
            float stepZ = request.WorldSizeZ / Mathf.Max(1, heightfield.Depth - 1);
            float cellArea = stepX * stepZ;

            for (int z = 1; z < heightfield.Depth - 1; z++)
            {
                for (int x = 1; x < heightfield.Width - 1; x++)
                {
                    TerrainZone zone = zoneMap.GetZone(x, z);
                    if (zone == TerrainZone.DeepWater || zone == TerrainZone.ShallowWater)
                    {
                        continue;
                    }

                    float height = heightfield.GetHeight(x, z);
                    if (height < heightfield.SeaLevel)
                    {
                        continue;
                    }

                    float slope = TerrainZoneClassifier.CalculateSlope(heightfield, x, z, request.WorldSizeX, request.WorldSizeZ);
                    TryPlace(TerrainPropType.Tree, zone, slope, heightfield, request, settings, cellArea, x, z, ref random, placements);
                    TryPlace(TerrainPropType.GrassPatch, zone, slope, heightfield, request, settings, cellArea, x, z, ref random, placements);
                    TryPlace(TerrainPropType.Rock, zone, slope, heightfield, request, settings, cellArea, x, z, ref random, placements);
                    TryPlace(TerrainPropType.Driftwood, zone, slope, heightfield, request, settings, cellArea, x, z, ref random, placements);
                }
            }

            return placements.ToArray();
        }

        private static void TryPlace(
            TerrainPropType type,
            TerrainZone zone,
            float slope,
            Heightfield heightfield,
            TerrainGenerationRequest request,
            TerrainPropPlacementSettings settings,
            float cellArea,
            int x,
            int z,
            ref DeterministicRandom random,
            List<TerrainPropPlacement> placements)
        {
            if (!IsZoneAllowed(type, zone) || !IsSlopeAllowed(type, slope, settings))
            {
                return;
            }

            float density = settings.GetDensity(type);
            if (density <= 0f)
            {
                return;
            }

            float probability = Mathf.Clamp01(density * cellArea / 1000f);
            if (random.NextFloat() > probability)
            {
                return;
            }

            Vector3 position = CalculateWorldPosition(heightfield, request, x, z);
            float minSpacing = settings.GetMinSpacing(type);
            if (!HasSpacing(type, position, minSpacing, placements))
            {
                return;
            }

            float yaw = random.Range(0f, 360f);
            float scale = random.Range(settings.MinScale, settings.MaxScale);
            placements.Add(new TerrainPropPlacement(type, position, yaw, scale, zone));
        }

        private static bool IsZoneAllowed(TerrainPropType type, TerrainZone zone)
        {
            switch (type)
            {
                case TerrainPropType.Tree:
                case TerrainPropType.GrassPatch:
                    return zone == TerrainZone.Grassland;
                case TerrainPropType.Rock:
                    return zone == TerrainZone.Rock || zone == TerrainZone.Mountain;
                case TerrainPropType.Driftwood:
                    return zone == TerrainZone.Beach;
                default:
                    return false;
            }
        }

        private static bool IsSlopeAllowed(TerrainPropType type, float slope, TerrainPropPlacementSettings settings)
        {
            switch (type)
            {
                case TerrainPropType.Tree:
                    return slope <= settings.TreeMaxSlope;
                case TerrainPropType.GrassPatch:
                    return slope <= settings.GrassPatchMaxSlope;
                case TerrainPropType.Driftwood:
                    return slope <= settings.DriftwoodMaxSlope;
                case TerrainPropType.Rock:
                    return true;
                default:
                    return false;
            }
        }

        private static Vector3 CalculateWorldPosition(
            Heightfield heightfield,
            TerrainGenerationRequest request,
            int x,
            int z)
        {
            float stepX = request.WorldSizeX / Mathf.Max(1, heightfield.Width - 1);
            float stepZ = request.WorldSizeZ / Mathf.Max(1, heightfield.Depth - 1);
            return new Vector3(
                -request.WorldSizeX * 0.5f + x * stepX,
                heightfield.GetHeight(x, z),
                -request.WorldSizeZ * 0.5f + z * stepZ);
        }

        private static bool HasSpacing(
            TerrainPropType type,
            Vector3 position,
            float minSpacing,
            List<TerrainPropPlacement> placements)
        {
            if (minSpacing <= 0f)
            {
                return true;
            }

            float minSqr = minSpacing * minSpacing;
            for (int i = 0; i < placements.Count; i++)
            {
                TerrainPropPlacement existing = placements[i];
                if (existing.Type != type)
                {
                    continue;
                }

                var existingXZ = new Vector2(existing.Position.x, existing.Position.z);
                var candidateXZ = new Vector2(position.x, position.z);
                if ((candidateXZ - existingXZ).sqrMagnitude < minSqr)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
