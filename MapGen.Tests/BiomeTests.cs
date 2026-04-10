using MapGen.Core.Modules;
using Newtonsoft.Json;
using Xunit;

namespace MapGen.Tests
{
    public class BiomeTests
    {
        public class BiomeRegressionData
        {
            public int CellCount { get; set; }
            public List<CellBiomeEntry> Cells { get; set; }
        }

        public class CellBiomeEntry
        {
            [JsonProperty("i")]
            public int Index { get; set; }
            [JsonProperty("b")]
            public byte BiomeId { get; set; }
            [JsonProperty("t")]
            public double Temperature { get; set; }
            [JsonProperty("p")]
            public double Precipitation { get; set; }
            [JsonProperty("m")]
            public double Moisture { get; set; }
        }

        [Fact]
        public void TestBiomeBitwiseParity()
        {
            // 1. Load Data
            var json = File.ReadAllText("data/regression_biomes.json");
            var expected = JsonConvert.DeserializeObject<BiomeRegressionData>(json);

            // 2. Setup (Full Pipeline up to Biomes)
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

            // Biomes require river flux for moisture bonuses
            RiverModule.Generate(pack, allowErosion: true);

            // 3. Run the Module Under Test
            BiomModule.Define(pack);

            // 4. Assertions
            Assert.Equal(expected.CellCount, pack.Cells.Length);

            foreach (var exp in expected.Cells)
            {
                var actCell = pack.Cells[exp.Index];
                var actGridCell = mapData.Cells[actCell.GridId];

                // Check Temperature Parity (Input)
                Assert.Equal(exp.Temperature, actGridCell.Temp);

                // Check Raw Precipitation Parity (Input)
                Assert.Equal(exp.Precipitation, actGridCell.Prec);

                // Check Moisture Calculation Parity (Intermediate)
                // This is where most logic errors occur due to river flux or averaging
                double actMoisture = actCell.Height < 20 ? 0 : BiomModule.CalculateMoisture(pack, exp.Index);
                Assert.Equal(exp.Moisture, actMoisture);

                // Check Biome ID Parity (Final Output)
                Assert.Equal(exp.BiomeId, actCell.BiomeId);
            }
        }

        //[Fact]
        //public void DumpBiomeDiagnostics()
        //{
        //    // 1. Setup (Standard Pipeline)
        //    var mapData = TestMapData.TestData;
        //    GridGenerator.Generate(mapData);
        //    VoronoiGenerator.CalculateVoronoi(mapData);
        //    HeightmapGenerator.Generate(mapData);
        //    FeatureModule.MarkupGrid(mapData);
        //    GlobeModule.DefineMapSize(mapData);
        //    GlobeModule.CalculateMapCoordinates(mapData);
        //    ClimateModule.CalculateTemperatures(mapData);
        //    ClimateModule.GeneratePrecipitation(mapData);

        //    var pack = PackModule.ReGraph(mapData);
        //    FeatureModule.MarkupPack(pack);
        //    RiverModule.Generate(pack, mapData, allowErosion: true);

        //    // 2. Run Module
        //    BiomModule.Define(pack, mapData);

        //    // 3. Create Dynamic Dump
        //    var diagnostics = new
        //    {
        //        cellCount = pack.Cells.Length,
        //        cells = pack.Cells.Select(c => new
        //        {
        //            i = c.Index,
        //            b = c.BiomeId,
        //            t = mapData.Cells[c.GridId].Temp,
        //            p = mapData.Cells[c.GridId].Prec,
        //            // Capture intermediate moisture calculation
        //            m = c.Height < 20 ? (int)0 : (int)BiomModule.CalculateMoisture(pack, mapData, c.Index),
        //            //h = c.H,
        //            //r = c.RiverId != 0
        //        }).ToList()
        //    };

        //    string json = JsonConvert.SerializeObject(diagnostics, Formatting.Indented);
        //    File.WriteAllText("D:\\downloads\\diagnostics_biomes.json", json);
        //}
    }
}