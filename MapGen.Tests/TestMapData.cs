using MapGen.Core;
using MapGen.Core.Helpers;
using MapGen.Core.Modules;

namespace MapGen.Tests
{
    public static class TestMapData
    {
        public static MapOptions TestOptions => new MapOptions
        {
            Seed = "42",
            Width = 1920,
            Height = 1080,
            PointsCount = 2000,
        };

        public static MapData TestData
        {
            get
            {
                var options = TestOptions;
                var rng = new AleaRandom(options.Seed);

                MapOptions.RandomizeOptions(options, rng);
                options.Template = HeightmapTemplate.Continents;

                var mapData = new MapData
                {
                    Rng = rng,
                    Options = options,
                };

                return mapData;
            }

        }
    }
}
