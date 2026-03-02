using System;
using System.Collections.Generic;
using System.Linq;

namespace MapGen.Core.Modules
{
    public class BiomeDefinition
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public int Habitability { get; set; }
        public int IconsDensity { get; set; }
        public List<string> Icons { get; set; } = new List<string>();
        public int MovementCost { get; set; }
    }

    public static class BiomModule
    {
        // The matrix maps Moisture (Rows 0-4) and Temperature (Columns 0-25)
        private static readonly byte[][] BiomeMatrix = new byte[][]
        {
            new byte[] { 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 10 },
            new byte[] { 3, 3, 3, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 9, 9, 9, 9, 10, 10, 10 },
            new byte[] { 5, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 9, 9, 9, 9, 9, 10, 10, 10 },
            new byte[] { 5, 6, 6, 6, 6, 6, 6, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 9, 9, 9, 9, 9, 9, 10, 10, 10 },
            new byte[] { 7, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 9, 9, 9, 9, 9, 9, 9, 10, 10 }
        };

        // --- Biome Data Arrays ---
        private static readonly string[] BiomeNames = {
            "Marine", "Hot desert", "Cold desert", "Savanna", "Grassland",
            "Tropical seasonal forest", "Temperate deciduous forest", "Tropical rainforest",
            "Temperate rainforest", "Taiga", "Tundra", "Glacier", "Wetland"
        };

        private static readonly string[] BiomeColors = {
            "#466eab", "#fbe79f", "#b5b887", "#d2d082", "#c8d68f",
            "#b6d95d", "#29bc56", "#7dcb35", "#409c43", "#4b6b32",
            "#96784b", "#d5e7eb", "#0b9131"
        };

        private static readonly int[] BiomeHabitability = { 0, 4, 10, 22, 30, 50, 100, 80, 90, 12, 4, 0, 12 };

        private static readonly int[] BiomeDensities = { 0, 3, 2, 120, 120, 120, 120, 150, 150, 100, 5, 0, 250 };

        private static readonly int[] BiomeCosts = { 10, 200, 150, 60, 50, 70, 70, 80, 90, 200, 1000, 5000, 150 };

        private static readonly Dictionary<string, int>[] BiomeRawIcons =
        {
            new Dictionary<string, int>(), // Marine
            new Dictionary<string, int>() {{"dune", 3}, {"cactus", 6}, {"deadTree", 1}}, // Hot desert
            new Dictionary<string, int>() {{"dune", 9}, {"deadTree", 1}},               // Cold desert
            new Dictionary<string, int>() {{"acacia", 1}, {"grass", 9}},                // Savanna
            new Dictionary<string, int>() {{"grass", 1}},                               // Grassland
            new Dictionary<string, int>() {{"acacia", 8}, {"palm", 1}},                 // Tropical seasonal forest
            new Dictionary<string, int>() {{"deciduous", 1}},                           // Temperate deciduous forest
            new Dictionary<string, int>() {{"acacia", 5}, {"palm", 3}, {"deciduous", 1}, {"swamp", 1}}, // Tropical rainforest
            new Dictionary<string, int>() {{"deciduous", 6}, {"swamp", 1}},              // Temperate rainforest
            new Dictionary<string, int>() {{"conifer", 1}},                             // Taiga
            new Dictionary<string, int>() {{"grass", 1}},                               // Tundra
            new Dictionary<string, int>(),                                              // Glacier
            new Dictionary<string, int>() {{"swamp", 1}}                                // Wetland
        };

        public static void Define(MapPack pack, MapData grid)
        {
            // Ensure the Biome array is initialized on the pack cells
            for (int i = 0; i < pack.Cells.Length; i++)
            {
                var cell = pack.Cells[i];

                // 1. Calculate Moisture
                double moisture = cell.H < 20 ? 0 : CalculateMoisture(pack, grid, i);

                // 2. Get Temperature from Grid Reference
                // GridId is the link between the 'Pack' cell and the 'Grid' cell
                double temperature = grid.Cells[cell.GridId].Temp;

                // 3. Determine and assign Biome ID
                // getId helper we created previously
                cell.BiomeId = GetBiomeId(moisture, temperature, cell.H, cell.RiverId != 0);
            }
        }

        private static double CalculateMoisture(MapPack pack, MapData grid, int cellId)
        {
            var cell = pack.Cells[cellId];

            // Start with base precipitation from the grid
            double moisture = grid.Cells[cell.GridId].Prec;

            // Apply River Bonus: Math.max(flux / 10, 2)
            if (cell.RiverId != 0)
            {
                moisture += Math.Max(cell.Flux / 10.0, 2.0);
            }

            // Neighbors Smoothing
            // Filter to only land neighbors, get their precipitation, and include current moisture
            var landNeighborPrec = cell.C
                .Where(nIdx => pack.Cells[nIdx].H >= 20)
                .Select(nIdx => (double)grid.Cells[pack.Cells[nIdx].GridId].Prec)
                .ToList();

            landNeighborPrec.Add(moisture);

            // Calculate mean and add constant 4 (Azgaar's logic)
            // 'rn' in JS is typically a rounding function, usually to 0 or 1 decimal places
            double mean = landNeighborPrec.Average();
            return Math.Round(4.0 + mean);
        }

        public static byte GetBiomeId(double moisture, double temperature, byte height, bool hasRiver)
        {
            // 1. Static Overrides (The "Fast Paths")
            if (height < 20) return 0; // Marine

            // Permafrost / Glacier (Too cold)
            if (temperature < -5) return 11;

            // Hot Desert check (High temp, no river, low moisture)
            if (temperature >= 25 && !hasRiver && moisture < 8) return 1;

            // Wetland check
            if (IsWetland(moisture, temperature, height)) return 12;

            // 2. Matrix Lookup (The "Climate Band" Path)
            // JS: (moisture / 5) | 0
            int moistureBand = Math.Min((int)Math.Floor(moisture / 5), 4);

            // JS: Math.min(Math.max(20 - temperature, 0), 25)
            // This flips the temperature so that 20°C is index 0 and -5°C is index 25
            int temperatureBand = (int)Math.Min(Math.Max(20 - temperature, 0), 25);

            return BiomeMatrix[moistureBand][temperatureBand];
        }

        private static bool IsWetland(double moisture, double temperature, byte height)
        {
            if (temperature <= -2) return false; // Too cold for swamps

            // Near coast wetland logic
            if (moisture > 40 && height < 25) return true;

            // Inland/Highland wetland logic
            if (moisture > 24 && height > 24 && height < 60) return true;

            return false;
        }

        public static List<BiomeDefinition> GetDefaultBiomes()
        {
            var biomes = new List<BiomeDefinition>();

            for (int i = 0; i < BiomeNames.Length; i++)
            {
                var b = new BiomeDefinition
                {
                    Id = i,
                    Name = BiomeNames[i],
                    Color = BiomeColors[i],
                    Habitability = BiomeHabitability[i],
                    IconsDensity = BiomeDensities[i],
                    MovementCost = BiomeCosts[i],
                    Icons = new List<string>()
                };

                // Flatten weighted icons from the static dictionary array
                foreach (var kvp in BiomeRawIcons[i])
                {
                    for (int j = 0; j < kvp.Value; j++)
                    {
                        b.Icons.Add(kvp.Key);
                    }
                }

                biomes.Add(b);
            }

            return biomes;
        }
    }
}
