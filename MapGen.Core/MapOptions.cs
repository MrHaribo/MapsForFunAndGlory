using MapGen.Core.Helpers;
using MapGen.Core.Modules;
using System;
using System.Collections.Generic;
using static MapGen.Core.Helpers.NumberUtils;

namespace MapGen.Core
{
    public class MapOptions
    {
        public string Seed { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int PointsCount { get; set; }
        public HeightmapTemplate Template { get; set; }

        // Simulation Properties
        public double TemperatureEquator { get; set; }
        public double TemperatureNorthPole { get; set; }
        public double TemperatureSouthPole { get; set; }
        public double Precipitation { get; set; }

        // Logic Values (kept for RNG sync and potential future use)
        public int StatesCount { get; set; }
        public int ReligionsCount { get; set; }
        public double GrowthRate { get; set; }
        public int CulturesCount { get; set; }
        public Culture CultureSet { get; set; }

        public static void RandomizeOptions(MapOptions opt, IRandom rng)
        {
            // --- Heightmap Template ---
            // RNG Order 1: Selection via weighted dictionary
            //opt.Template = rng.Rw(GetHeightmapWeights());

            // --- Political & Social ---
            // RNG Order 2-7
            opt.StatesCount = (int)rng.Gauss(18, 5, 2, 30);
            rng.Gauss(20, 10, 20, 100); // provincesRatio
            opt.ReligionsCount = (int)rng.Gauss(6, 3, 2, 10);
            rng.Gauss(4, 2, 0, 10, 1);  // sizeVariety
            opt.GrowthRate = Round(1 + rng.Next(), 1);
            opt.CulturesCount = (int)rng.Gauss(12, 3, 5, 30);

            // --- Culture Set ---
            // RNG Order 8: Selection via weighted dictionary
            opt.CultureSet = rng.Rw(GetCultureWeights());

            // --- World Configuration ---
            // RNG Order 9-12
            opt.TemperatureEquator = rng.Gauss(25, 7, 20, 35, 0);
            opt.TemperatureNorthPole = rng.Gauss(-25, 7, -40, 10, 0);
            opt.TemperatureSouthPole = rng.Gauss(-15, 7, -40, 10, 0);
            opt.Precipitation = rng.Gauss(100, 40, 5, 500);

            // --- Unit Scaling ---
            // RNG Order 13
            rng.Gauss(3, 1, 1, 5); // distanceScale
        }

        private static Dictionary<HeightmapTemplate, int> GetHeightmapWeights()
        {
            // We initialize a new dictionary to guarantee the insertion (and iteration) 
            // order matches the JavaScript object key order.
            return new Dictionary<HeightmapTemplate, int>
            {
                { HeightmapTemplate.Atoll, 3 },         // JS: atolls
                { HeightmapTemplate.Archipelago, 10 },
                { HeightmapTemplate.HighIsland, 10 },
                { HeightmapTemplate.Mediterranean, 7 },
                { HeightmapTemplate.Peninsula, 5 },
                { HeightmapTemplate.Pangea, 3 },
                { HeightmapTemplate.Continents, 10 },
                { HeightmapTemplate.Shattered, 5 },
                { HeightmapTemplate.LowIsland, 3 }      // JS: lowlands
            };
        }

        private static Dictionary<Culture, int> GetCultureWeights()
        {
            // Matches JS order: world, european, oriental, english, antique, highFantasy, darkFantasy, random
            return new Dictionary<Culture, int>
            {
                { Culture.World, 10 },
                { Culture.European, 10 },
                { Culture.Oriental, 2 },
                { Culture.English, 5 },
                { Culture.Antique, 3 },
                { Culture.HighFantasy, 11 },
                { Culture.DarkFantasy, 3 },
                { Culture.Random, 1 }
            };
        }
    }
}