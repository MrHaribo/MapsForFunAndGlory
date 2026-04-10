using MapGen.Core.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MapGen.Core.Modules
{
    using D3Sharp.QuadTree;
    using MapGen.Core.Helpers;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public static class BurgModule
    {
        #region Generate Burgs

        public static void Generate(MapPack pack, MapData mapData)
        {
            var cells = pack.Cells;
            var rng = mapData.Rng;

            List<MapBurg> burgs = new List<MapBurg>();
            foreach (var cell in cells) cell.BurgId = 0;

            var populatedIndices = new List<int>();
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i].Suitability > 0 && cells[i].CultureId > 0)
                    populatedIndices.Add(i);
            }

            if (populatedIndices.Count == 0) return;

            // Initialize QuadTree directly (using the types from your QuadtreeHelper)
            // We start with an empty list and add burgs as we go.
            var quadtree = new QuadTree<QuadPoint, QuadPointNode>(new List<QuadPoint>());

            GenerateCapitals();
            GenerateTowns();

            pack.Burgs = burgs;

            // --- Local Functions ---

            void GenerateCapitals()
            {
                short[] scores = new short[cells.Length];
                foreach (int i in populatedIndices)
                    scores[i] = (short)(cells[i].Suitability * (0.5 + rng.Next() * 0.5));

                var sorted = populatedIndices.OrderByDescending(i => scores[i]).ToList();
                int capitalsNumber = GetCapitalsNumber();
                double spacing = (pack.Width + pack.Height) / 2.0 / capitalsNumber;

                // JS: for (let i = 0; burgs.length <= capitalsNumber; i++)
                // Since burgs is 0-indexed now, we check count < capitalsNumber
                for (int i = 0; burgs.Count < capitalsNumber; i++)
                {
                    int cellIdx = sorted[i];
                    var p = cells[cellIdx].Point;

                    if (quadtree.Find(p.X, p.Y, spacing) == null)
                    {
                        // The Burg is added at Index (burgs.Count)
                        burgs.Add(new MapBurg { Cell = cellIdx, Position = new MapPoint(p.X, p.Y) });

                        // We store the current index (0, 1, 2...) in the QuadTree
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

                for (int i = 0; i < burgs.Count; i++)
                {
                    var b = burgs[i];
                    var cell = cells[b.Cell];

                    // bId is the 1-based ID used by the cells and the "GetBurg" helper
                    int bId = i + 1;

                    b.Id = bId;
                    b.StateId = bId;
                    b.CultureId = cell.CultureId;
                    b.Name = NameModule.GetCultureShort(rng, pack.Cultures[b.CultureId].BaseNameId);
                    b.FeatureId = cell.FeatureId;
                    b.IsCapital = true;

                    cell.BurgId = bId; // Cell gets 1, 2, 3...
                }
            }

            void GenerateTowns()
            {
                short[] scores = new short[cells.Length];
                foreach (int i in populatedIndices)
                    scores[i] = (short)(cells[i].Suitability * rng.Gauss(1, 3, 0, 20, 3));

                var sorted = populatedIndices.OrderByDescending(i => scores[i]).ToList();
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

                        if (quadtree.Find(p.X, p.Y, minSpacing) != null) continue;

                        // New 1-based ID for the cell
                        int bId = burgs.Count + 1;
                        var cultureId = cells[cellIdx].CultureId;

                        burgs.Add(new MapBurg
                        {
                            Id = bId,
                            Cell = cellIdx,
                            Position = new MapPoint(p.X, p.Y),
                            StateId = 0,
                            IsCapital = false,
                            CultureId = cultureId,
                            Name = NameModule.GetCulture(rng, pack.Cultures[cultureId].BaseNameId),
                            FeatureId = cells[cellIdx].FeatureId
                        });

                        // Store the 0-based list index in the quadtree
                        quadtree.Add(new QuadPoint { X = p.X, Y = p.Y, DataIndex = burgs.Count - 1 });
                        cells[cellIdx].BurgId = (ushort)bId;
                        added++;
                    }
                    spacing *= 0.5;
                }
            }

            int GetCapitalsNumber()
            {
                int number = mapData.Options.StatesCount;
                if (populatedIndices.Count < number * 10)
                    number = populatedIndices.Count / 10;
                return number;
            }

            int GetTownsNumber(MapOptions options)
            {
                if (options.BurgCount == MapConstants.BURG_MAX_COUNT)
                {
                    double density = Math.Pow(pack.Points.Length / 10000.0, 0.8);
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
            var features = pack.Features;
            var burgs = pack.Burgs;

            // Port candidates grouped by the water body feature they sit on
            var featurePortCandidates = new Dictionary<int, List<MapBurg>>();

            foreach (var burg in burgs)
            {
                // burg.lock is a JS state for manually placed burgs; 
                // if you don't have it in your model yet, you can omit that check.
                burg.PortId = 0; // Reset port status

                int cellId = burg.Cell;
                int havenIdx = cells[cellId].Haven;
                int harbor = cells[cellId].Harbor;

                // Get the water feature the haven cell belongs to
                var havenCell = cells[havenIdx];
                int featureId = havenCell.FeatureId;

                if (featureId == 0) continue; // No adjacent water

                var feature = pack.GetFeature(featureId);
                bool isMulticell = feature.CellsCount > 1;

                // Port logic: Capitals with any harbor OR any burg with a "safe harbor" (harbor == 1)
                bool isHarbor = (harbor > 0 && burg.IsCapital) || harbor == 1;

                // Temperature check (JS uses grid temperature)
                bool isFrozen = cells[cellId].Temp <= 0;

                if (isMulticell && isHarbor && !isFrozen)
                {
                    if (!featurePortCandidates.ContainsKey(featureId))
                        featurePortCandidates[featureId] = new List<MapBurg>();

                    featurePortCandidates[featureId].Add(burg);
                }
            }

            // Shift ports to the edge of the water body
            foreach (var kvp in featurePortCandidates)
            {
                int featureId = kvp.Key;
                var candidates = kvp.Value;

                if (candidates.Count < 2) continue; // Only one port on water body - skip per Azgaar logic

                foreach (var burg in candidates)
                {
                    burg.PortId = featureId;
                    int havenIdx = cells[burg.Cell].Haven;

                    var (x, y) = GetCloseToEdgePoint(burg.Cell, havenIdx);
                    burg.Position = new MapPoint(x, y);
                }
            }

            // Shift non-port river burgs a bit based on flux
            foreach (var burg in burgs)
            {
                // Skip if it's already a port or not on a river (RiverId == 0)
                if (burg.PortId > 0 || cells[burg.Cell].RiverId == 0) continue;

                int cellId = burg.Cell;
                double fluxShift = Math.Min(cells[cellId].Flux / 150.0, 1.0);

                double newX = burg.Position.X;
                double newY = burg.Position.Y;

                // Deterministic nudge based on cell/river IDs
                newX = (cellId % 2 != 0) ? newX + fluxShift : newX - fluxShift;
                newY = (cells[cellId].RiverId % 2 != 0) ? newY + fluxShift : newY - fluxShift;

                burg.Position = new MapPoint(Math.Round(newX, 2), Math.Round(newY, 2));
            }

            // --- Local Helper Function ---

            (double X, double Y) GetCloseToEdgePoint(int cell1Idx, int cell2Idx)
            {
                var c1 = cells[cell1Idx];
                var c2 = cells[cell2Idx];

                // Find vertices shared between the burg cell and its water "haven" cell
                var commonVertices = c1.Verticies
                    .Where(vIdx => pack.Vertices[vIdx].AdjacentCells.Contains(cell2Idx))
                    .ToList();

                if (commonVertices.Count < 2) return (c1.Point.X, c1.Point.Y);

                var v1 = pack.Vertices[commonVertices[0]].Point;
                var v2 = pack.Vertices[commonVertices[1]].Point;

                // Calculate mid-point of the edge
                double xEdge = (v1.X + v2.X) / 2.0;
                double yEdge = (v1.Y + v2.Y) / 2.0;

                // Move the burg 95% of the way toward the edge from the cell center
                double x = Math.Round(c1.Point.X + 0.95 * (xEdge - c1.Point.X), 2);
                double y = Math.Round(c1.Point.Y + 0.95 * (yEdge - c1.Point.Y), 2);

                return (x, y);
            }
        }

        #endregion
    }
}
