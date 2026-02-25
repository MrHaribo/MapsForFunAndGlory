using MapGen.Core;
using MapGen.Core.Helpers;
using MapGen.Core.Modules;
using Newtonsoft.Json;
using Xunit;

namespace MapGen.Tests
{
    public class GlobeRegressionData
    {
        public string Template { get; set; }
        public string Seed { get; set; }
        public double Size { get; set; }
        public double Lat { get; set; }
        public double Lon { get; set; }
        public MapCoordinates Coords { get; set; }
    }

    public class GlobeTests
    {
        private GlobeRegressionData LoadExpected()
        {
            var json = File.ReadAllText("data/regression_globe_continents.json");
            return JsonConvert.DeserializeObject<GlobeRegressionData>(json);
        }

        [Fact]
        public void DefineMapSize_MatchesJs()
        {
            // 1. Setup
            var expected = LoadExpected();

            // 2. Setup MapData with the exact topology from our test setup
            var options = GenerationOptions.TestOptions; // Ensure this is Seed 42
            var rng = new AleaRandom(options.Seed);
            var generator = new MapGenerator();
            generator.Generate(options, rng); // Builds the Voronoi Graph
            var data = generator.Data;
            data.Template = HeightmapTemplate.Continents;

            // 3. Run the same template as in JS (resetting seed to match JS heightmap state)
            rng = new AleaRandom(options.Seed);
            HeightmapGenerator.Generate(data, HeightmapTemplate.Continents, rng);

            // 2. Requires feature detection for "touchesBorder" check
            rng = new AleaRandom(options.Seed);
            FeatureModule.MarkupGrid(data);

            // 3. Lake Module
            LakeModule.AddLakesInDeepDepressions(data, MapConstants.DEFAULT_LAKE_ELEV_LIMIT);
            LakeModule.OpenNearSeaLakes(data, HeightmapTemplate.Test);

            // 4. Execute Placement Logic
            GlobeModule.DefineMapSize(data, rng);

            // 5. Assert (The "Where" of the map)
            Assert.Equal(expected.Size, data.MapSize);
            Assert.Equal(expected.Lat, data.Latitude);
            Assert.Equal(expected.Lon, data.Longitude);
        }

        [Fact]
        public void CalculateCoordinates_MatchesJs()
        {
            // 1. Setup
            var expected = LoadExpected();
            // Use standard 1000x500 to match JS graphWidth/Height
            var data = new MapData(2000, 1920, 1080);

            // 2. Inject the specific inputs from the dump to isolate coordinate math
            data.MapSize = expected.Size;
            data.Latitude = expected.Lat;
            data.Longitude = expected.Lon;

            // 3. Execute Coordinate Math
            GlobeModule.CalculateMapCoordinates(data);

            // 4. Assert (The "Degrees" on the globe)
            Assert.Equal(expected.Coords.LatT, data.Coords.LatT);
            Assert.Equal(expected.Coords.LatN, data.Coords.LatN);
            Assert.Equal(expected.Coords.LatS, data.Coords.LatS);
            Assert.Equal(expected.Coords.LonT, data.Coords.LonT);
            Assert.Equal(expected.Coords.LonE, data.Coords.LonE);
            Assert.Equal(expected.Coords.LonW, data.Coords.LonW);
        }
    }
}