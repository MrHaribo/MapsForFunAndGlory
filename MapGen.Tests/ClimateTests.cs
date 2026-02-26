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

    public class PrecipitationRegressionData
    {
        public string Seed { get; set; }
        public int[] Winds { get; set; }
        public double PrecipitationModifier { get; set; }
        public byte[] Precipitation { get; set; }
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

        [Fact]
        public void GeneratePrecipitation_MatchesJs()
        {
            // 1. Load Expected Data
            var json = File.ReadAllText("data/regression_precipitation_continents.json");
            var expected = JsonConvert.DeserializeObject<PrecipitationRegressionData>(json);

            // 2. Setup MapData
            var mapData = TestMapData.TestData;

            // Ensure the winds match the dump context exactly
            mapData.Options.Precipitation = expected.PrecipitationModifier;

            // Orchestration sequence
            GridGenerator.Generate(mapData);
            VoronoiGenerator.CalculateVoronoi(mapData);
            HeightmapGenerator.Generate(mapData);
            FeatureModule.MarkupGrid(mapData);
            LakeModule.AddLakesInDeepDepressions(mapData);
            LakeModule.OpenNearSeaLakes(mapData);
            GlobeModule.DefineMapSize(mapData);
            GlobeModule.CalculateMapCoordinates(mapData);
            ClimateModule.CalculateTemperatures(mapData);

            // 3. Execute
            ClimateModule.GeneratePrecipitation(mapData);

            // 4. Assert
            Assert.Equal(expected.Precipitation.Length, mapData.Cells.Length);
            for (int i = 0; i < expected.Precipitation.Length; i++)
            {
                // We use a small tolerance or direct equality depending on rounding
                // Since JS uses Uint8Array and we use byte, it should be exact.
                Assert.Equal(expected.Precipitation[i], mapData.Cells[i].Prec);
            }
        }
    }
}