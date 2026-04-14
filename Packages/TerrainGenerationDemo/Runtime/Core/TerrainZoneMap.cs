using System;

namespace BitBox.TerrainGeneration.Core
{
    public sealed class TerrainZoneMap
    {
        public TerrainZoneMap(int width, int depth, TerrainZone[] zones)
        {
            if (width < TerrainGenerationRequest.MinResolution)
            {
                throw new ArgumentOutOfRangeException(nameof(width));
            }

            if (depth < TerrainGenerationRequest.MinResolution)
            {
                throw new ArgumentOutOfRangeException(nameof(depth));
            }

            if (zones == null)
            {
                throw new ArgumentNullException(nameof(zones));
            }

            if (zones.Length != width * depth)
            {
                throw new ArgumentException("Zone array length must equal width * depth.", nameof(zones));
            }

            Width = width;
            Depth = depth;
            Zones = zones;
        }

        public int Width { get; }
        public int Depth { get; }
        public TerrainZone[] Zones { get; }

        public int IndexOf(int x, int z)
        {
            return z * Width + x;
        }

        public TerrainZone GetZone(int x, int z)
        {
            return Zones[IndexOf(x, z)];
        }
    }
}
