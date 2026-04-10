using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;

namespace MapGen.Core.Modules
{
    public static class RiverModule
    {
        #region Generate

        public static void Generate(MapPack pack, bool allowErosion = true)
        {
            pack.Rivers = GenerateRivers(pack, allowErosion);
        }

        public static List<MapRiver> GenerateRivers(IMapGraph map, bool allowErosion)
        {
            map.Rng.Init(map.Seed);

            var cells = map.Cells;
            var features = map.Features;
            int cellsCount = cells.Length;

            // Scoped tracking objects
            var riversData = new Dictionary<int, List<int>>();
            var riverParents = new Dictionary<int, int>();
            ushort riverNext = 1;

            // 1. Initial Height modification (JS: alterHeights)
            double[] h = AlterHeights(map);

            // 2. Hydrological preprocessing
            LakeModule.DetectCloseLakes(map, h);
            ResolveDepressions(map, h);

            // 3. Core Simulation
            DrainWater();

            var rivers = DefineRivers(map, riversData, riverParents);
            CalculateConfluenceFlux(map, h);
            LakeModule.CleanupLakeData(map);

            // 4. Erosion / Finalize
            if (allowErosion)
            {
                for (int i = 0; i < map.Cells.Length; i++)
                {
                    map.Cells[i].Height = (byte)Math.Floor(h[i]);
                }
                DowncutRivers(map);
            }

            return rivers;

            //if (allowErosion)
            //{
            //    // Apply modified heights back to the cell data
            //    for (int i = 0; i < cellsCount; i++) cells[i].H = (byte)Math.Clamp(h[i], 0, 255);
            //    DowncutRivers(pack, h);
            //}

            // --- Local Functions (Closures) ---

            void AddCellToRiver(int cell, int riverId)
            {
                if (!riversData.ContainsKey(riverId)) riversData[riverId] = new List<int>();
                riversData[riverId].Add(cell);
            }

            void DrainWater()
            {
                double cellsNumberModifier = Math.Pow(map.PointsCount / 10000.0, 0.25);

                // Sorting land by height descending (JS: h[b] - h[a])
                var land = Enumerable.Range(0, cellsCount)
                    .Where(i => h[i] >= 20)
                    .OrderByDescending(i => h[i])
                    .ToList();

                var lakeOutCells = LakeModule.DefineClimateData(map, h);

                foreach (int i in land)
                {
                    // Add precipitation flux
                    byte prec = map.Cells[i].Prec;
                    //tempFlux[i] += prec / cellsNumberModifier;
                    cells[i].Flux += Math.Floor(prec / cellsNumberModifier);

                    // 2. Lake Outlet Logic
                    // In JS, 'lakes' is a list of features where i is the outCell
                    if (lakeOutCells.TryGetValue(i, out int featureId))
                    {
                        var lake = map.GetFeature(featureId);
                        if (lake != null && lake.Flux > lake.Evaporation)
                        {
                            // Find the specific water cell belonging to this lake neighbor
                            int lakeCell = cells[i].NeighborCells.Find(c => h[c] < 20 && cells[c].FeatureId == lake.Id);

                            // Water from lake drains to outlet
                            double lakeDrainage = Math.Max(lake.Flux - lake.Evaporation, 0);
                            cells[lakeCell].Flux += lakeDrainage;

                            // Chain lakes: maintain river identity or proclaim new one
                            if (cells[lakeCell].RiverId != lake.RiverId)
                            {
                                bool sameRiver = cells[lakeCell].NeighborCells.Any(c => cells[c].RiverId == lake.RiverId);
                                if (sameRiver)
                                {
                                    cells[lakeCell].RiverId = lake.RiverId;
                                    AddCellToRiver(lakeCell, lake.RiverId);
                                }
                                else
                                {
                                    cells[lakeCell].RiverId = (ushort)riverNext;
                                    AddCellToRiver(lakeCell, (ushort)riverNext);
                                    riverNext++;
                                }
                            }

                            lake.RiverId = cells[lakeCell].RiverId;

                            // IMPORTANT: Flow water down from the outlet point 'i' 
                            // using the flux that just arrived from the lake
                            FlowDown(i, cells[lakeCell].Flux, lake.RiverId);

                            // Assign tributary parents
                            if (lake.Inlets != null)
                            {
                                foreach (var inletId in lake.Inlets)
                                    riverParents[inletId] = lake.RiverId;
                            }
                        }
                    }

                    // 3. Near-border logic
                    if (cells[i].Border == 1 && cells[i].RiverId > 0)
                    {
                        AddCellToRiver(-1, cells[i].RiverId);
                        continue;
                    }

                    // 4. Determine Downhill (Min)
                    int min = GetLowestNeighbor(i, lakeOutCells.ContainsKey(i) ? (int?)lakeOutCells[i] : null);

                    // Depression check
                    if (h[i] <= h[min]) continue;

                    // 5. River Formation
                    if (cells[i].Flux < MapConstants.MIN_FLUX_TO_FORM_RIVER)
                    {
                        if (h[min] >= 20) cells[min].Flux += cells[i].Flux;
                        continue;
                    }

                    if (cells[i].RiverId == 0)
                    {
                        cells[i].RiverId = (ushort)riverNext;
                        AddCellToRiver(i, (ushort)riverNext);
                        riverNext++;
                    }

                    FlowDown(min, cells[i].Flux, cells[i].RiverId);
                }
            }

            // Updated FlowDown to match JS confluence logic exactly
            void FlowDown(int toCell, double fromFlux, ushort riverId)
            {
                // JS: const toFlux = cells.fl[toCell] - cells.conf[toCell];
                double toFlux = cells[toCell].Flux - cells[toCell].Confluence;
                ushort toRiver = cells[toCell].RiverId;

                if (toRiver != 0)
                {
                    if (fromFlux > toFlux)
                    {
                        // JS: cells.conf[toCell] += cells.fl[toCell];
                        // Note: We use the total flux here to match JS
                        cells[toCell].Confluence = (byte)Math.Min(255, cells[toCell].Confluence + Math.Round(cells[toCell].Flux));

                        if (h[toCell] >= 20) riverParents[toRiver] = riverId;
                        cells[toCell].RiverId = riverId;
                    }
                    else
                    {
                        // JS: cells.conf[toCell] += fromFlux;
                        cells[toCell].Confluence = (byte)Math.Min(255, cells[toCell].Confluence + Math.Round(fromFlux));

                        if (h[toCell] >= 20) riverParents[riverId] = toRiver;
                    }
                }
                else
                {
                    cells[toCell].RiverId = riverId;
                }

                if (h[toCell] < 20)
                {
                    var waterBody = map.GetFeature(cells[toCell].FeatureId);
                    if (waterBody != null && waterBody.Type == FeatureType.Lake)
                    {
                        if (waterBody.RiverId == 0 || fromFlux > waterBody.EnteringFlux)
                        {
                            waterBody.RiverId = riverId;
                            waterBody.EnteringFlux = fromFlux;
                        }
                        waterBody.Flux += fromFlux;
                        // Ensure inlets are unique
                        if (waterBody.Inlets == null) waterBody.Inlets = new List<ushort>();
                        if (!waterBody.Inlets.Contains(riverId)) waterBody.Inlets.Add(riverId);
                    }
                }
                else
                {
                    cells[toCell].Flux += fromFlux;
                }

                AddCellToRiver(toCell, riverId);
            }

            int GetLowestNeighbor(int i, int? excludeLakeId = null)
            {
                var neighbors = map.Cells[i].NeighborCells;

                // 1. Lake Outlet Logic: Filter neighbors first if an exclusion ID is provided.
                // This prevents rivers from flowing back into the lake they just exited.
                if (excludeLakeId.HasValue)
                {
                    var filtered = neighbors.Where(n => map.Cells[n].FeatureId != excludeLakeId.Value).ToList();

                    if (filtered.Count > 0)
                    {
                        // Stable Sort: matches JS Array.sort((a,b) => h[a] - h[b])
                        return filtered
                            .OrderBy(n => h[n])
                            .ThenBy(n => neighbors.IndexOf(n))
                            .First();
                    }

                    // Fallback if all neighbors were filtered (edge case)
                    return neighbors[0];
                }

                // 2. Haven Logic: If no lake filter, check for forced downhill paths.
                // Havens are pre-calculated to guide rivers toward the sea in flat areas.
                if (map.Cells[i].Haven > 0)
                {
                    return map.Cells[i].Haven;
                }

                // 3. Standard Downhill: Find the neighbor with the minimum height.
                // We use the index in the original neighbor list as a tie-breaker 
                // to maintain 1:1 parity with the JS sort stability.
                return neighbors
                    .OrderBy(n => h[n])
                    .ThenBy(n => neighbors.IndexOf(n))
                    .First();
            }
        }

        #endregion

        #region DefineRivers

        // Logic for defining River objects (Metadata)
        public static List<MapRiver> DefineRivers(IMapGraph map, Dictionary<int, List<int>> riversData, Dictionary<int, int> riverParents)
        {
            var rivers = new List<MapRiver>();

            // 1. Reset cell-level river metadata to ensure a clean slate
            foreach (var cell in map.Cells)
            {
                cell.RiverId = 0;
                cell.Confluence = 0;
            }


            // Constants for width scaling based on map density
            // JS: const cellsFactor = Math.pow(pack.cells.i.length / 10000, 0.25);
            double cellsFactor = Math.Pow(map.PointsCount / 10000.0, 0.25);
            double defaultWidthFactor = Math.Round(1.0 / cellsFactor, 2);
            double mainStemWidthFactor = Math.Round(defaultWidthFactor * 1.2, 2);

            foreach (var kvp in riversData)
            {
                var riverCells = kvp.Value;
                // Azgaar requirement: A river must have at least 3 cells to be "visible"
                if (riverCells.Count < 3) continue;

                int riverId = kvp.Key;

                // 2. Map cell-to-river relationship and detect confluences
                foreach (int cellIdx in riverCells)
                {
                    // FIX: Skip invalid indices like -1
                    if (cellIdx < 0 || cellIdx >= map.Cells.Length) continue;

                    var cell = map.Cells[cellIdx];
                    if (cell.Height < MapConstants.LAND_THRESHOLD) continue;

                    // If the cell already has a different RiverId, it's a confluence point
                    if (cell.RiverId != 0 && cell.RiverId != riverId)
                    {
                        cell.Confluence = 1;
                    }
                    else
                    {
                        cell.RiverId = (ushort)riverId;
                    }
                }

                int source = riverCells[0];
                int mouth = riverCells[riverCells.Count - 2]; // Last land cell before water

                riverParents.TryGetValue(riverId, out int parentId);

                // 3. Determine width factor (Main stems are thicker than tributaries)
                double widthFactor = (parentId == 0 || parentId == riverId)
                    ? mainStemWidthFactor
                    : defaultWidthFactor;

                // 4. Generate Geometric Path (Add wiggle/meandering based on terrain)
                // This usually involves looking at the vertices of the Voronoi cells
                var meanderedPoints = AddMeandering(map, riverCells);

                // 5. Calculate Physical Attributes for Rendering/Simulation
                double discharge = map.Cells[mouth].Flux;
                double length = GetApproximateLength(meanderedPoints);
                double sourceWidth = GetSourceWidth(map.Cells[source].Flux);

                // Calculate visual width using the Azgaar power formula
                double offset = GetOffset(discharge, meanderedPoints.Count, widthFactor, sourceWidth);
                double width = Math.Round(Math.Pow(offset / 1.5, 1.8), 2);

                rivers.Add(new MapRiver
                {
                    Id = riverId,
                    Source = source,
                    Mouth = mouth,
                    Discharge = discharge,
                    Length = length,
                    Width = width,
                    WidthFactor = widthFactor,
                    SourceWidth = sourceWidth,
                    Parent = (ushort)parentId,
                    Cells = riverCells
                });
            }

            return rivers;
        }

        public static void CalculateConfluenceFlux(IMapGraph map, double[] h)
        {
            foreach (var cell in map.Cells)
            {
                // Check if there is any confluence marked (JS: if (!cells.conf[i]) continue)
                if (cell.Confluence == 0) continue;

                var sortedInflux = cell.NeighborCells
                    .Select(neighborIdx => map.Cells[neighborIdx])
                    // JS: cells.r[c] && h[c] > h[i]
                    .Where(n => n.RiverId > 0 && h[n.Index] > h[cell.Index])
                    // JS: .map(c => cells.fl[c])
                    .Select(n => n.Flux)
                    // JS: .sort((a, b) => b - a)
                    .OrderByDescending(f => f)
                    .ToList();

                // JS: sortedInflux.reduce((acc, flux, index) => (index ? acc + flux : acc), 0)
                // This sums all elements EXCEPT the first one (index 0).
                if (sortedInflux.Count > 1)
                {
                    cell.Confluence = sortedInflux.Skip(1).Sum();
                }
                else
                {
                    // If only one stream flows in, it's not a confluence visually
                    cell.Confluence = 0;
                }
            }
        }

        public static void DowncutRivers(IMapGraph map)
        {
            var cells = map.Cells;

            // JS: for (const i of pack.cells.i)
            for (int i = 0; i < cells.Length; i++)
            {
                var cell = cells[i];

                // 1. Threshold check: must match JS integer comparison exactly
                // JS: if (cells.h[i] < 35) continue; 
                if (cell.Height < 35 || cell.Flux == 0) continue;

                // 2. Filter higher neighbors using the current state of the byte array
                // JS: const higherCells = cells.c[i].filter(c => cells.h[c] > cells.h[i]);
                byte currentH = cell.Height;
                var higherCellIndices = cell.NeighborCells
                    .Where(nIdx => cells[nIdx].Height > currentH)
                    .ToList();

                if (higherCellIndices.Count == 0) continue;

                // 3. Average Flux calculation
                double sumFlux = 0;
                foreach (int nIdx in higherCellIndices)
                {
                    sumFlux += cells[nIdx].Flux;
                }
                double higherFlux = sumFlux / higherCellIndices.Count;

                if (higherFlux == 0) continue;

                // 4. Calculate integer downcut amount
                // JS: const downcut = Math.floor(cells.fl[i] / higherFlux);
                int downcut = (int)Math.Floor(cell.Flux / higherFlux);

                if (downcut > 0)
                {
                    // 5. Apply erosion in-place
                    // JS: cells.h[i] -= Math.min(downcut, MAX_DOWNCUT);
                    int erosion = Math.Min(downcut, MapConstants.MAX_DOWNCUT);

                    // Re-assigning to the byte property ensures the next cell in the loop
                    // sees the updated height if it considers this cell a 'neighbor'.
                    cell.Height = (byte)Math.Max(0, cell.Height - erosion);
                }
            }
        }

        #endregion

        #region Resolve Depressions

        private static double[] AlterHeights(IMapGraph map)
        {
            return map.Cells.Select((c, i) =>
            {
                if (c.Height < MapConstants.LAND_THRESHOLD || c.Distance < 1) return (double)c.Height;
                double meanDist = c.NeighborCells.Average(n => (double)map.Cells[n].Distance);
                return c.Height + (c.Distance / 100.0) + (meanDist / 10000.0);
            }).ToArray();
        }

        public static void ResolveDepressions(IMapGraph map, double[] h)
        {
            var cells = map.Cells;
            var features = map.Features;

            int maxIterations = 250;
            int checkLakeMaxIteration = (int)(maxIterations * 0.85);
            int elevateLakeMaxIteration = (int)(maxIterations * 0.75);

            double GetHeight(int i)
            {
                var f = map.GetFeature(map.Cells[i].FeatureId);
                return (f != null && f.Type == FeatureType.Lake) ? f.Height : h[i];
            }

            var lakes = features.Where(f => f.Type == FeatureType.Lake).ToList();

            // JS sorts once at the beginning
            var land = Enumerable.Range(0, cells.Length)
                .Where(i => h[i] >= MapConstants.LAND_THRESHOLD && cells[i].Border == 0)
                .OrderBy(i => h[i])
                .ToList();

            List<int> progress = new List<int>();
            int depressions = int.MaxValue;
            int? prevDepressions = null;

            for (int iteration = 0; depressions > 0 && iteration < maxIterations; iteration++)
            {
                if (progress.Count > 5 && progress.Sum() > 0)
                {
                    double[] original = RiverModule.AlterHeights(map);
                    Array.Copy(original, h, h.Length);
                    break;
                }

                depressions = 0;

                if (iteration < checkLakeMaxIteration)
                {
                    foreach (var l in lakes)
                    {
                        if (l.IsClosed) continue;
                        double minShoreHeight = l.ShorelineCells.Min(s => h[s]);
                        if (minShoreHeight >= 100 || l.Height > minShoreHeight) continue;

                        if (iteration > elevateLakeMaxIteration)
                        {
                            foreach (int i in l.ShorelineCells) h[i] = cells[i].Height;
                            l.Height = l.ShorelineCells.Min(s => h[s]) - 1;
                            l.IsClosed = true;
                            continue;
                        }

                        depressions++;
                        l.Height = minShoreHeight + 0.2;
                    }
                }

                foreach (int i in land)
                {
                    // FIX 3: Neighbors in JS are cells.c[i]
                    double minNeighborHeight = cells[i].NeighborCells.Min(neighborIndex => GetHeight(neighborIndex));

                    if (minNeighborHeight >= 100 || h[i] > minNeighborHeight) continue;

                    depressions++;
                    h[i] = minNeighborHeight + 0.1;
                }

                // JS: prevDepressions !== null && progress.push(depressions - prevDepressions);
                if (prevDepressions.HasValue)
                    progress.Add(depressions - prevDepressions.Value);

                prevDepressions = depressions;
            }
        }

        #endregion

        #region Meandering

        public static List<PointFlux> AddMeandering(IMapGraph map, List<int> riverCells, double meandering = 0.5)
        {
            // FIX: Clean the input list immediately so all subsequent logic (and indices) are safe
            riverCells = riverCells.Where(idx => idx >= 0 && idx < map.Cells.Length).ToList();

            if (riverCells.Count < 2) return new List<PointFlux>();

            var cells = map.Cells;
            var meandered = new List<PointFlux>();
            int lastStep = riverCells.Count - 1;

            // Convert cell IDs to Coordinates (MapPoint list)
            var points = GetRiverPoints(map, riverCells);

            // Initial step logic based on height (Lowland vs Highland)
            int step = map.Cells[riverCells[0]].Height < 20 ? 1 : 10;

            for (int i = 0; i <= lastStep; i++, step++)
            {
                int cellIdx = riverCells[i];
                var p1 = points[i];
                double currentFlux = cells[cellIdx].Flux;

                // 1. Add the actual cell center
                meandered.Add(new PointFlux(p1.X, p1.Y, currentFlux));

                if (i == lastStep) break;

                var p2 = points[i + 1];
                double dist2 = Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2);

                // Skip jitter if points are too close and river is long
                if (dist2 <= 25 && riverCells.Count >= 6) continue;

                // 2. Calculate Meander offset (Perpendicular jitter)
                double meanderAmount = meandering + 1.0 / step + Math.Max(meandering - step / 100.0, 0);
                double angle = Math.Atan2(p2.Y - p1.Y, p2.X - p1.X);

                // Perpendicular vectors
                double sinMeander = Math.Sin(angle) * meanderAmount;
                double cosMeander = Math.Cos(angle) * meanderAmount;

                // 3. Add Intermediate Jitter Points
                // We pass 'currentFlux' to these points so the river width is maintained
                if (step < 20 && (dist2 > 64 || (dist2 > 36 && riverCells.Count < 5)))
                {
                    // Two intermediate points at 1/3 and 2/3
                    meandered.Add(new PointFlux((p1.X * 2 + p2.X) / 3.0 - sinMeander, (p1.Y * 2 + p2.Y) / 3.0 + cosMeander, currentFlux));
                    meandered.Add(new PointFlux((p1.X + p2.X * 2) / 3.0 + sinMeander / 2.0, (p1.Y + p2.Y * 2) / 3.0 - cosMeander / 2.0, currentFlux));
                }
                else if (dist2 > 25 || riverCells.Count < 6)
                {
                    // One midpoint
                    meandered.Add(new PointFlux((p1.X + p2.X) / 2.0 - sinMeander, (p1.Y + p2.Y) / 2.0 + cosMeander, currentFlux));
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

        private static List<MapPoint> GetRiverPoints(IMapGraph map, List<int> riverCells)
        {
            return riverCells.Select((cell, i) =>
            {
                if (cell == -1) return GetBorderPoint(map, riverCells[i - 1]);

                // Use the pack-level coordinate array
                return map.Points[cell];
            }).ToList();
        }

        private static MapPoint GetBorderPoint(IMapGraph map, int cellIndex)
        {
            var p = map.Points[cellIndex];
            double[] dists = { p.Y, map.Height - p.Y, p.X, map.Width - p.X };
            double min = dists.Min();

            if (min == p.Y) return new MapPoint(p.X, 0);
            if (min == map.Height - p.Y) return new MapPoint(p.X, map.Height);
            if (min == p.X) return new MapPoint(0, p.Y);
            return new MapPoint(map.Width, p.Y);
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

        #region River Rendering

        public static List<MapPoint> GetRiverPolygon(List<PointFlux> meanderedPoints, double widthFactor, double startingWidth)
        {
            var riverPointsLeft = new List<MapPoint>();
            var riverPointsRight = new List<MapPoint>();
            double maxFlux = 0;

            for (int i = 0; i < meanderedPoints.Count; i++)
            {
                var p1 = meanderedPoints[i];
                var p0 = i == 0 ? p1 : meanderedPoints[i - 1];
                var p2 = i == meanderedPoints.Count - 1 ? p1 : meanderedPoints[i + 1];

                // Track max flux encountered so far (rivers only get wider)
                if (p1.Flux > maxFlux) maxFlux = p1.Flux;

                // FGM Width Logic
                double offset = (Math.Sqrt(maxFlux) * widthFactor * 0.1) + startingWidth + (i * 0.1);

                // Perpendicular angle
                double angle = Math.Atan2(p0.Y - p2.Y, p0.X - p2.X);
                double sinOffset = Math.Sin(angle) * offset;
                double cosOffset = Math.Cos(angle) * offset;

                riverPointsLeft.Add(new MapPoint(p1.X - sinOffset, p1.Y + cosOffset));
                riverPointsRight.Add(new MapPoint(p1.X + sinOffset, p1.Y - cosOffset));
            }

            var polygon = new List<MapPoint>();
            polygon.AddRange(riverPointsRight); // Source to Mouth
            for (int i = riverPointsLeft.Count - 1; i >= 0; i--) // Mouth back to Source
            {
                polygon.Add(riverPointsLeft[i]);
            }

            return polygon;
        }

        #endregion
    }
}
