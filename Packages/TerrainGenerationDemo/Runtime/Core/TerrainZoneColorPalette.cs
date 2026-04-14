using UnityEngine;

namespace BitBox.TerrainGeneration.Core
{
    public readonly struct TerrainZoneColorPalette
    {
        public readonly Color DeepWater;
        public readonly Color ShallowWater;
        public readonly Color Beach;
        public readonly Color Grassland;
        public readonly Color Rock;
        public readonly Color Mountain;

        public TerrainZoneColorPalette(
            Color deepWater,
            Color shallowWater,
            Color beach,
            Color grassland,
            Color rock,
            Color mountain)
        {
            DeepWater = deepWater;
            ShallowWater = shallowWater;
            Beach = beach;
            Grassland = grassland;
            Rock = rock;
            Mountain = mountain;
        }

        public static TerrainZoneColorPalette Default => new(
            deepWater: new Color(0.02f, 0.08f, 0.18f, 1f),
            shallowWater: new Color(0.06f, 0.48f, 0.58f, 1f),
            beach: new Color(0.83f, 0.76f, 0.55f, 1f),
            grassland: new Color(0.22f, 0.54f, 0.22f, 1f),
            rock: new Color(0.42f, 0.42f, 0.40f, 1f),
            mountain: new Color(0.72f, 0.72f, 0.68f, 1f));

        public Color GetColor(TerrainZone zone)
        {
            switch (zone)
            {
                case TerrainZone.DeepWater:
                    return DeepWater;
                case TerrainZone.ShallowWater:
                    return ShallowWater;
                case TerrainZone.Beach:
                    return Beach;
                case TerrainZone.Grassland:
                    return Grassland;
                case TerrainZone.Rock:
                    return Rock;
                case TerrainZone.Mountain:
                    return Mountain;
                default:
                    return Color.magenta;
            }
        }
    }
}
