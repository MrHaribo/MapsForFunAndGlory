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

        public static void DetectCloseLakes(MapPack pack, double[] h)
        {
            var cells = pack.Cells;
            // Standard Azgaar default for lake elevation limit
            double elevationLimit = 10;

            foreach (var feature in pack.Features)
            {
                if (feature.Type != FeatureType.Lake) continue;
                feature.IsClosed = false;

                double maxElevation = feature.Height + elevationLimit;
                if (maxElevation > 99) continue;

                bool isDeep = true;
                int lowestShoreCell = feature.Shoreline.OrderBy(s => h[s]).First();

                var queue = new Queue<int>();
                queue.Enqueue(lowestShoreCell);

                var checkedCells = new HashSet<int>();
                checkedCells.Add(lowestShoreCell);

                while (queue.Count > 0 && isDeep)
                {
                    int cellId = queue.Dequeue();

                    foreach (int n in cells[cellId].C)
                    {
                        if (checkedCells.Contains(n) || h[n] >= maxElevation) continue;

                        if (h[n] < MapConstants.LAND_THRESHOLD) // Water found
                        {
                            var nFeature = pack.GetFeature(cells[n].FeatureId);
                            if (nFeature.Type == FeatureType.Ocean || feature.Height > nFeature.Height)
                            {
                                isDeep = false; // Found an outlet path
                                break;
                            }
                        }

                        checkedCells.Add(n);
                        queue.Enqueue(n);
                    }
                }
                feature.IsClosed = isDeep;
            }
        }

        public static Dictionary<int, int> DefineClimateData(MapPack pack, MapData grid, double[] h)
        {
            var lakeOutCells = new Dictionary<int, int>();
            double heightExponent = 1.8;

            foreach (var feature in pack.Features)
            {
                // Only process lakes
                if (feature.Type != FeatureType.Lake) continue;

                // 1. Calculate incoming water (Flux) from precipitation on the shoreline
                feature.Flux = feature.Shoreline.Sum(c => (double)grid.Cells[pack.Cells[c].GridId].Prec);

                // 2. Calculate average Temperature
                if (feature.Vertices.Count < 6)
                    feature.Temp = grid.Cells[pack.Cells[feature.FirstCell].GridId].Temp;
                else
                    feature.Temp = feature.Shoreline.Average(c => (double)grid.Cells[pack.Cells[c].GridId].Temp);

                // 3. Calculate Evaporation (Water loss)
                double heightInMeters = Math.Pow(Math.Max(0, feature.Height - 18), heightExponent);
                double evaporation = ((700 * (feature.Temp + 0.006 * heightInMeters)) / 50 + 75) / (80 - feature.Temp);
                feature.Evaporation = NumberUtils.Round(evaporation * feature.Vertices.Count);

                // 4. Determine Outlet (if not a closed/endorheic lake)
                if (feature.IsClosed) continue;

                // The lowest cell on the shore is where the river will start
                feature.OutCell = feature.Shoreline.OrderBy(s => h[s]).First();
                lakeOutCells[feature.OutCell] = feature.Id;
            }

            return lakeOutCells;
        }

        public static void CleanupLakeData(MapPack pack)
        {
            foreach (var feature in pack.Features)
            {
                if (feature.Type != FeatureType.Lake) continue;

                feature.Height = NumberUtils.Round(feature.Height, 3);

                // Clean up Inlets: keep only those that exist in the finalized pack.Rivers list
                if (feature.Inlets != null)
                {
                    feature.Inlets = feature.Inlets
                        .Where(rId => pack.Rivers.Any(river => river.Id == rId))
                        .ToList();

                    if (feature.Inlets.Count == 0) feature.Inlets = null;
                }

                // Validate outlet river
                if (feature.RiverId > 0 && !pack.Rivers.Any(r => r.Id == feature.RiverId))
                {
                    feature.RiverId = 0;
                }
            }
        }
    }
}
