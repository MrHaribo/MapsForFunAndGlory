using D3Sharp.QuadTree;
using MapGen.Core.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MapGen.Core.Modules
{
    public static class BurgModule
    {
        #region Generate Burgs

        public static void Generate(MapPack pack)
        {
            var cells = pack.Cells;
            var rng = pack.Rng;

            List<MapBurg> burgs = new List<MapBurg> { };
            foreach (var cell in cells) cell.BurgId = 0;

            var populatedIndices = new List<int>();
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i].Suitability > 0 && cells[i].CultureId > 0)
                    populatedIndices.Add(i);
            }

            if (populatedIndices.Count == 0) return;

            populatedIndices.Sort();

            var quadtree = new QuadTree<QuadPoint, QuadPointNode>(new List<QuadPoint>());

            GenerateCapitals();
            GenerateTowns();

            pack.Burgs = burgs;
            Shift(pack);

            // --- Local Functions ---

            void GenerateCapitals()
            {
                short[] scores = new short[cells.Length];
                for (int i = 0; i < cells.Length; i++)
                {
                    scores[i] = (short)Math.Floor(cells[i].Suitability * (0.5 + rng.Next() * 0.5));
                }

                var sorted = populatedIndices
                    .OrderByDescending(i => scores[i])
                    .ToList();
                int capitalsNumber = GetCapitalsNumber();
                double spacing = (pack.Width + pack.Height) / 2.0 / capitalsNumber;

                // JS Alignment: check against length which includes the padding
                for (int i = 0; burgs.Count < capitalsNumber; i++)
                {
                    int cellIdx = sorted[i];
                    var p = cells[cellIdx].Point;

                    if (quadtree.Find(p.X, p.Y, spacing) == null)
                    {
                        burgs.Add(new MapBurg { CellId = cellIdx, Position = new MapPoint(p.X, p.Y) });
                        quadtree.Add(new QuadPoint { X = p.X, Y = p.Y, DataIndex = burgs.Count - 1 });
                    }

                    if (i == sorted.Count - 1)
                    {
                        quadtree = new QuadTree<QuadPoint, QuadPointNode>(new List<QuadPoint>());
                        burgs.Clear();
                        spacing /= 1.2;
                        i = -1;
                    }
                }

                // Skip index 0 (padding)
                for (int i = 0; i < burgs.Count; i++)
                {
                    var b = burgs[i];
                    var cell = cells[b.CellId];

                    int bId = i + 1; // The index IS the ID now because of padding
                    var culture = pack.Cultures[cell.CultureId];

                    b.Id = bId;
                    b.StateId = bId;
                    b.CultureId = cell.CultureId;
                    b.Name = NameModule.GetCultureShort(rng, culture.BaseNameId);
                    b.FeatureId = cell.FeatureId;
                    b.IsCapital = true;

                    cell.BurgId = (ushort)bId;
                }
            }

            void GenerateTowns()
            {
                short[] scores = new short[cells.Length];
                for (int i = 0; i < cells.Length; i++)
                {
                    scores[i] = (short)Math.Floor((cells[i].Suitability * rng.Gauss(1, 3, 0, 20, 3)));
                }

                var sorted = populatedIndices
                    .OrderByDescending(i => scores[i])
                    .ToList();
                int townsNumber = GetTownsNumber(pack.Options);
                double spacing = (pack.Width + pack.Height) / 150.0 / (Math.Pow(townsNumber, 0.7) / 66.0);

                int added = 0;
                while (added < townsNumber && spacing > 1)
                {
                    for (int i = 0; i < sorted.Count && added < townsNumber; i++)
                    {
                        int cellIdx = sorted[i];
                        if (cells[cellIdx].BurgId > 0) continue;

                        var p = cells[cellIdx].Point;

                        double minSpacing = spacing * rng.Gauss(1, 0.3, 0.2, 2, 2);
                        var found = quadtree.Find(p.X, p.Y, minSpacing);
                        if (found != null)
                            continue;

                        int bId = burgs.Count + 1;

                        var culture = pack.Cultures[cells[cellIdx].CultureId];

                        burgs.Add(new MapBurg
                        {
                            Id = bId,
                            CellId = cellIdx,
                            Position = new MapPoint(p.X, p.Y),
                            StateId = 0,
                            IsCapital = false,
                            CultureId = cells[cellIdx].CultureId,
                            Name = NameModule.GetCulture(rng, culture.BaseNameId),
                            //Name = "town",
                            FeatureId = cells[cellIdx].FeatureId
                        });

                        // Nasty 6h to fix bug by Gemeni
                        //quadtree.Add(new QuadPoint { X = p.X, Y = p.Y, DataIndex = bId });
                        cells[cellIdx].BurgId = (ushort)bId;
                        added++;
                    }
                    spacing *= 0.5;
                }
            }

            int GetCapitalsNumber()
            {
                int number = pack.Options.StatesCount;
                if (populatedIndices.Count < number * 10)
                    number = populatedIndices.Count / 10;
                return number;
            }

            int GetTownsNumber(MapOptions options)
            {
                if (options.BurgCount == MapConstants.BURG_MAX_COUNT)
                {
                    double density = Math.Pow(pack.GridPointsCount / 10000.0, 0.8);
                    return (int)Math.Round(populatedIndices.Count / 5.0 / density);
                }
                return Math.Min(options.BurgCount, populatedIndices.Count);
            }
        }

        #endregion

        #region Shift Burgs

        public static void Shift(MapPack pack)
        {
            var cells = pack.Cells;
            var burgs = pack.Burgs;

            var featurePortCandidates = new Dictionary<int, List<MapBurg>>();

            for (int i = 0; i < burgs.Count; i++)
            {
                var burg = burgs[i];
                burg.PortId = 0;

                int cellId = burg.CellId;
                int havenIdx = cells[cellId].Haven;
                int featureId = cells[havenIdx].FeatureId;

                if (featureId == 0) continue;

                var feature = pack.GetFeature(featureId);
                bool isHarbor = (cells[cellId].Harbor > 0 && burg.IsCapital) || cells[cellId].Harbor == 1;
                bool isFrozen = cells[cellId].Temp <= 0;

                if (feature.CellsCount > 1 && isHarbor && !isFrozen)
                {
                    if (!featurePortCandidates.ContainsKey(featureId))
                        featurePortCandidates[featureId] = new List<MapBurg>();

                    featurePortCandidates[featureId].Add(burg);
                }
            }

            foreach (var kvp in featurePortCandidates)
            {
                if (kvp.Value.Count < 2) continue;

                foreach (var burg in kvp.Value)
                {
                    burg.PortId = kvp.Key;
                    int havenIdx = cells[burg.CellId].Haven;
                    var (x, y) = GetCloseToEdgePoint(cells, pack, burg.CellId, havenIdx);
                    burg.Position = new MapPoint(x, y);
                }
            }

            for (int i = 0; i < burgs.Count; i++)
            {
                var burg = burgs[i];
                if (burg.PortId > 0 || cells[burg.CellId].RiverId == 0) continue;

                int cellId = burg.CellId;
                double fluxShift = Math.Min(cells[cellId].Flux / 150.0, 1.0);

                double newX = burg.Position.X;
                double newY = burg.Position.Y;

                newX = (cellId % 2 != 0) ? newX + fluxShift : newX - fluxShift;
                newY = (cells[cellId].RiverId % 2 != 0) ? newY + fluxShift : newY - fluxShift;

                burg.Position = new MapPoint(NumberUtils.Round(newX, 2), NumberUtils.Round(newY, 2));
            }
        }

        private static (double X, double Y) GetCloseToEdgePoint(MapCell[] cells, MapPack pack, int cell1Idx, int cell2Idx)
        {
            var c1 = cells[cell1Idx];
            var commonVertices = c1.Verticies
                .Where(vIdx => pack.Vertices[vIdx].AdjacentCells.Contains(cell2Idx))
                .ToList();

            if (commonVertices.Count < 2) return (c1.Point.X, c1.Point.Y);

            var v1 = pack.Vertices[commonVertices[0]].Point;
            var v2 = pack.Vertices[commonVertices[1]].Point;

            double xEdge = (v1.X + v2.X) / 2.0;
            double yEdge = (v1.Y + v2.Y) / 2.0;

            double x = NumberUtils.Round(c1.Point.X + 0.95 * (xEdge - c1.Point.X), 2);
            double y = NumberUtils.Round(c1.Point.Y + 0.95 * (yEdge - c1.Point.Y), 2);

            return (x, y);
        }

        #endregion
    }
}
