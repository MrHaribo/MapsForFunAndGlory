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
                if (cell.Border == 1 || cell.Height < MapConstants.LAND_THRESHOLD) continue;

                double minNeighborH = cell.NeighborCells.Where(n => n != -1).Min(n => (double)data.Cells[n].Height);
                if (cell.Height > minNeighborH) continue;

                bool isDeep = true;
                double threshold = cell.Height + elevationLimit;
                Queue<int> queue = new Queue<int>();
                HashSet<int> checkedCells = new HashSet<int> { i };
                queue.Enqueue(i);

                while (isDeep && queue.Count > 0)
                {
                    int q = queue.Dequeue();
                    foreach (int n in data.Cells[q].NeighborCells)
                    {
                        if (n == -1 || checkedCells.Contains(n)) continue;
                        if (data.Cells[n].Height >= threshold) continue;

                        if (data.Cells[n].Height < MapConstants.LAND_THRESHOLD)
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
                    lakeCells.AddRange(cell.NeighborCells.Where(n => n != -1 && data.Cells[n].Height == cell.Height));
                    AddLake(data, lakeCells);
                }
            }
        }

        private static void AddLake(MapData data, List<int> lakeCells)
        {
            int newFeatureId = data.Features.Count;
            foreach (int i in lakeCells)
            {
                data.Cells[i].Height = MapConstants.LAKE_HEIGHT;
                data.Cells[i].Distance = MapConstants.WATER_COAST;
                data.Cells[i].FeatureId = (ushort)newFeatureId;

                foreach (int n in data.Cells[i].NeighborCells)
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
                foreach (int c in data.Cells[i].NeighborCells)
                {
                    if (c == -1) continue;
                    var potentialBreach = data.Cells[c];

                    // If the neighbor is a low-lying coastline cell
                    if (potentialBreach.Distance == MapConstants.LAND_COAST &&
                        potentialBreach.Height <= MapConstants.LAKE_BREACH_LIMIT)
                    {
                        foreach (int n in potentialBreach.NeighborCells)
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
            data.Cells[thresholdId].Height = MapConstants.LAKE_HEIGHT;
            data.Cells[thresholdId].Distance = MapConstants.WATER_COAST;
            data.Cells[thresholdId].FeatureId = (ushort)oceanId;

            foreach (int n in data.Cells[thresholdId].NeighborCells)
            {
                if (n != -1 && data.Cells[n].Height >= MapConstants.LAND_THRESHOLD)
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
            if (feature.ShorelineCells == null || feature.ShorelineCells.Count == 0)
                return 20.0; // Default matching JS logic

            // Get minimum height from shoreline cells
            double minShoreHeight = feature.ShorelineCells.Min(cellId => pack.Cells[cellId].Height);

            // Match JS: rn(minShoreHeight - 0.01, 2)
            return NumberUtils.Round(minShoreHeight - MapConstants.LAKE_ELEVATION_DELTA, 2);
        }

        public static void DetectCloseLakes(IMapGraph map, double[] h)
        {
            var cells = map.Cells;

            foreach (var feature in map.Features)
            {
                if (feature.Type != FeatureType.Lake) continue;
                feature.IsClosed = false;

                double maxElevation = feature.Height + MapConstants.LAKE_ELEVATION_LIMIT;
                if (maxElevation > 99)
                {
                    feature.IsClosed = false;
                    continue;
                }

                // 1. MATCH JS SIDE-EFFECT: Sort the shoreline in-place by height
                feature.ShorelineCells.Sort((a, b) => h[a].CompareTo(h[b]));

                bool isDeep = true;
                int lowestShoreCell = feature.ShorelineCells[0];

                // 2. MATCH JS STACK BEHAVIOR: Use Stack (LIFO) instead of Queue
                var stack = new Stack<int>();
                stack.Push(lowestShoreCell);

                var checkedCells = new HashSet<int>();
                checkedCells.Add(lowestShoreCell);

                while (stack.Count > 0 && isDeep)
                {
                    int cellId = stack.Pop();

                    foreach (int n in cells[cellId].NeighborCells)
                    {
                        if (checkedCells.Contains(n) || h[n] >= maxElevation) continue;

                        if (h[n] < MapConstants.LAND_THRESHOLD) // Water found
                        {
                            var nFeature = map.GetFeature(cells[n].FeatureId);
                            if (nFeature.Type == FeatureType.Ocean || feature.Height > nFeature.Height)
                            {
                                isDeep = false;
                            }
                        }

                        if (isDeep) // Only add to stack if we haven't found an outlet yet
                        {
                            checkedCells.Add(n);
                            stack.Push(n);
                        }
                    }
                }
                feature.IsClosed = isDeep;
            }
        }

        public static Dictionary<int, int> DefineClimateData(IMapGraph map, double[] h)
        {
            var lakeOutCells = new Dictionary<int, int>();

            foreach (var feature in map.Features)
            {
                if (feature.Type != FeatureType.Lake) continue;

                // 1. Flux (Precipitation on shoreline)
                feature.Flux = feature.ShorelineCells.Sum(c => (double)map.Cells[c].Prec);

                // 2. Temperature (Rounded to 1 decimal, threshold based on CellsCount)
                if (feature.CellsCount < 6)
                {
                    feature.Temp = map.Cells[feature.FirstCell].Temp;
                }
                else
                {
                    double avgTemp = feature.ShorelineCells.Average(c => (double)map.Cells[c].Temp);
                    feature.Temp = Math.Round(avgTemp, 1);
                }

                // 3. Evaporation (Multiplier must be CellsCount, result rounded to integer)
                double heightInMeters = Math.Pow(Math.Max(0, feature.Height - 18), MapConstants.LAKE_HEIGHT_EXPONENT);
                double evaporationFactor = ((700 * (feature.Temp + 0.006 * heightInMeters)) / 50 + 75) / (80 - feature.Temp);

                // Match JS: rn(evaporation * lake.cells)
                feature.Evaporation = Math.Round(evaporationFactor * feature.CellsCount);

                // 4. Outlet and Shoreline Mutation
                if (feature.IsClosed) continue;

                // CRITICAL: Sort in-place to match JS side-effect
                // This reorders the actual list on the feature object
                feature.ShorelineCells.Sort((a, b) => h[a].CompareTo(h[b]));

                // After sorting, the lowest cell is at index 0
                feature.OutCell = feature.ShorelineCells[0];
                lakeOutCells[feature.OutCell] = feature.Id;
            }

            return lakeOutCells;
        }

        public static void CleanupLakeData(IMapGraph map)
        {
            if (!(map is MapPack pack))
                return;

            foreach (var feature in map.Features)
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
