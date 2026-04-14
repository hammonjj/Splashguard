using UnityEngine;

namespace BitBox.TerrainGeneration.Core
{
    public readonly struct TerrainPropPlacement
    {
        public readonly TerrainPropType Type;
        public readonly Vector3 Position;
        public readonly float YawDegrees;
        public readonly float Scale;
        public readonly TerrainZone SourceZone;

        public TerrainPropPlacement(
            TerrainPropType type,
            Vector3 position,
            float yawDegrees,
            float scale,
            TerrainZone sourceZone)
        {
            Type = type;
            Position = position;
            YawDegrees = yawDegrees;
            Scale = scale;
            SourceZone = sourceZone;
        }
    }
}
