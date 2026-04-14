using UnityEngine;

namespace BitBox.TerrainGeneration.Core
{
    public readonly struct TerrainPropPlacementSettings
    {
        public readonly float TreeDensity;
        public readonly float RockDensity;
        public readonly float GrassPatchDensity;
        public readonly float DriftwoodDensity;
        public readonly float TreeMaxSlope;
        public readonly float GrassPatchMaxSlope;
        public readonly float DriftwoodMaxSlope;
        public readonly float TreeMinSpacing;
        public readonly float RockMinSpacing;
        public readonly float GrassPatchMinSpacing;
        public readonly float DriftwoodMinSpacing;
        public readonly float MinScale;
        public readonly float MaxScale;

        public TerrainPropPlacementSettings(
            float treeDensity,
            float rockDensity,
            float grassPatchDensity,
            float driftwoodDensity,
            float treeMaxSlope,
            float grassPatchMaxSlope,
            float driftwoodMaxSlope,
            float treeMinSpacing,
            float rockMinSpacing,
            float grassPatchMinSpacing,
            float driftwoodMinSpacing,
            float minScale,
            float maxScale)
        {
            TreeDensity = Mathf.Max(0f, treeDensity);
            RockDensity = Mathf.Max(0f, rockDensity);
            GrassPatchDensity = Mathf.Max(0f, grassPatchDensity);
            DriftwoodDensity = Mathf.Max(0f, driftwoodDensity);
            TreeMaxSlope = Mathf.Max(0f, treeMaxSlope);
            GrassPatchMaxSlope = Mathf.Max(0f, grassPatchMaxSlope);
            DriftwoodMaxSlope = Mathf.Max(0f, driftwoodMaxSlope);
            TreeMinSpacing = Mathf.Max(0f, treeMinSpacing);
            RockMinSpacing = Mathf.Max(0f, rockMinSpacing);
            GrassPatchMinSpacing = Mathf.Max(0f, grassPatchMinSpacing);
            DriftwoodMinSpacing = Mathf.Max(0f, driftwoodMinSpacing);
            MinScale = Mathf.Max(0.01f, minScale);
            MaxScale = Mathf.Max(MinScale, maxScale);
        }

        public static TerrainPropPlacementSettings Default => new(
            treeDensity: 9f,
            rockDensity: 10f,
            grassPatchDensity: 20f,
            driftwoodDensity: 3f,
            treeMaxSlope: 0.32f,
            grassPatchMaxSlope: 0.38f,
            driftwoodMaxSlope: 0.20f,
            treeMinSpacing: 4.5f,
            rockMinSpacing: 3.5f,
            grassPatchMinSpacing: 2.25f,
            driftwoodMinSpacing: 5f,
            minScale: 0.75f,
            maxScale: 1.35f);

        public float GetDensity(TerrainPropType type)
        {
            switch (type)
            {
                case TerrainPropType.Tree:
                    return TreeDensity;
                case TerrainPropType.Rock:
                    return RockDensity;
                case TerrainPropType.GrassPatch:
                    return GrassPatchDensity;
                case TerrainPropType.Driftwood:
                    return DriftwoodDensity;
                default:
                    return 0f;
            }
        }

        public float GetMinSpacing(TerrainPropType type)
        {
            switch (type)
            {
                case TerrainPropType.Tree:
                    return TreeMinSpacing;
                case TerrainPropType.Rock:
                    return RockMinSpacing;
                case TerrainPropType.GrassPatch:
                    return GrassPatchMinSpacing;
                case TerrainPropType.Driftwood:
                    return DriftwoodMinSpacing;
                default:
                    return 0f;
            }
        }
    }
}
