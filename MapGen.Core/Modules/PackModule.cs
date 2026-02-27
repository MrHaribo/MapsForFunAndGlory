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
                if (cell.H < MapConstants.LAND_THRESHOLD && cell.Distance != -1 && cell.Distance != -2) continue;

                // Lake point subsampling
                if (cell.Distance == -2)
                {
                    var feature = data.Features[cell.FeatureId];
                    if (i % 4 == 0 || feature.Type == FeatureType.Lake) continue;
                }

                var p = data.Points[i];
                AddPoint(newP, newG, newH, p.X, p.Y, i, cell.H);

                // Add midpoints along coast (Distance 1 or -1)
                if (cell.Distance == 1 || cell.Distance == -1)
                {
                    if (cell.B == 1) continue;

                    foreach (int e in cell.C)
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
                            AddPoint(newP, newG, newH, x1, y1, i, cell.H);
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
                cell.H = newH[i];
                cell.GridId = newG[i];
                cell.Index = i; // Ensure internal index is set

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