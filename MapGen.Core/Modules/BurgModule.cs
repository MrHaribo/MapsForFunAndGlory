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

        #region Specify Burgs


        public static void Specify(MapPack pack)
        {
            // First Pass: Define Populations, Emblems, and Features
            foreach (var burg in pack.Burgs)
            {
                if (burg.Id <= 0) continue;

                DefinePopulation(pack, burg);
                DefineEmblem(pack, burg);
                DefineFeatures(pack, burg);
            }

            var populations = pack.Burgs
                .Where(b => b.Id > 0)
                .Select(b => b.Population)
                .OrderBy(p => p) // ascending
                .ToList();

            var groups = GetDefaultGroups();

            // Second Pass: Assign Groups
            foreach (var burg in pack.Burgs)
            {
                if (burg.Id <= 0) continue;

                DefineGroup(pack, burg, populations, groups);
            }
        }

        private static void DefinePopulation(MapPack pack, MapBurg burg)
        {
            int cellId = burg.CellId;

            // JS: let population = pack.cells.s[cellId] / 5;
            double population = pack.Cells[cellId].Suitability / 5.0;

            if (burg.IsCapital) population *= 1.5;

            // Note: Assuming RouteModule handles this lookup. Replace with your actual implementation.
            double connectivityRate = RouteModule.GetConnectivityRate(pack, cellId);
            if (connectivityRate > 0) population *= connectivityRate;

            population *= pack.Rng.Gauss(1, 1, 0.25, 4, 5); // randomize
            population += ((burg.Id % 100) - (cellId % 100)) / 1000.0; // unround

            burg.Population = Math.Round(Math.Max(population, 0.01), 3);
        }

        private static void DefineEmblem(MapPack pack, MapBurg burg)
        {
            burg.Type = GetType(pack, burg.CellId, burg.IsPort); // burg.Type is CultureType

            var state = pack.States[burg.StateId]; // Direct 0-padded array lookup
            var stateCOA = state?.CoA;

            double kinship = 0.25;
            if (burg.IsCapital) kinship += 0.1;
            else if (burg.IsPort) kinship -= 0.1;

            if (state != null && burg.CultureId != state.CultureId) kinship -= 0.25;

            // NO MAGIC STRINGS: We pull the exact names from the BurgType and CultureType enums
            string type = (burg.IsCapital && pack.Rng.P(0.2))
                ? BurgType.Capital.ToString()
                : (burg.Type == CultureType.Generic ? BurgType.City.ToString() : burg.Type.ToString());

            // Placeholder for now. Eventually this will be: 
            // burg.CoA = CoaModule.Generate(pack.Rng, stateCOA, kinship, null, type);
            burg.CoA = new MapCoA { Type = CultureType.Undefined };
        }

        private static CultureType GetType(MapPack pack, int cellId, bool port)
        {
            var cells = pack.Cells;

            if (port) return CultureType.Naval;

            int haven = cells[cellId].Haven;
            if (haven > 0)
            {
                int featureId = cells[haven].FeatureId;
                if (featureId > 0)
                {
                    // Strict usage of your defined GetFeature method
                    var feature = pack.GetFeature(featureId);
                    if (feature != null && feature.Type == FeatureType.Lake) return CultureType.Lake;
                }
            }

            if (cells[cellId].Height > 60) return CultureType.Highland;

            if (cells[cellId].RiverId > 0 && cells[cellId].Flux >= 100) return CultureType.River;

            int biome = cells[cellId].BiomeId;
            double population = cells[cellId].Population; // Cell population

            if (cells[cellId].BurgId == 0 || population <= 5)
            {
                if (population < 5 && biome >= 1 && biome <= 4) return CultureType.Nomadic;
                if (biome > 4 && biome < 10) return CultureType.Hunting;
            }

            return CultureType.Generic;
        }

        private static void DefineFeatures(MapPack pack, MapBurg burg)
        {
            double pop = burg.Population;
            var rng = pack.Rng;

            BurgFeature features = BurgFeature.None;

            if (burg.IsCapital) features |= BurgFeature.Capital;
            if (burg.IsPort) features |= BurgFeature.Port;

            if (burg.IsCapital || (pop > 50 && rng.P(0.75)) || (pop > 15 && rng.P(0.5)) || rng.P(0.1))
                features |= BurgFeature.Citadel;

            // Note: Assuming RouteModule handles these lookups.
            if (RouteModule.IsCrossroad(pack, burg.CellId) || (RouteModule.HasRoad(pack, burg.CellId) && rng.P(0.7)) || pop > 20 || (pop > 10 && rng.P(0.8)))
                features |= BurgFeature.Plaza;

            if (burg.IsCapital || pop > 30 || (pop > 20 && rng.P(0.75)) || (pop > 10 && rng.P(0.5)) || rng.P(0.1))
                features |= BurgFeature.Walls;

            if (pop > 60 || (pop > 40 && rng.P(0.75)) || (pop > 20 && features.HasFlag(BurgFeature.Walls) && rng.P(0.4)))
                features |= BurgFeature.Shanty;

            int religionId = pack.Cells[burg.CellId].ReligionId;
            var state = pack.States[burg.StateId];
            bool theocracy = state != null && state.Form == StateForm.Theocracy;

            if ((religionId > 0 && theocracy && rng.P(0.5)) || pop > 50 || (pop > 35 && rng.P(0.75)) || (pop > 20 && rng.P(0.5)))
                features |= BurgFeature.Temple;

            burg.Features = features;
        }

        private static void DefineGroup(MapPack pack, MapBurg burg, List<double> populations, List<BurgGroup> groups)
        {
            var defaultGroup = groups.FirstOrDefault(g => g.IsDefault);
            if (defaultGroup == null) return;

            // 2. Assign default using Enum
            burg.Group = defaultGroup.Type;

            foreach (var group in groups)
            {
                if (!group.Active) continue;

                if (group.Min > 0 && burg.Population < group.Min) continue;
                if (group.Max > 0 && burg.Population > group.Max) continue;

                // Fast bitwise flag matching
                if ((burg.Features & group.RequiredFeatures) != group.RequiredFeatures) continue;
                if ((burg.Features & group.ForbiddenFeatures) != 0) continue;

                if (group.Biomes != null && group.Biomes.Count > 0)
                {
                    if (!group.Biomes.Contains(pack.Cells[burg.CellId].BiomeId)) continue;
                }

                if (group.Percentile > 0)
                {
                    int index = populations.IndexOf(burg.Population);
                    bool isFit = index >= Math.Floor((populations.Count * group.Percentile) / 100.0);
                    if (!isFit) continue;
                }

                // 3. Apply fitting enum type
                burg.Group = group.Type;
                return;
            }
        }

        // Mirrors JS's getDefaultGroups() array using the new bitwise flags
        private static List<BurgGroup> GetDefaultGroups()
        {
            return new List<BurgGroup>
            {
                new BurgGroup { Type = BurgType.Capital, Name = "capital", Active = true, Order = 9, RequiredFeatures = BurgFeature.Capital },
                new BurgGroup { Type = BurgType.City, Name = "city", Active = true, Order = 8, Percentile = 90, Min = 5 },
                new BurgGroup { Type = BurgType.Fort, Name = "fort", Active = true, Order = 6, Max = 1, RequiredFeatures = BurgFeature.Citadel, ForbiddenFeatures = BurgFeature.Walls | BurgFeature.Plaza | BurgFeature.Port },
                new BurgGroup { Type = BurgType.Monastery, Name = "monastery", Active = true, Order = 5, Max = 0.8, RequiredFeatures = BurgFeature.Temple, ForbiddenFeatures = BurgFeature.Walls | BurgFeature.Plaza | BurgFeature.Port },
                new BurgGroup { Type = BurgType.Caravanserai, Name = "caravanserai", Active = true, Order = 4, Max = 0.8, RequiredFeatures = BurgFeature.Plaza, ForbiddenFeatures = BurgFeature.Port, Biomes = new List<int> { 1, 2, 3 } },
                new BurgGroup { Type = BurgType.TradingPost, Name = "trading_post", Active = true, Order = 3, Max = 0.8, RequiredFeatures = BurgFeature.Plaza, Biomes = new List<int> { 5, 6, 7, 8, 9, 10, 11, 12 } },
                new BurgGroup { Type = BurgType.Village, Name = "village", Active = true, Order = 2, Min = 0.1, Max = 2 },
                new BurgGroup { Type = BurgType.Hamlet, Name = "hamlet", Active = true, Order = 1, Max = 0.1, ForbiddenFeatures = BurgFeature.Plaza },
                new BurgGroup { Type = BurgType.Town, Name = "town", Active = true, Order = 7, IsDefault = true }
            };
        }

        private class BurgGroup
        {
            public BurgType Type { get; set; }
            public string Name { get; set; }
            public bool Active { get; set; } = true;
            public int Order { get; set; }
            public bool IsDefault { get; set; }
            public double Min { get; set; }
            public double Max { get; set; }
            public int Percentile { get; set; }
            public List<int> Biomes { get; set; }

            // Enum Flags for rapid bitwise evaluation
            public BurgFeature RequiredFeatures { get; set; }
            public BurgFeature ForbiddenFeatures { get; set; }
        }

        #endregion
    }
}
