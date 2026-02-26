using MapGen.Core.Helpers;

namespace MapGen.Core
{
    public class MapOptions
    {
        // Rng
        public string Seed { get; set; }

        // Grid Options
        public int Width { get; set; }
        public int Height { get; set; }
        public int PointsCount { get; set; }

        // Climate properties
        public double TemperatureEquator { get; set; }
        public double TemperatureNorthPole { get; set; }
        public double TemperatureSouthPole { get; set; }
        public double Precipitation { get; set; }

        // While we don't use these for Climate, they consume RNG 
        // in the JS sequence and must be called to keep the seed in sync.
        public int StatesNumber { get; set; }
        public int ReligionsNumber { get; set; }
        public double GrowthRate { get; set; }
        public int CulturesCount { get; set; }

        public static void RandomizeOptions(MapOptions opt, IRandom rng)
        {
            // JS sequence simulation:
            // 1. States Number: gauss(18, 5, 2, 30)
            opt.StatesNumber = (int)rng.Gauss(18, 5, 2, 30);

            // 2. Provinces Ratio (JS consumes it, we'll just discard/ignore the value)
            rng.Gauss(20, 10, 20, 100);

            // 3. Religions: gauss(6, 3, 2, 10)
            opt.ReligionsNumber = (int)rng.Gauss(6, 3, 2, 10);

            // 4. Size Variety (RNG consumption)
            rng.Gauss(4, 2, 0, 10, 1);

            // 5. Growth Rate: 1 + Math.random()
            opt.GrowthRate = NumberUtils.Round(1 + rng.Next(), 1);

            // 6. Cultures: gauss(12, 3, 5, 30)
            opt.CulturesCount = (int)rng.Gauss(12, 3, 5, 30);

            // 7. Temperature Equator: gauss(25, 7, 20, 35, 0)
            opt.TemperatureEquator = rng.Gauss(25, 7, 20, 35, 0);

            // 8. Temperature North Pole: gauss(-25, 7, -40, 10, 0)
            opt.TemperatureNorthPole = rng.Gauss(-25, 7, -40, 10, 0);

            // 9. Temperature South Pole: gauss(-15, 7, -40, 10, 0)
            opt.TemperatureSouthPole = rng.Gauss(-15, 7, -40, 10, 0);

            // 10. Precipitation: gauss(100, 40, 5, 500)
            opt.Precipitation = rng.Gauss(100, 40, 5, 500);

            // 11. Distance Scale: gauss(3, 1, 1, 5)
            rng.Gauss(3, 1, 1, 5);
        }
    }
}