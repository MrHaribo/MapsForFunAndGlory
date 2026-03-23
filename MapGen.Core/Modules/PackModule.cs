using MapGen.Core.Helpers;
using System;
using System.Collections.Generic;
using static MapGen.Core.Helpers.NumberUtils;

namespace MapGen.Core.Modules
{
    public static class PackModule
    {
        public static MapPack ReGraph(MapData data)
        {
            var newP = new List<MapPoint>();
            var newG = new List<int>();
            var newH = new List<byte>();

            double spacing2 = Math.Pow(data.Spacing, 2);

            // 1. Point Filtering & Coastal Densification
            for (int i = 0; i < data.Cells.Length; i++)
            {
                var cell = data.Cells[i];

                // H < 20 check (Deep Ocean)
                // Distance (t) -1/-2 are water-adjacent/coastal
                if (cell.Height < MapConstants.LAND_THRESHOLD && cell.Distance != -1 && cell.Distance != -2) continue;

                // Lake point subsampling
                if (cell.Distance == -2)
                {
                    var feature = data.Features[cell.FeatureId];
                    if (i % 4 == 0 || feature.Type == FeatureType.Lake) continue;
                }

                var p = data.Points[i];
                AddPoint(newP, newG, newH, p.X, p.Y, i, cell.Height);

                // Add midpoints along coast (Distance 1 or -1)
                if (cell.Distance == 1 || cell.Distance == -1)
                {
                    if (cell.Border == 1) continue;

                    foreach (int e in cell.NeighborCells)
                    {
                        if (i > e) continue;
                        if (data.Cells[e].Distance == cell.Distance)
                        {
                            var ep = data.Points[e];
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

            // 2. Voronoi Generation for the filtered Pack points
            var (packCells, packVertices) = VoronoiGenerator.CalculateVoronoi(newP.ToArray(), data.BoundaryPoints);

            // 3. Setup Spatial Lookup
            var lookups = QuadtreeHelper.CreateLookupDelegates(newP);

            // 4. MapPack Assembly
            var pack = new MapPack
            {
                Cells = packCells,
                Vertices = packVertices,
                Points = newP.ToArray(),
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

                double rawArea = PathUtils.CalculatePolygonArea(cell, pack.Vertices);
                // Area is clamped to ushort.MaxValue (65535)
                cell.Area = (ushort)MinMax(Math.Abs(rawArea), 0, ushort.MaxValue);
            }

            return pack;
        }

        public static MapPack RefineRivers(MapPack pack, MapData originalData)
        {
            var newP = new List<MapPoint>();
            var newG = new List<int>(); // Grid indices
            var newH = new List<byte>(); // Heights

            // 1. Point Collection & River Densification
            for (int i = 0; i < pack.Cells.Length; i++)
            {
                var cell = pack.Cells[i];
                var p = pack.Points[i];

                // Always add the base cell point
                AddPoint(newP, newG, newH, p.X, p.Y, cell.GridId, cell.Height);

                // If this is a river cell, densify its boundaries with land neighbors
                if (cell.RiverId != 0)
                {
                    foreach (int nIdx in cell.NeighborCells)
                    {
                        var neighbor = pack.Cells[nIdx];

                        // We only add extra points between River -> Land 
                        // (Adding between River -> River is usually unnecessary and adds too many points)
                        if (neighbor.RiverId == 0)
                        {
                            var np = pack.Points[nIdx];

                            // Calculate two points along the line between cell centers
                            // This "constricts" the Voronoi cell shape to follow the river path
                            double x1 = Round(p.X + (np.X - p.X) * 0.33, 1);
                            double y1 = Round(p.Y + (np.Y - p.Y) * 0.33, 1);

                            double x2 = Round(p.X + (np.X - p.X) * 0.66, 1);
                            double y2 = Round(p.Y + (np.Y - p.Y) * 0.66, 1);

                            AddPoint(newP, newG, newH, x1, y1, cell.GridId, cell.Height);
                            AddPoint(newP, newG, newH, x2, y2, neighbor.GridId, neighbor.Height);
                        }
                    }
                }
            }

            // 2. Voronoi Generation (Standard logic)
            var (packCells, packVertices) = VoronoiGenerator.CalculateVoronoi(newP.ToArray(), originalData.BoundaryPoints);

            // 3. Setup Spatial Lookup
            var lookups = QuadtreeHelper.CreateLookupDelegates(newP);

            // 4. MapPack Assembly
            var newPack = new MapPack
            {
                Cells = packCells,
                Vertices = packVertices,
                Points = newP.ToArray(),
                Options = originalData.Options,
                FindCell = lookups.Find,
                FindCellInRange = lookups.FindInRange
            };

            // 5. Transfer Data & Calculate Areas
            for (int i = 0; i < newPack.Cells.Length; i++)
            {
                var cell = newPack.Cells[i];
                cell.Height = newH[i];
                cell.GridId = newG[i];
                cell.Index = i;

                double rawArea = PathUtils.CalculatePolygonArea(cell, newPack.Vertices);
                cell.Area = (ushort)Math.Clamp(Math.Abs(rawArea), 0, ushort.MaxValue);
            }

            return newPack;
        }

        private static void AddPoint(List<MapPoint> p, List<int> g, List<byte> h, double x, double y, int gridIdx, byte height)
        {
            p.Add(new MapPoint(x, y));
            g.Add(gridIdx);
            h.Add(height);
        }
    }
}