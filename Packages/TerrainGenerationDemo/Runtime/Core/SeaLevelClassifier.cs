namespace BitBox.TerrainGeneration.Core
{
    public static class SeaLevelClassifier
    {
        public static bool IsLand(float height, float seaLevel)
        {
            return height >= seaLevel;
        }

        public static bool IsWater(float height, float seaLevel)
        {
            return !IsLand(height, seaLevel);
        }
    }
}
