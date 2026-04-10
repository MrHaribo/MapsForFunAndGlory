using MapGen.Core.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using static MapGen.Core.Helpers.NumberUtils;

namespace MapGen.Core.Modules
{
    public static class PackModule
    {
        public static MapPack ReGraph(MapData mapData)
        {
            var newP = new List<MapPoint>();
            var newG = new List<int>();
            var newH = new List<byte>();

            double spacing2 = Math.Pow(mapData.Spacing, 2);

            // 1. Point Filtering & Coastal Densification
            for (int i = 0; i < mapData.Cells.Length; i++)
            {
                var cell = mapData.Cells[i];

                // H < 20 check (Deep Ocean)
                // Distance (t) -1/-2 are water-adjacent/coastal
                if (cell.Height < MapConstants.LAND_THRESHOLD && cell.Distance != -1 && cell.Distance != -2) continue;

                // Lake point subsampling
                if (cell.Distance == -2)
                {
                    var feature = mapData.Features[cell.FeatureId];
                    if (i % 4 == 0 || feature.Type == FeatureType.Lake) continue;
                }

                var p = mapData.Points[i];
                AddPoint(newP, newG, newH, p.X, p.Y, i, cell.Height);

                // Add midpoints along coast (Distance 1 or -1)
                if (cell.Distance == 1 || cell.Distance == -1)
                {
                    if (cell.Border == 1) continue;

                    foreach (int e in cell.NeighborCells)
                    {
                        if (i > e) continue;
                        if (mapData.Cells[e].Distance == cell.Distance)
                        {
                            var ep = mapData.Points[e];
                            double dist2 = Math.Pow(p.Y - ep.Y, 2) + Math.Pow(p.X - ep.X, 2);
                            if (dist2 < spacing2) continue;

                            // Use NumberUtils.Round to match JS: rn((x+x)/2, 1)
                            double x1 = Round((p.X + ep.X) / 2.0, 1);
                            double y1 = Round((p.Y + ep.Y) / 2.0, 1);
                            AddPoint(newP, newG, newH, x1, y1, i, cell.Height);
                        }
                    }
                }
            }

            return CreatePack(mapData, newP, newG, newH);
        }

        public static MapPack RefineRivers(MapPack pack, MapData mapData)
        {
            var newP = new List<MapPoint>();
            var newG = new List<int>();
            var newH = new List<byte>();

            // 1. Pre-calculate paths once (O(Rivers))
            var riverPaths = new Dictionary<int, List<MapPoint>>();
            foreach (var river in pack.Rivers)
            {
                riverPaths[river.Id] = RiverModule.AddMeandering(pack, river.Cells, 1.5).Select(p => new MapPoint(p.X, p.Y)).ToList();
            }

            // 2. Collection Loop
            for (int i = 0; i < pack.Cells.Length; i++)
            {
                var cell = pack.Cells[i];
                var p = pack.Points[i];

                // Base Point: Link to current index 'i'
                AddPoint(newP, newG, newH, p.X, p.Y, i, cell.Height);

                if (cell.RiverId != 0)
                {
                    foreach (int nIdx in cell.NeighborCells)
                    {
                        var neighbor = pack.Cells[nIdx];
                        if (neighbor.RiverId == 0)
                        {
                            var np = pack.Points[nIdx];
                            // Midpoints: Link to 'i' if it's the river side, 
                            // or 'nIdx' if it's the land side.
                            AddPoint(newP, newG, newH, p.X + (np.X - p.X) * 0.33, p.Y + (np.Y - p.Y) * 0.33, i, cell.Height);
                            AddPoint(newP, newG, newH, p.X + (np.X - p.X) * 0.66, p.Y + (np.Y - p.Y) * 0.66, nIdx, neighbor.Height);
                        }
                    }
                }
            }

            // 3. Execute Warp (Now much faster)
            double warpRadius = mapData.Spacing * 1.5;
            WarpPointsToRivers(newP, newG, pack, riverPaths, warpRadius);

            // 4. Finalize
            var newPack = CreatePack(mapData, newP, newG, newH);
            SelectiveRiverSmooth(newPack, mapData);

            return newPack;
        }

        public static void SelectiveRiverSmooth(MapPack newPack, MapData originalData)
        {
            foreach (var cell in newPack.Cells)
            {
                int centerIdx = originalData.FindGridCell(cell.Point.X, cell.Point.Y);
                var sourceCell = originalData.Cells[centerIdx];

                // Only smooth if this cell (or its source) is a river
                // You can also check sourceCell.NeighborCells to see if a river is nearby
                bool isRiverArea = sourceCell.RiverId != 0 ||
                                   sourceCell.NeighborCells.Any(n => originalData.Cells[n].RiverId != 0);

                if (!isRiverArea)
                {
                    // Keep original sharp detail
                    cell.Height = sourceCell.Height;
                    continue;
                }

                // Apply IDW Smoothing only to the river corridor
                double totalWeight = 0;
                double weightedHeight = 0;
                var sampleIndices = new List<int>(sourceCell.NeighborCells) { centerIdx };

                foreach (int idx in sampleIndices)
                {
                    var sampleCell = originalData.Cells[idx];
                    double dist = Math.Sqrt(Math.Pow(cell.Point.X - originalData.Points[idx].X, 2) +
                                            Math.Pow(cell.Point.Y - originalData.Points[idx].Y, 2));
                    double weight = 1.0 / (dist + 0.001);
                    weightedHeight += sampleCell.Height * weight;
                    totalWeight += weight;
                }
                cell.Height = (byte)(weightedHeight / totalWeight);
            }
        }

        public static void WarpPointsToRivers(
            List<MapPoint> points,
            List<int> originCellIndices,
            MapPack originalPack,
            Dictionary<int, List<MapPoint>> riverPaths,
            double radius)
        {
            double radiusSq = radius * radius;

            for (int i = 0; i < points.Count; i++)
            {
                // Safety: Ensure the index exists in the origin list and the original pack
                int cellIdx = originCellIndices[i];
                if (cellIdx < 0 || cellIdx >= originalPack.Cells.Length) continue;

                var cell = originalPack.Cells[cellIdx];

                // Only warp points that belong to a river or are immediately adjacent
                if (cell.RiverId != 0 && riverPaths.ContainsKey(cell.RiverId))
                {
                    var p = points[i];
                    var spine = riverPaths[cell.RiverId];

                    // Find closest point on this specific river's spine
                    MapPoint closest = spine[0];
                    double minD2 = double.MaxValue;
                    for (int j = 0; j < spine.Count; j++)
                    {
                        double d2 = DistSq(p.X, p.Y, spine[j].X, spine[j].Y);
                        if (d2 < minD2) { minD2 = d2; closest = spine[j]; }
                    }

                    if (minD2 < radiusSq)
                    {
                        double dist = Math.Sqrt(minD2);
                        // Quadratic falloff: stronger pull closer to the center
                        double force = Math.Pow(1.0 - (dist / radius), 2);
                        double strength = 0.45 * force;

                        points[i] = new MapPoint(
                            p.X + (closest.X - p.X) * strength,
                            p.Y + (closest.Y - p.Y) * strength
                        );
                    }
                }
            }
        }


        private static double DistSq(double x1, double y1, double x2, double y2)
        {
            return (x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2);
        }

        private static MapPack CreatePack(MapData data, List<MapPoint> newP, List<int> newG, List<byte> newH)
        {
            // 2. Voronoi Generation for the filtered Pack points
            var (packCells, packVertices) = VoronoiGenerator.CalculateVoronoi(newP.ToArray(), data.BoundaryPoints);

            // 3. Setup Spatial Lookup
            var lookups = QuadtreeHelper.CreateLookupDelegates(newP);

            // 4. MapPack Assembly
            var pack = new MapPack
            {
                Rng = data.Rng,
                Cells = packCells,
                Vertices = packVertices,
                Points = newP.ToArray(),
                GridPointsCount = data.Points.Length,
                Options = data.Options,

                // JS: pack.cells.q.find(x, y)
                FindCell = lookups.Find,
                FindCellInRange = lookups.FindInRange
            };

            // 5. Populate Pack-specific Cell Data & Calculate Area
            for (int i = 0; i < pack.Cells.Length; i++)
            {
                var cell = pack.Cells[i];
                cell.Height = newH[i];
                cell.GridId = newG[i];
                cell.Index = i; // Ensure internal index is set
                cell.Temp = data.Cells[newG[i]].Temp;
                cell.Prec = data.Cells[newG[i]].Prec;

                double rawArea = PathUtils.CalculatePolygonArea(cell, pack.Vertices);
                // Area is clamped to ushort.MaxValue (65535)
                cell.Area = (ushort)MinMax(Math.Abs(rawArea), 0, ushort.MaxValue);
            }

            return pack;
        }

        private static void AddPoint(List<MapPoint> p, List<int> g, List<byte> h, double x, double y, int gridIdx, byte height)
        {
            p.Add(new MapPoint(x, y));
            g.Add(gridIdx);
            h.Add(height);
        }
    }
}