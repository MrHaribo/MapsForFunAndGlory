using MapGen.Core.Modules;
using MapGen.Tests;
using Newtonsoft.Json;


namespace MapGen.Tests
{
    public class RiverTests
    {
        // --- Test Data Schema ---

        public class RiverRegressionData
        {
            public int RiverCount { get; set; }
            public int ConfluenceCount { get; set; }
            public List<RiverEntry> Rivers { get; set; }
            public List<CellHydrologyEntry> Cells { get; set; }
        }

        public class RiverEntry
        {
            public int Id { get; set; }
            public int Parent { get; set; }
            public int Source { get; set; }
            public int Mouth { get; set; }
            public double Discharge { get; set; }
            public double Length { get; set; }
            public double Width { get; set; }
            public double WidthFactor { get; set; }
            public double SourceWidth { get; set; }
            public int CellCount { get; set; }
            public int[] CellSample { get; set; }
        }

        public class CellHydrologyEntry
        {
            [JsonProperty("i")]
            public int Index { get; set; }
            [JsonProperty("r")]
            public int RiverId { get; set; }
            [JsonProperty("c")]
            public double Confluence { get; set; }
            [JsonProperty("h")]
            public double Height { get; set; }
            [JsonProperty("f")]
            public double Flux { get; set; }
        }

        [Fact]
        public void TestRiverFullBitwiseParity()
        {
            // 1. Load Data
            var json = File.ReadAllText("data/regression_rivers.json");
            var expected = JsonConvert.DeserializeObject<RiverRegressionData>(json);

            // 2. Setup (Ensure this matches the JS state exactly)
            var mapData = TestMapData.TestData;
            GridGenerator.Generate(mapData);
            VoronoiGenerator.CalculateVoronoi(mapData);
            HeightmapGenerator.Generate(mapData);
            FeatureModule.MarkupGrid(mapData);
            LakeModule.AddLakesInDeepDepressions(mapData);
            LakeModule.OpenNearSeaLakes(mapData);
            GlobeModule.DefineMapSize(mapData);
            GlobeModule.CalculateMapCoordinates(mapData);
            ClimateModule.CalculateTemperatures(mapData);
            ClimateModule.GeneratePrecipitation(mapData);

            var pack = PackModule.ReGraph(mapData);
            FeatureModule.MarkupPack(pack);

            // 3. Run
            RiverModule.Generate(pack, mapData, allowErosion: true);

            // --- STRICT ASSERTIONS ---

            // A. Global Counts
            Assert.Equal(expected.RiverCount, pack.Rivers.Count);
            Assert.Equal(expected.ConfluenceCount, pack.Cells.Count(c => c.Confluence > 0));

            // B. Cell-Level Hydrology (The "Engine" of the river system)
            foreach (var expCell in expected.Cells)
            {
                var actCell = pack.Cells[expCell.Index];

                // Verify River Assignment & Logic Flow
                Assert.Equal(expCell.RiverId, actCell.RiverId);

                // Note: If this fails, ensure C# uses Math.Round(flux, MidpointRounding.AwayFromZero) 
                // to match JS Math.round() behavior for confluences
                Assert.Equal(expCell.Confluence, (double)actCell.Confluence);

                // Verify Downcutting (Erosion) results
                Assert.Equal((byte)expCell.Height, actCell.Height);

                // Verify Precipitation/Lake Drainage Flux
                Assert.Equal(expCell.Flux, (double)actCell.Flux);
            }

            // C. River Metadata (The "Output" of the river system)
            foreach (var expRiver in expected.Rivers)
            {
                var actRiver = pack.Rivers.FirstOrDefault(r => r.Id == expRiver.Id);
                Assert.NotNull(actRiver);

                Assert.Equal(expRiver.Parent, (int)actRiver.Parent);
                Assert.Equal(expRiver.Source, actRiver.Source);
                Assert.Equal(expRiver.Mouth, actRiver.Mouth);
                Assert.Equal(expRiver.CellCount, actRiver.Cells.Count);

                // Discharge must be exact bitwise match
                Assert.Equal(expRiver.Discharge, actRiver.Discharge);

                // Physical attributes derived from Meandering path
                Assert.Equal(expRiver.Length, actRiver.Length);
                Assert.Equal(expRiver.Width, actRiver.Width);
                Assert.Equal(expRiver.SourceWidth, actRiver.SourceWidth);

                // Path sequence verification
                for (int i = 0; i < expRiver.CellSample.Length; i++)
                {
                    int actCellId = i < 3
                        ? actRiver.Cells[i]
                        : actRiver.Cells[actRiver.Cells.Count - (6 - i)];

                    Assert.Equal(expRiver.CellSample[i], actCellId);
                }
            }
        }
    }
}