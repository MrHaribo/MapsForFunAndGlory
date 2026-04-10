
using MapGen.Core;
using MapGen.Core.Modules;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static MapGen.Tests.FeatureTests;

namespace MapGen.Tests
{
    public class RegressionCulturesData
    {
        public List<CultureJson> Cultures { get; set; }
        public ushort[] Cells_Culture { get; set; }
    }

    public class CultureJson
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Code { get; set; }
        public string Color { get; set; }
        public int Center { get; set; }
        public int Base { get; set; }
        public string Type { get; set; }
        public double Expansionism { get; set; }
        public string Shield { get; set; }
    }

    public class RegressionExpansionData
    {
        [JsonProperty("seed")]
        public string Seed { get; set; }

        [JsonProperty("cellsCount")]
        public int CellsCount { get; set; }

        [JsonProperty("cultureMap")]
        public int[] CultureMap { get; set; }

        [JsonProperty("cultures")]
        public List<CultureMetadata> Cultures { get; set; }
    }

    public class CultureMetadata
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("center")]
        public int Center { get; set; }

        [JsonProperty("expansionism")]
        public double Expansionism { get; set; }
    }

    public class CultureTests
    {
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void TestCultureGeneration(int rngOffset)
        {
            // 1. Load Expected Data
            var json = File.ReadAllText($"data/regression_cultures_{rngOffset}.json");
            var expected = JsonConvert.DeserializeObject<RegressionCulturesData>(json);

            // 2. Setup (Full Pipeline)
            var mapData = TestMapData.TestData; // Ensure this matches your JS "Seed" setup
            GridGenerator.Generate(mapData);
            VoronoiGenerator.CalculateVoronoi(mapData);
            HeightmapGenerator.Generate(mapData);
            FeatureModule.MarkupGrid(mapData);
            GlobeModule.DefineMapSize(mapData);
            GlobeModule.CalculateMapCoordinates(mapData);
            ClimateModule.CalculateTemperatures(mapData);
            ClimateModule.GeneratePrecipitation(mapData);

            var pack = PackModule.ReGraph(mapData);
            FeatureModule.MarkupPack(pack);
            RiverModule.Generate(pack);
            BiomModule.Define(pack);
            FeatureModule.DefineGroups(pack);
            FeatureModule.RankCells(pack);

            for (int i = 0; i < rngOffset; i++)
                mapData.Rng.Next();

            // 3. Execution
            CultureModule.Generate(pack);

            // 4. Assertions
            Assert.NotNull(pack.Cultures);
            Assert.Equal(expected.Cultures.Count, pack.Cultures.Count);

            for (int i = 0; i < expected.Cultures.Count; i++)
            {
                var exp = expected.Cultures[i];
                var act = pack.Cultures[i];


                // Core Metadata
                Assert.Equal(exp.Id, act.Id);
                Assert.Equal(exp.Name, act.Name);
                Assert.Equal(exp.Code, act.Code);
                Assert.Equal(exp.Base, act.BaseNameId);
                Assert.Equal(exp.Color, act.Color);

                // Geographical Logic Result
                Assert.Equal(exp.Center, act.CenterCell);

                if (exp.Type == null)
                    Assert.Equal(CultureType.Undefined, act.Type);
                else
                    Assert.Equal(exp.Type.ToLower(), act.Type.ToString().ToLower());

                // Scaling Logic
                Assert.Equal(exp.Expansionism, act.Expansionism); // 1 decimal precision
                Assert.Equal(exp.Shield, act.Shield);
            }

            // 5. Verify the Cell-to-Culture Map (The "Centers" should be marked)
            // In the generate phase, only the centers are usually set in pack.Cells.Culture
            for (int i = 0; i < expected.Cells_Culture.Length; i++)
            {
                // We only check if the center was placed in the right cell
                if (expected.Cells_Culture[i] != 0)
                {
                    Assert.Equal(expected.Cells_Culture[i], pack.Cells[i].CultureId);
                }
            }
        }

        [Fact]
        public void TestCultureExpansion()
        {
            // 1. Load Expected Data
            var json = File.ReadAllText($"data/regression_cultures_expansion.json");
            var expected = JsonConvert.DeserializeObject<RegressionExpansionData>(json);

            // 2. Setup (Full Pipeline)
            var mapData = TestMapData.TestData;
            GridGenerator.Generate(mapData);
            VoronoiGenerator.CalculateVoronoi(mapData);
            HeightmapGenerator.Generate(mapData);
            FeatureModule.MarkupGrid(mapData);
            GlobeModule.DefineMapSize(mapData);
            GlobeModule.CalculateMapCoordinates(mapData);
            ClimateModule.CalculateTemperatures(mapData);
            ClimateModule.GeneratePrecipitation(mapData);

            var pack = PackModule.ReGraph(mapData);
            FeatureModule.MarkupPack(pack);
            RiverModule.Generate(pack);
            BiomModule.Define(pack);
            FeatureModule.DefineGroups(pack);
            FeatureModule.RankCells(pack);

            // 3. Execution
            CultureModule.Generate(pack);
            CultureModule.ExpandCultures(pack);

            // 4. Assertions
            Assert.Equal(expected.Cultures.Count, pack.Cultures.Count);

            Assert.Equal(expected.CellsCount, pack.Cells.Length);

            for (int i = 0; i < pack.Cells.Length; i++)
            {
                int expectedCulture = expected.CultureMap[i];
                int actualCulture = pack.Cells[i].CultureId;

                Assert.Equal(expectedCulture, actualCulture);
            }
        }
    }
}
