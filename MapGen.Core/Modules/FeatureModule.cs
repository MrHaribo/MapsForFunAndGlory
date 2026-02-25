using System;
using System.Collections.Generic;
using System.Text;

namespace MapGen.Core.Modules
{
    public static class FeatureModule
    {
        public static void MarkupGrid(MapData data)
        {
            int cellsCount = data.Cells.Length;
            data.DistanceField = new sbyte[cellsCount];
            data.FeatureIds = new ushort[cellsCount];
            data.Features = new List<MapFeature> { null }; // Index 0 is empty/padding like JS

            Queue<int> queue = new Queue<int>();
            ushort nextFeatureId = 1;

            // 1. Identify Features using Flood Fill
            for (int startCell = 0; startCell < cellsCount; startCell++)
            {
                if (data.FeatureIds[startCell] != MapConstants.UNMARKED) continue;

                ushort currentFeatureId = nextFeatureId++;
                bool isLand = data.Cells[startCell].H >= MapConstants.LAND_THRESHOLD;
                bool isBorder = false;

                queue.Enqueue(startCell);
                data.FeatureIds[startCell] = currentFeatureId;

                while (queue.Count > 0)
                {
                    int cellId = queue.Dequeue();
                    if (!isBorder && data.Cells[cellId].B == 1) isBorder = true;

                    foreach (int neighborId in data.Cells[cellId].C)
                    {
                        bool isNeighborLand = data.Cells[neighborId].H >= MapConstants.LAND_THRESHOLD;

                        if (isLand == isNeighborLand && data.FeatureIds[neighborId] == MapConstants.UNMARKED)
                        {
                            data.FeatureIds[neighborId] = currentFeatureId;
                            queue.Enqueue(neighborId);
                        }
                        else if (isLand && !isNeighborLand)
                        {
                            // Boundary detected: Mark initial coast distance
                            data.DistanceField[cellId] = MapConstants.LAND_COAST;
                            data.DistanceField[neighborId] = MapConstants.WATER_COAST;
                        }
                    }
                }

                var type = isLand ? FeatureType.Island : (isBorder ? FeatureType.Ocean : FeatureType.Lake);
                data.Features.Add(new MapFeature { Id = currentFeatureId, IsLand = isLand, IsBorder = isBorder, Type = type });
            }

            // 2. Markup Deep Water (Distance Field Propagation)
            MarkupDistance(data, MapConstants.DEEP_WATER, -1, -10);

            // Sync back to individual cells for easy access
            for (int i = 0; i < cellsCount; i++)
            {
                data.Cells[i].FeatureId = data.FeatureIds[i];
                data.Cells[i].Distance = data.DistanceField[i];
            }
        }

        private static void MarkupDistance(MapData data, sbyte start, sbyte increment, sbyte limit)
        {
            for (sbyte dist = start; dist != limit; dist += increment)
            {
                int marked = 0;
                sbyte prevDist = (sbyte)(dist - increment);

                for (int i = 0; i < data.Cells.Length; i++)
                {
                    if (data.DistanceField[i] != prevDist) continue;

                    foreach (int n in data.Cells[i].C)
                    {
                        if (data.DistanceField[n] == MapConstants.UNMARKED)
                        {
                            data.DistanceField[n] = dist;
                            marked++;
                        }
                    }
                }
                if (marked == 0) break;
            }
        }
    }
}
