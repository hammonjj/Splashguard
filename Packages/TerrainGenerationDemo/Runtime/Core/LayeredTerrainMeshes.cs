namespace BitBox.TerrainGeneration.Core
{
    public sealed class LayeredTerrainMeshes
    {
        public LayeredTerrainMeshes(MeshArrays land, MeshArrays shallowWater, MeshArrays deepWater)
        {
            Land = land;
            ShallowWater = shallowWater;
            DeepWater = deepWater;
        }

        public MeshArrays Land { get; }
        public MeshArrays ShallowWater { get; }
        public MeshArrays DeepWater { get; }
    }
}
