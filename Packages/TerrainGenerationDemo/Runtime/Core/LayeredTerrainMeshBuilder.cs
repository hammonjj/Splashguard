using System.Collections.Generic;
using UnityEngine;

namespace BitBox.TerrainGeneration.Core
{
    public static class LayeredTerrainMeshBuilder
    {
        public static LayeredTerrainMeshes Build(
            Heightfield heightfield,
            TerrainZoneMap zoneMap,
            float worldSizeX,
            float worldSizeZ,
            TerrainZoneColorPalette palette,
            int smoothingPasses)
        {
            MeshArrays baseArrays = TerrainMeshBuilder.Build(
                heightfield,
                worldSizeX,
                worldSizeZ,
                includeClassificationColors: false);
            Color[] colors = TerrainZoneMeshColorizer.BuildSmoothedColors(zoneMap, palette, smoothingPasses);

            return new LayeredTerrainMeshes(
                BuildLayer(baseArrays, zoneMap, colors, TerrainMeshLayer.Land),
                BuildLayer(baseArrays, zoneMap, colors, TerrainMeshLayer.ShallowWater),
                BuildLayer(baseArrays, zoneMap, colors, TerrainMeshLayer.DeepWater));
        }

        private static MeshArrays BuildLayer(
            MeshArrays baseArrays,
            TerrainZoneMap zoneMap,
            Color[] colors,
            TerrainMeshLayer layer)
        {
            var triangles = new List<int>(baseArrays.Triangles.Length);

            for (int z = 0; z < zoneMap.Depth - 1; z++)
            {
                for (int x = 0; x < zoneMap.Width - 1; x++)
                {
                    TerrainMeshLayer quadLayer = ClassifyQuadLayer(zoneMap, x, z);
                    if (quadLayer != layer)
                    {
                        continue;
                    }

                    int i0 = zoneMap.IndexOf(x, z);
                    int i1 = i0 + 1;
                    int i2 = i0 + zoneMap.Width;
                    int i3 = i2 + 1;

                    triangles.Add(i0);
                    triangles.Add(i2);
                    triangles.Add(i1);
                    triangles.Add(i1);
                    triangles.Add(i2);
                    triangles.Add(i3);
                }
            }

            return new MeshArrays(
                baseArrays.Vertices,
                triangles.ToArray(),
                baseArrays.Uvs,
                colors);
        }

        private static TerrainMeshLayer ClassifyQuadLayer(TerrainZoneMap zoneMap, int x, int z)
        {
            int land = 0;
            int shallow = 0;
            int deep = 0;
            Count(zoneMap.GetZone(x, z), ref land, ref shallow, ref deep);
            Count(zoneMap.GetZone(x + 1, z), ref land, ref shallow, ref deep);
            Count(zoneMap.GetZone(x, z + 1), ref land, ref shallow, ref deep);
            Count(zoneMap.GetZone(x + 1, z + 1), ref land, ref shallow, ref deep);

            if (land >= shallow && land >= deep)
            {
                return TerrainMeshLayer.Land;
            }

            return shallow >= deep ? TerrainMeshLayer.ShallowWater : TerrainMeshLayer.DeepWater;
        }

        private static void Count(TerrainZone zone, ref int land, ref int shallow, ref int deep)
        {
            if (zone == TerrainZone.DeepWater)
            {
                deep++;
            }
            else if (zone == TerrainZone.ShallowWater)
            {
                shallow++;
            }
            else
            {
                land++;
            }
        }
    }
}
