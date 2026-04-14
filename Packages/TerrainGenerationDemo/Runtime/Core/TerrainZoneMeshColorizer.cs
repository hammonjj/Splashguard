using UnityEngine;

namespace BitBox.TerrainGeneration.Core
{
    public static class TerrainZoneMeshColorizer
    {
        public static MeshArrays Colorize(MeshArrays arrays, TerrainZoneMap zoneMap, TerrainZoneColorPalette palette)
        {
            Color[] colors = BuildColors(zoneMap, palette, arrays.Vertices.Length);
            return new MeshArrays(arrays.Vertices, arrays.Triangles, arrays.Uvs, colors);
        }

        public static MeshArrays ColorizeSmoothed(
            MeshArrays arrays,
            TerrainZoneMap zoneMap,
            TerrainZoneColorPalette palette,
            int smoothingPasses)
        {
            Color[] colors = BuildSmoothedColors(zoneMap, palette, smoothingPasses, arrays.Vertices.Length);
            return new MeshArrays(arrays.Vertices, arrays.Triangles, arrays.Uvs, colors);
        }

        public static Color[] BuildColors(TerrainZoneMap zoneMap, TerrainZoneColorPalette palette)
        {
            return BuildColors(zoneMap, palette, zoneMap.Zones.Length);
        }

        public static Color[] BuildSmoothedColors(
            TerrainZoneMap zoneMap,
            TerrainZoneColorPalette palette,
            int smoothingPasses)
        {
            return BuildSmoothedColors(zoneMap, palette, smoothingPasses, zoneMap.Zones.Length);
        }

        private static Color[] BuildColors(TerrainZoneMap zoneMap, TerrainZoneColorPalette palette, int colorCount)
        {
            var colors = new Color[colorCount];
            int count = Mathf.Min(colors.Length, zoneMap.Zones.Length);
            for (int i = 0; i < count; i++)
            {
                colors[i] = palette.GetColor(zoneMap.Zones[i]);
            }

            for (int i = count; i < colors.Length; i++)
            {
                colors[i] = palette.Grassland;
            }

            return colors;
        }

        private static Color[] BuildSmoothedColors(
            TerrainZoneMap zoneMap,
            TerrainZoneColorPalette palette,
            int smoothingPasses,
            int colorCount)
        {
            Color[] colors = BuildColors(zoneMap, palette, colorCount);
            smoothingPasses = Mathf.Clamp(smoothingPasses, 0, 8);
            if (smoothingPasses == 0)
            {
                return colors;
            }

            int width = zoneMap.Width;
            int depth = zoneMap.Depth;
            var working = new Color[colors.Length];

            for (int pass = 0; pass < smoothingPasses; pass++)
            {
                for (int z = 0; z < depth; z++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        var sum = Color.clear;
                        int samples = 0;
                        for (int nz = z - 1; nz <= z + 1; nz++)
                        {
                            if (nz < 0 || nz >= depth)
                            {
                                continue;
                            }

                            for (int nx = x - 1; nx <= x + 1; nx++)
                            {
                                if (nx < 0 || nx >= width)
                                {
                                    continue;
                                }

                                int neighborIndex = zoneMap.IndexOf(nx, nz);
                                if (neighborIndex >= colors.Length)
                                {
                                    continue;
                                }

                                sum += colors[neighborIndex];
                                samples++;
                            }
                        }

                        int index = zoneMap.IndexOf(x, z);
                        if (index < working.Length)
                        {
                            working[index] = samples > 0 ? sum / samples : colors[index];
                        }
                    }
                }

                Color[] swap = colors;
                colors = working;
                working = swap;
            }

            return colors;
        }
    }
}
