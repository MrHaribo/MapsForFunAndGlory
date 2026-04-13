using MapGen.Core.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MapGen.Core.Modules
{
    public static class StateModule
    {
        private struct ExpansionNode
        {
            public int CellId;
            public double Cost;
            public int StateId;
            public byte NativeBiome;
        }

        public static void Generate(MapPack pack)
        {
            pack.States = CreateStates(pack);

            ExpandStates(pack);
            Normalize(pack);
            // GetPoles(pack);           // Requires Polylabel/Centroid math
            FindNeighbors(pack);
            AssignColors(pack);
            CollectStatistics(pack);     // Fills the Area/Population fields
            // GenerateCampaigns(pack);  // Requires Campaign model array on MapState
            // GenerateDiplomacy(pack);  // Requires Diplomacy model array on MapState

            // --- Local Functions ---

            List<MapState> CreateStates(MapPack pack)
            {
                var rng = pack.Rng;
                var burgs = pack.Burgs;
                var cultures = pack.Cultures;

                // State 0 is always "Neutrals"
                var states = new List<MapState>
                {
                    new MapState { Id = 0, Name = "Neutrals", Color = "#777777" }
                };

                double sizeVariety = MapConstants.STATE_SIZE_VARIETY;

                foreach (var burg in burgs)
                {
                    if (!burg.IsCapital) continue;

                    // JS: Math.random() is a double between 0 and 1. Use rng.NextDouble()
                    double expansionism = Math.Round(rng.Next() * sizeVariety + 1, 1);

                    bool isEach5th = (burg.Cell % 5 == 0);
                    string basename = (burg.Name.Length < 9 && isEach5th)
                        ? burg.Name
                        : NameModule.GetCultureShort(rng, cultures[burg.CultureId].BaseNameId);

                    string name = NameModule.GetStateName(rng, basename, cultures[burg.CultureId].BaseNameId);
                    CultureType type = cultures[burg.CultureId].Type;

                    var coa = new MapCoA { Type = type };

                    var state = new MapState
                    {
                        Id = burg.Id, // State ID in the list matches Capital Burg ID
                        Name = name,
                        Expansionism = expansionism,
                        CapitalId = burg.Id,
                        Type = type,
                        CenterCell = burg.Cell,
                        CultureId = burg.CultureId,
                        CoA = coa
                    };

                    burg.StateId = state.Id;
                    states.Add(state);
                }

                return states;
            }
        }

        private static void ExpandStates(MapPack pack)
        {
            var cells = pack.Cells;
            var states = pack.States;
            var cultures = pack.Cultures;
            var burgs = pack.Burgs;

            // C# 10+ PriorityQueue is perfect for Azgaar's FlatQueue
            var queue = new PriorityQueue<ExpansionNode, double>();
            var cost = new double[cells.Length];

            var bioms = BiomModule.GetDefaultBiomes();

            double globalGrowthRate = 1.0;
            double statesGrowthRate = 1.0;
            double growthRate = (cells.Length / 2.0) * globalGrowthRate * statesGrowthRate;

            // Clear state from all cells initially
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i].StateId = 0;
                cost[i] = double.MaxValue;
            }

            var stateDict = states.ToDictionary(s => s.Id);

            // Queue initial capitals
            foreach (var state in states)
            {
                if (state.Id == 0) continue; // Skip Neutrals

                int capitalCell = burgs[state.CapitalId - 1].Cell; // Remember: Burg Id is 1-based, list is 0-based
                cells[capitalCell].StateId = state.Id;

                int cultureCenter = cultures[state.CultureId].CenterCell;
                byte nativeBiome = cells[cultureCenter].BiomeId;

                queue.Enqueue(new ExpansionNode { CellId = state.CenterCell, Cost = 0, StateId = state.Id, NativeBiome = nativeBiome }, 0);
                cost[state.CenterCell] = 1;
            }

            while (queue.TryDequeue(out ExpansionNode next, out double p))
            {
                int e = next.CellId;
                int s = next.StateId;
                byte b = next.NativeBiome;

                var state = stateDict[s];
                CultureType type = state.Type;
                int culture = state.CultureId;

                foreach (int neighbor in cells[e].NeighborCells)
                {
                    // Do not overwrite capital cells
                    if (cells[neighbor].StateId != 0 && cells[neighbor].BurgId > 0 && burgs[cells[neighbor].BurgId - 1].IsCapital)
                        continue;

                    double cultureCost = (culture == cells[neighbor].CultureId) ? -9 : 100;
                    double populationCost = cells[neighbor].Height < 20 ? 0 :
                        (cells[neighbor].Suitability > 0 ? Math.Max(20 - cells[neighbor].Suitability, 0) : 5000);

                    double biomeCost = GetBiomeCost(b, cells[neighbor].BiomeId, type, bioms);
                    double heightCost = GetHeightCost(cells[neighbor], pack, type);
                    double riverCost = GetRiverCost(cells[neighbor], type);
                    double typeCost = GetTypeCost(cells[neighbor], type);

                    double cellCost = Math.Max(cultureCost + populationCost + biomeCost + heightCost + riverCost + typeCost, 0);
                    double totalCost = p + 10 + (cellCost / state.Expansionism);

                    if (totalCost > growthRate) continue;

                    if (totalCost < cost[neighbor])
                    {
                        if (cells[neighbor].Height >= 20) cells[neighbor].StateId = s;
                        cost[neighbor] = totalCost;
                        queue.Enqueue(new ExpansionNode { CellId = neighbor, Cost = totalCost, StateId = s, NativeBiome = b }, totalCost);
                    }
                }
            }

            // Reassign non-capital burgs to the state that captured their cell
            foreach (var burg in burgs)
            {
                burg.StateId = cells[burg.Cell].StateId;
            }
        }

        private static void Normalize(MapPack pack)
        {
            var cells = pack.Cells;
            var burgs = pack.Burgs;

            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i].Height < 20 || cells[i].BurgId > 0) continue;

                // Do not overwrite near capitals
                bool nearCapital = cells[i].NeighborCells.Any(c => cells[c].BurgId > 0 && burgs[cells[c].BurgId - 1].IsCapital);
                if (nearCapital) continue;

                var landNeighbors = cells[i].NeighborCells.Where(c => cells[c].Height >= 20).ToList();
                var adversaries = landNeighbors.Where(c => cells[c].StateId != cells[i].StateId).ToList();
                if (adversaries.Count < 2) continue;

                var buddies = landNeighbors.Where(c => cells[c].StateId == cells[i].StateId).ToList();
                if (buddies.Count > 2) continue;
                if (adversaries.Count <= buddies.Count) continue;

                cells[i].StateId = cells[adversaries[0]].StateId;
            }
        }

        private static void FindNeighbors(MapPack pack)
        {
            var cells = pack.Cells;
            var stateDict = pack.States.ToDictionary(s => s.Id);

            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i].Height < 20) continue;
                int s = cells[i].StateId;
                if (s == 0 || !stateDict.ContainsKey(s)) continue;

                foreach (int c in cells[i].NeighborCells)
                {
                    if (cells[c].Height >= 20 && cells[c].StateId != s)
                    {
                        int neighborState = cells[c].StateId;
                        if (neighborState != 0 && !stateDict[s].Neighbors.Contains(neighborState))
                        {
                            stateDict[s].Neighbors.Add(neighborState);
                        }
                    }
                }
            }
        }

        private static void AssignColors(MapPack pack)
        {
            var states = pack.States;
            // d3.schemeSet2 equivalent palette
            var colors = new List<string> { "#66c2a5", "#fc8d62", "#8da0cb", "#e78ac3", "#a6d854", "#ffd92f" };
            var stateDict = states.ToDictionary(s => s.Id);

            foreach (var state in states)
            {
                if (state.Id == 0) continue;

                string assignedColor = colors.FirstOrDefault(color =>
                    state.Neighbors.All(neibId => stateDict.ContainsKey(neibId) && stateDict[neibId].Color != color));

                if (assignedColor == null)
                {
                    assignedColor = ColorUtils.GetRandomColor(pack.Rng);
                }
                else
                {
                    // Rotate palette
                    colors.Remove(assignedColor);
                    colors.Add(assignedColor);
                }
                state.Color = assignedColor;
            }

            // If multiple states share a color, slightly mutate the others (Assuming a MixColor helper exists)
            // ColorUtils.MutateDuplicates(states); 
        }

        private static void CollectStatistics(MapPack pack)
        {
            var cells = pack.Cells;
            var stateDict = pack.States.ToDictionary(s => s.Id);

            foreach (var s in pack.States)
            {
                s.Area = 0;
                s.Population = 0;
                s.BurgsCount = 0;
            }

            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i].Height < 20) continue;
                int sId = cells[i].StateId;
                if (sId == 0 || !stateDict.ContainsKey(sId)) continue;

                var state = stateDict[sId];
                state.Area += cells[i].Area;
                state.Population += (int)cells[i].Population; // Rural population

                if (cells[i].BurgId > 0)
                {
                    state.BurgsCount++;
                    // state.Population += pack.Burgs[cells[i].BurgId - 1].Population; // Add Urban
                }
            }
        }

        // --- Helper Methods for ExpandStates ---

        private static double GetBiomeCost(byte nativeBiome, byte currentBiome, CultureType type, List<BiomeDefinition> bioms)
        {
            if (nativeBiome == currentBiome) return 10;

            double cost = currentBiome < bioms.Count ? bioms[currentBiome].MovementCost : 50;

            if (type == CultureType.Hunting) return cost * 2;
            if (type == CultureType.Nomadic && currentBiome > 4 && currentBiome < 10) return cost * 3;

            return cost;
        }

        private static double GetHeightCost(MapCell cell, MapPack pack, CultureType type)
        {
            byte h = cell.Height;
            bool isLake = cell.FeatureId > 0 && pack.GetFeature(cell.FeatureId).Type == FeatureType.Lake;

            if (type == CultureType.Lake && isLake) return 10;
            if (type == CultureType.Naval && h < 20) return 300;
            if (type == CultureType.Nomadic && h < 20) return 10000;
            if (h < 20) return 1000;
            if (type == CultureType.Highland && h < 62) return 1100;
            if (type == CultureType.Highland) return 0;
            if (h >= 67) return 2200;
            if (h >= 44) return 300;
            return 0;
        }

        private static double GetRiverCost(MapCell cell, CultureType type)
        {
            if (type == CultureType.River) return cell.RiverId > 0 ? 0 : 100;
            if (cell.RiverId == 0) return 0;
            return Math.Clamp(cell.Flux / 10.0, 20, 100);
        }

        private static double GetTypeCost(MapCell cell, CultureType type)
        {
            int t = cell.Distance; // 1 = coast, >1 = mainland, <0 = water
            if (t == 1) return (type == CultureType.Naval || type == CultureType.Lake) ? 0 : (type == CultureType.Nomadic ? 60 : 20);
            if (t == 2) return (type == CultureType.Naval || type == CultureType.Nomadic) ? 30 : 0;
            if (t != -1) return (type == CultureType.Naval || type == CultureType.Lake) ? 100 : 0;
            return 0;
        }
    }
}