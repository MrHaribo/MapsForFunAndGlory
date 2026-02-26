using MapGen.Core;
using MapGen.Core.Modules;
using Newtonsoft.Json;
using Xunit;

namespace MapGen.Tests
{
    public class TemperatureRegressionData
    {
        public string Template { get; set; }
        public MapCoordinates Coords { get; set; }
        public sbyte[] Temperatures { get; set; }
    }

    public class ClimateTests
    {
        [Fact]
        public void CalculateTemperatures_MatchesJs()
        {
            // 1. Setup Data
            var json = File.ReadAllText("data/regression_temperatures_continents.json");
            var expected = JsonConvert.DeserializeObject<TemperatureRegressionData>(json);

            var mapData = TestMapData.TestData;

            // 2. Orchestration
            GridGenerator.Generate(mapData);
            VoronoiGenerator.CalculateVoronoi(mapData);
            HeightmapGenerator.Generate(mapData);
            FeatureModule.MarkupGrid(mapData);
            LakeModule.AddLakesInDeepDepressions(mapData);
            LakeModule.OpenNearSeaLakes(mapData);
            GlobeModule.DefineMapSize(mapData);
            GlobeModule.CalculateMapCoordinates(mapData);

            // 3. Execute
            ClimateModule.CalculateTemperatures(mapData);

            // 4. Assert
            Assert.Equal(expected.Temperatures.Length, mapData.Cells.Length);
            for (int i = 0; i < expected.Temperatures.Length; i++)
            {
                Assert.Equal(expected.Temperatures[i], mapData.Cells[i].Temp);
            }
        }
    }
}