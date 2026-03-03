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
        public RegressionMapCoordinates Coords { get; set; }
    }

    public class RegressionMapCoordinates
    {
        public double LatT { get; set; } // Total Latitude span
        public double LatN { get; set; } // Northern Latitude
        public double LatS { get; set; } // Southern Latitude
        public double LonT { get; set; } // Total Longitude span
        public double LonW { get; set; } // Western Longitude
        public double LonE { get; set; } // Eastern Longitude
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
            var mapData = TestMapData.TestData;
            GridGenerator.Generate(mapData);
            VoronoiGenerator.CalculateVoronoi(mapData);
            HeightmapGenerator.Generate(mapData);
            FeatureModule.MarkupGrid(mapData);
            LakeModule.AddLakesInDeepDepressions(mapData);
            LakeModule.OpenNearSeaLakes(mapData);

            // 3. Execute Placement Logic
            GlobeModule.DefineMapSize(mapData);

            // 4. Assert (The "Where" of the map)
            Assert.Equal(expected.Size, mapData.MapSize);
            Assert.Equal(expected.Lat, mapData.Latitude);
            Assert.Equal(expected.Lon, mapData.Longitude);
        }

        [Fact]
        public void CalculateCoordinates_MatchesJs()
        {
            // 1. Setup
            var expected = LoadExpected();
            // Use standard 1000x500 to match JS graphWidth/Height
            var options = new MapOptions 
            {
                PointsCount = 2000,
                Width = 1920,
                Height = 1080
            };

            var data = new MapData { Options = options };

            // 2. Inject the specific inputs from the dump to isolate coordinate math
            data.MapSize = expected.Size;
            data.Latitude = expected.Lat;
            data.Longitude = expected.Lon;

            // 3. Execute Coordinate Math
            GlobeModule.CalculateMapCoordinates(data);

            // 4. Assert (The "Degrees" on the globe)
            Assert.Equal(expected.Coords.LatT, data.Coords.LatTotal);
            Assert.Equal(expected.Coords.LatN, data.Coords.LatNorth);
            Assert.Equal(expected.Coords.LatS, data.Coords.LatSouth);
            Assert.Equal(expected.Coords.LonT, data.Coords.LonTotal);
            Assert.Equal(expected.Coords.LonE, data.Coords.LonEast);
            Assert.Equal(expected.Coords.LonW, data.Coords.LonWest);
        }
    }
}