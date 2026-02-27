using MapGen.Core.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MapGen.Core.Modules
{
    public static class LakeModule
    {
        public static void AddLakesInDeepDepressions(MapData data, int elevationLimit = MapConstants.DEFAULT_LAKE_ELEV_LIMIT)
        {
            if (elevationLimit >= MapConstants.DEFAULT_LAKE_ELEV_LIMIT) return;

            for (int i = 0; i < data.Cells.Length; i++)
            {
                var cell = data.Cells[i];
                if (cell.B == 1 || cell.H < MapConstants.LAND_THRESHOLD) continue;

                double minNeighborH = cell.C.Where(n => n != -1).Min(n => (double)data.Cells[n].H);
                if (cell.H > minNeighborH) continue;

                bool isDeep = true;
                double threshold = cell.H + elevationLimit;
                Queue<int> queue = new Queue<int>();
                HashSet<int> checkedCells = new HashSet<int> { i };
                queue.Enqueue(i);

                while (isDeep && queue.Count > 0)
                {
                    int q = queue.Dequeue();
                    foreach (int n in data.Cells[q].C)
                    {
                        if (n == -1 || checkedCells.Contains(n)) continue;
                        if (data.Cells[n].H >= threshold) continue;

                        if (data.Cells[n].H < MapConstants.LAND_THRESHOLD)
                        {
                            isDeep = false;
                            break;
                        }

                        checkedCells.Add(n);
                        queue.Enqueue(n);
                    }
                }

                if (isDeep)
                {
                    var lakeCells = new List<int> { i };
                    lakeCells.AddRange(cell.C.Where(n => n != -1 && data.Cells[n].H == cell.H));
                    AddLake(data, lakeCells);
                }
            }
        }

        private static void AddLake(MapData data, List<int> lakeCells)
        {
            int newFeatureId = data.Features.Count;
            foreach (int i in lakeCells)
            {
                data.Cells[i].H = MapConstants.LAKE_HEIGHT;
                data.Cells[i].Distance = MapConstants.WATER_COAST;
                data.Cells[i].FeatureId = (ushort)newFeatureId;

                foreach (int n in data.Cells[i].C)
                {
                    if (n != -1 && !lakeCells.Contains(n))
                        data.Cells[n].Distance = MapConstants.LAND_COAST;
                }
            }
            data.Features.Add(new MapFeature { Id = newFeatureId, Type = FeatureType.Lake, IsLand = false });
        }

        public static void OpenNearSeaLakes(MapData data)
        {
            if (data.Template == HeightmapTemplate.Atoll) return;
            if (!data.Features.Any(f => f?.Type == FeatureType.Lake)) return;

            for (int i = 0; i < data.Cells.Length; i++)
            {
                int lakeId = data.Cells[i].FeatureId;
                if (data.Features[lakeId].Type != FeatureType.Lake) continue;

                bool isBreached = false;
                foreach (int c in data.Cells[i].C)
                {
                    if (c == -1) continue;
                    var potentialBreach = data.Cells[c];

                    // If the neighbor is a low-lying coastline cell
                    if (potentialBreach.Distance == MapConstants.LAND_COAST &&
                        potentialBreach.H <= MapConstants.LAKE_BREACH_LIMIT)
                    {
                        foreach (int n in potentialBreach.C)
                        {
                            if (n == -1) continue;
                            int neighborFeatureId = data.Cells[n].FeatureId;

                            // If we find the ocean on the other side of this cell
                            if (data.Features[neighborFeatureId].Type == FeatureType.Ocean)
                            {
                                RemoveLake(data, c, lakeId, neighborFeatureId);
                                isBreached = true;
                                break; // Exit neighbor loop
                            }
                        }
                    }
                    if (isBreached) break; // Exit potential breach loop
                }
            }
        }

        private static void RemoveLake(MapData data, int thresholdId, int lakeId, int oceanId)
        {
            data.Cells[thresholdId].H = MapConstants.LAKE_HEIGHT;
            data.Cells[thresholdId].Distance = MapConstants.WATER_COAST;
            data.Cells[thresholdId].FeatureId = (ushort)oceanId;

            foreach (int n in data.Cells[thresholdId].C)
            {
                if (n != -1 && data.Cells[n].H >= MapConstants.LAND_THRESHOLD)
                    data.Cells[n].Distance = MapConstants.LAND_COAST;
            }

            // Convert the entire lake feature into the ocean feature
            foreach (var cell in data.Cells)
            {
                if (cell.FeatureId == lakeId) cell.FeatureId = (ushort)oceanId;
            }
            data.Features[lakeId].Type = FeatureType.Ocean;
        }

        public static double GetHeight(MapPack pack, MapFeature feature)
        {
            if (feature.Shoreline == null || feature.Shoreline.Count == 0)
                return 20.0; // Default matching JS logic

            // Get minimum height from shoreline cells
            double minShoreHeight = feature.Shoreline.Min(cellId => pack.Cells[cellId].H);

            // Match JS: rn(minShoreHeight - 0.01, 2)
            return NumberUtils.Round(minShoreHeight - MapConstants.LAKE_ELEVATION_DELTA, 2);
        }
    }
}
