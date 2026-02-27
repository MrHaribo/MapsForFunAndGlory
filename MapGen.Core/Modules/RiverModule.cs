using System;
using System.Collections.Generic;
using System.Linq;

namespace MapGen.Core.Modules
{
    public static class RiverModule
    {
        #region Generate

        public static void Generate(MapPack pack, MapData grid, bool allowErosion = true)
        {
            var cells = pack.Cells;
            var features = pack.Features;
            int cellsCount = cells.Length;

            // Scoped tracking objects
            var riversData = new Dictionary<int, List<int>>();
            var riverParents = new Dictionary<int, int>();
            ushort riverNext = 1;

            // 1. Initial Height modification (JS: alterHeights)
            double[] h = AlterHeights(pack);

            // 2. Hydrological preprocessing
            LakeModule.DetectCloseLakes(pack, h);
            ResolveDepressions(pack, h);

            // 3. Core Simulation
            DrainWater();
            DefineRivers();
            CalculateConfluenceFlux();
            LakeModule.CleanupLakeData(pack);

            // 4. Erosion / Finalize
            if (allowErosion)
            {
                // Apply modified heights back to the cell data
                for (int i = 0; i < cellsCount; i++) cells[i].H = (byte)Math.Clamp(h[i], 0, 255);
                DowncutRivers(pack);
            }

            // --- Local Functions (Closures) ---

            void AddCellToRiver(int cell, int riverId)
            {
                if (!riversData.ContainsKey(riverId)) riversData[riverId] = new List<int>();
                riversData[riverId].Add(cell);
            }

            void DrainWater()
            {
                const int MIN_FLUX_TO_FORM_RIVER = 30;
                // Simplified modifier calculation
                double cellsNumberModifier = Math.Pow(cellsCount / 10000.0, 0.25);

                // Sort land cells by height descending
                var land = Enumerable.Range(0, cellsCount)
                    .Where(i => h[i] >= 20)
                    .OrderByDescending(i => h[i])
                    .ToList();

                var lakeOutCells = LakeModule.DefineClimateData(pack, grid, h);

                foreach (int i in land)
                {
                    // Add precipitation flux (from grid data)
                    byte prec = grid.Cells[pack.Cells[i].GridId].Prec;
                    cells[i].Flux += (ushort)(prec / cellsNumberModifier);

                    // Handle Lakes as water sources
                    var lakes = lakeOutCells.ContainsKey(i)
                        ? features.Where(f => i == f.OutCell && f.Flux > f.Evaporation).ToList()
                        : new List<MapFeature>();

                    foreach (var lake in lakes)
                    {
                        int lakeCell = cells[i].C.Find(c => h[c] < 20 && cells[c].FeatureId == lake.Id);
                        cells[lakeCell].Flux += (ushort)Math.Max(lake.Flux - lake.Evaporation, 0);

                        // Handle river continuity through lakes
                        if (cells[lakeCell].RiverId != lake.RiverId)
                        {
                            bool sameRiver = cells[lakeCell].C.Any(c => cells[c].RiverId == lake.RiverId);
                            if (sameRiver)
                            {
                                cells[lakeCell].RiverId = lake.RiverId;
                                AddCellToRiver(lakeCell, lake.RiverId);
                            }
                            else
                            {
                                cells[lakeCell].RiverId = riverNext;
                                AddCellToRiver(lakeCell, riverNext);
                                riverNext++;
                            }
                        }
                        lake.RiverId = cells[lakeCell].RiverId;
                        FlowDown(i, cells[lakeCell].Flux, lake.RiverId);
                    }

                    // Pour water out if near border
                    if (cells[i].B == 1 && cells[i].RiverId > 0)
                    {
                        AddCellToRiver(-1, cells[i].RiverId);
                        continue;
                    }

                    // Determine Downhill path (min height)
                    int min;
                    if (lakeOutCells.ContainsKey(i))
                    {
                        var lakeIds = lakes.Select(l => l.Id).ToList();
                        min = cells[i].C.Where(c => !lakeIds.Contains(cells[c].FeatureId))
                                       .OrderBy(c => h[c]).FirstOrDefault();
                    }
                    else if (cells[i].Haven > 0)
                    {
                        min = cells[i].Haven;
                    }
                    else
                    {
                        min = cells[i].C.OrderBy(c => h[c]).FirstOrDefault();
                    }

                    if (h[i] <= h[min]) continue; // Depressed

                    if (cells[i].Flux < MIN_FLUX_TO_FORM_RIVER)
                    {
                        if (h[min] >= 20) cells[min].Flux += cells[i].Flux;
                        continue;
                    }

                    // Proclaim new river if none exists
                    if (cells[i].RiverId == 0)
                    {
                        cells[i].RiverId = riverNext;
                        AddCellToRiver(i, riverNext);
                        riverNext++;
                    }

                    FlowDown(min, cells[i].Flux, cells[i].RiverId);
                }
            }

            void FlowDown(int toCell, int fromFlux, ushort riverId)
            {
                int toFlux = cells[toCell].Flux - cells[toCell].Confluence;
                ushort toRiver = cells[toCell].RiverId;

                if (toRiver != 0)
                {
                    if (fromFlux > toFlux)
                    {
                        cells[toCell].Confluence += (byte)cells[toCell].Flux;
                        if (h[toCell] >= 20) riverParents[toRiver] = riverId;
                        cells[toCell].RiverId = riverId;
                    }
                    else
                    {
                        cells[toCell].Confluence += (byte)fromFlux;
                        if (h[toCell] >= 20) riverParents[riverId] = toRiver;
                    }
                }
                else
                {
                    cells[toCell].RiverId = riverId;
                }

                if (h[toCell] < 20)
                {
                    var waterBody = features[cells[toCell].FeatureId];
                    if (waterBody.Type == FeatureType.Lake)
                    {
                        if (waterBody.RiverId == 0 || fromFlux > waterBody.EnteringFlux)
                        {
                            waterBody.RiverId = riverId;
                            waterBody.EnteringFlux = fromFlux;
                        }
                        waterBody.Flux += fromFlux;
                        waterBody.Inlets.Add(riverId);
                    }
                }
                else
                {
                    cells[toCell].Flux += (ushort)fromFlux;
                }
                AddCellToRiver(toCell, riverId);
            }

            // JS logic for downcutting (erosion)
            void DowncutRivers(MapPack pack)
            {
                const int MAX_DOWNCUT = 5;
                for (int i = 0; i < cellsCount; i++)
                {
                    if (cells[i].H < 35 || cells[i].Flux == 0) continue;
                    var higherCells = cells[i].C.Where(c => cells[c].H > cells[i].H).ToList();
                    if (higherCells.Count == 0) continue;

                    double higherFlux = higherCells.Average(c => (double)cells[c].Flux);
                    if (higherFlux == 0) continue;

                    int downcut = (int)Math.Floor(cells[i].Flux / higherFlux);
                    if (downcut > 0)
                        cells[i].H = (byte)Math.Max(cells[i].H - Math.Min(downcut, MAX_DOWNCUT), 20);
                }
            }

            // Logic for defining River objects (Metadata)
            void DefineRivers()
            {
                // 1. Re-initialize arrays to ensure we only have data from the final simulation
                foreach (var cell in cells)
                {
                    cell.RiverId = 0;
                    cell.Confluence = 0;
                }
                pack.Rivers = new List<MapRiver>();

                double cellsFactor = Math.Pow(cellsCount / 10000.0, 0.25);
                double defaultWidthFactor = Math.Round(1.0 / cellsFactor, 2);
                double mainStemWidthFactor = defaultWidthFactor * 1.2;

                foreach (var kvp in riversData)
                {
                    var riverCells = kvp.Value;
                    if (riverCells.Count < 3) continue;

                    int riverId = kvp.Key;

                    // 2. Mark real confluences and re-assign final river IDs to cells
                    foreach (int cellIdx in riverCells)
                    {
                        if (cellIdx < 0 || cells[cellIdx].H < 20) continue;

                        if (cells[cellIdx].RiverId != 0 && cells[cellIdx].RiverId != riverId)
                            cells[cellIdx].Confluence = 1; // It's a junction
                        else
                            cells[cellIdx].RiverId = (ushort)riverId;
                    }

                    int source = riverCells[0];
                    int mouth = riverCells[riverCells.Count - 2]; // Last land cell
                    riverParents.TryGetValue(riverId, out int parentId);

                    // 3. Determine width factor based on hierarchy
                    double widthFactor = (parentId == 0 || parentId == riverId) ? mainStemWidthFactor : defaultWidthFactor;

                    // 4. Generate Geometric Path (The wiggle)
                    var meanderedPoints = AddMeandering(pack, riverCells);

                    // 5. Calculate Physical Attributes
                    double discharge = cells[mouth].Flux;
                    double length = GetApproximateLength(meanderedPoints);
                    double sourceWidth = GetSourceWidth(cells[source].Flux);

                    // Use the formula to get the mouth width
                    double offset = GetOffset(discharge, meanderedPoints.Count, widthFactor, sourceWidth);
                    double width = Math.Round(Math.Pow(offset / 1.5, 1.8), 2);

                    pack.Rivers.Add(new MapRiver
                    {
                        Id = riverId,
                        Source = source,
                        Mouth = mouth,
                        Discharge = discharge,
                        Length = length,
                        Width = width,
                        WidthFactor = widthFactor,
                        SourceWidth = sourceWidth,
                        Parent = parentId,
                        Cells = riverCells
                    });
                }
            }

            void CalculateConfluenceFlux()
            {
                for (int i = 0; i < cellsCount; i++)
                {
                    if (cells[i].Confluence == 0) continue;
                    var sortedInflux = cells[i].C
                        .Where(c => cells[c].RiverId > 0 && h[c] > h[i])
                        .Select(c => (int)cells[c].Flux)
                        .OrderByDescending(f => f)
                        .ToList();

                    // Skip the first (main) influx, add the rest to confluence property
                    cells[i].Confluence = (byte)sortedInflux.Skip(1).Sum();
                }
            }
        }

        private static double[] AlterHeights(MapPack pack)
        {
            return pack.Cells.Select((c, i) => {
                if (c.H < 20 || c.Distance < 1) return (double)c.H;
                double meanDist = c.C.Average(n => (double)pack.Cells[n].Distance);
                return c.H + (c.Distance / 100.0) + (meanDist / 10000.0);
            }).ToArray();
        }

        #endregion

        #region Resolve Depressions

        public static void ResolveDepressions(MapPack pack, double[] h)
        {
            var cells = pack.Cells;
            var features = pack.Features;

            // Configurable iterations (matching JS defaults)
            int maxIterations = 500;
            int checkLakeMaxIteration = (int)(maxIterations * 0.85);
            int elevateLakeMaxIteration = (int)(maxIterations * 0.75);

            double GetHeight(int i) => features[cells[i].FeatureId - 1].Type == FeatureType.Lake
                ? features[cells[i].FeatureId].Height
                : h[i];

            var lakes = features.Where(f => f.Type == FeatureType.Lake).ToList();
            var land = Enumerable.Range(0, cells.Length)
                .Where(i => h[i] >= 20 && cells[i].B == 0)
                .OrderBy(i => h[i])
                .ToList();

            List<int> progress = new List<int>();
            int depressions = int.MaxValue;
            int? prevDepressions = null;

            for (int iteration = 0; depressions > 0 && iteration < maxIterations; iteration++)
            {
                // Abort if progress is stalling/reversing (sum of last 5 diffs > 0)
                if (progress.Count > 5 && progress.TakeLast(5).Sum() > 0)
                {
                    // Reset to original-ish state and break to avoid infinite loops
                    double[] original = RiverModule.AlterHeights(pack);
                    Array.Copy(original, h, h.Length);
                    break;
                }

                depressions = 0;

                // 1. Resolve Lake Depressions
                if (iteration < checkLakeMaxIteration)
                {
                    foreach (var l in lakes)
                    {
                        if (l.IsClosed) continue;
                        double minShoreHeight = l.Shoreline.Min(s => h[s]);
                        if (minShoreHeight >= 100 || l.Height > minShoreHeight) continue;

                        if (iteration > elevateLakeMaxIteration)
                        {
                            foreach (int i in l.Shoreline) h[i] = cells[i].H; // Reset to raw
                            l.Height = l.Shoreline.Min(s => h[s]) - 1;
                            l.IsClosed = true;
                            continue;
                        }

                        depressions++;
                        l.Height = minShoreHeight + 0.2;
                    }
                }

                // 2. Resolve Land Depressions
                foreach (int i in land)
                {
                    double minNeighborHeight = cells[i].C.Min(c => GetHeight(c));
                    if (minNeighborHeight >= 100 || h[i] > minNeighborHeight) continue;

                    depressions++;
                    h[i] = minNeighborHeight + 0.1;
                }

                if (prevDepressions.HasValue) progress.Add(depressions - prevDepressions.Value);
                prevDepressions = depressions;
            }
        }

        #endregion

        #region Meandering

        public static List<PointFlux> AddMeandering(MapPack pack, List<int> riverCells, double meandering = 0.5)
        {
            var cells = pack.Cells;
            var meandered = new List<PointFlux>();
            int lastStep = riverCells.Count - 1;

            // Convert cell IDs to Coordinates
            var points = GetRiverPoints(pack, riverCells);
            int step = pack.Cells[riverCells[0]].H < 20 ? 1 : 10;

            for (int i = 0; i <= lastStep; i++, step++)
            {
                int cell = riverCells[i];
                var p1 = points[i];

                // Add the actual cell center first
                meandered.Add(new PointFlux(p1.X, p1.Y, cells[cell >= 0 ? cell : riverCells[i - 1]].Flux));

                if (i == lastStep) break;

                int nextCell = riverCells[i + 1];
                var p2 = points[i + 1];

                if (nextCell == -1) // Flowing off-map
                {
                    meandered.Add(new PointFlux(p2.X, p2.Y, cells[riverCells[i]].Flux));
                    break;
                }

                double dist2 = Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2);
                if (dist2 <= 25 && riverCells.Count >= 6) continue;

                // Calculate Meander offset
                double meander = meandering + 1.0 / step + Math.Max(meandering - step / 100.0, 0);
                double angle = Math.Atan2(p2.Y - p1.Y, p2.X - p1.X);
                double sinMeander = Math.Sin(angle) * meander;
                double cosMeander = Math.Cos(angle) * meander;

                if (step < 20 && (dist2 > 64 || (dist2 > 36 && riverCells.Count < 5)))
                {
                    // Add two intermediate points at 1/3 and 2/3
                    meandered.Add(new PointFlux((p1.X * 2 + p2.X) / 3.0 - sinMeander, (p1.Y * 2 + p2.Y) / 3.0 + cosMeander, 0));
                    meandered.Add(new PointFlux((p1.X + p2.X * 2) / 3.0 + sinMeander / 2.0, (p1.Y + p2.Y * 2) / 3.0 - cosMeander / 2.0, 0));
                }
                else if (dist2 > 25 || riverCells.Count < 6)
                {
                    // Add one midpoint
                    meandered.Add(new PointFlux((p1.X + p2.X) / 2.0 - sinMeander, (p1.Y + p2.Y) / 2.0 + cosMeander, 0));
                }
            }
            return meandered;
        }

        #endregion

        #region Geometric Functions

        public static double GetOffset(double flux, int pointIndex, double widthFactor, double startingWidth)
        {
            if (pointIndex == 0) return startingWidth;

            double fluxWidth = Math.Min(Math.Pow(flux, 0.7) / MapConstants.RIVER_FLUX_FACTOR, MapConstants.RIVER_MAX_FLUX_WIDTH);

            double progValue = (pointIndex < MapConstants.RIVER_LENGTH_PROGRESSION.Length)
                ? MapConstants.RIVER_LENGTH_PROGRESSION[pointIndex]
                : MapConstants.RIVER_LENGTH_PROGRESSION.Last();

            double lengthWidth = (pointIndex / MapConstants.RIVER_LENGTH_FACTOR) + (progValue / MapConstants.RIVER_LENGTH_FACTOR);

            return widthFactor * (lengthWidth + fluxWidth) + startingWidth;
        }

        public static double GetSourceWidth(double flux)
        {
            return Math.Round(Math.Min(Math.Pow(flux, 0.9) / MapConstants.RIVER_FLUX_FACTOR, 1.0), 2);
        }

        private static List<MapPoint> GetRiverPoints(MapPack pack, List<int> riverCells)
        {
            return riverCells.Select((cell, i) => {
                if (cell == -1) return GetBorderPoint(pack, riverCells[i - 1]);

                // Use the pack-level coordinate array
                return pack.Points[cell];
            }).ToList();
        }

        private static MapPoint GetBorderPoint(MapPack pack, int cellIndex)
        {
            var p = pack.Points[cellIndex];
            double[] dists = { p.Y, pack.Height - p.Y, p.X, pack.Width - p.X };
            double min = dists.Min();

            if (min == p.Y) return new MapPoint(p.X, 0);
            if (min == pack.Height - p.Y) return new MapPoint(p.X, pack.Height);
            if (min == p.X) return new MapPoint(0, p.Y);
            return new MapPoint(pack.Width, p.Y);
        }

        private static double GetApproximateLength(List<PointFlux> points)
        {
            double length = 0;
            for (int i = 1; i < points.Count; i++)
            {
                double dx = points[i].X - points[i - 1].X;
                double dy = points[i].Y - points[i - 1].Y;
                length += Math.Sqrt(dx * dx + dy * dy);
            }
            return Math.Round(length, 2);
        }

        #endregion
    }
}
