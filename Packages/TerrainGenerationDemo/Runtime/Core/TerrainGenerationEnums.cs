namespace BitBox.TerrainGeneration.Core
{
    public enum TerrainNoiseMode
    {
        Smooth = 0,
        Ridged = 1
    }

    public enum TerrainMaskMode
    {
        None = 0,
        Radial = 1,
        DistanceToEdge = 2,
        Archipelago = 3
    }

    public enum MultiIslandBlendMode
    {
        Max = 0,
        SumClamp = 1,
        SmoothUnion = 2
    }
}
