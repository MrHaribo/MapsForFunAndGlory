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
    public class RiverTests
    {
        public class RiverRegressionData
        {
            public int RiverCount { get; set; }
            public int ConfluenceCount { get; set; }
            public List<RiverEntry> Rivers { get; set; }
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
            public List<int> CellSample { get; set; }
        }

        [Fact]
        public void TestRiverParity()
        {
            // 1. Load the specific feature dump from JS
            var json = File.ReadAllText("data/regression_rivers.json");
            var expected = JsonConvert.DeserializeObject<RiverRegressionData>(json);

            // 2. Prepare MapData (Existing pipeline)
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

            // 3. Run the target module
            RiverModule.Generate(pack, mapData); // Passing the grid for Prec/Temp access

            // --- ASSERTIONS ---

            // A. Global Counts
            Assert.True(expected.RiverCount == pack.Rivers.Count,
                $"River count mismatch. Expected {expected.RiverCount}, got {pack.Rivers.Count}");

            int actualConfluences = pack.Cells.Count(c => c.Confluence > 0);
            Assert.True(expected.ConfluenceCount == actualConfluences,
                $"Confluence count mismatch. Expected {expected.ConfluenceCount}, got {actualConfluences}");

            // B. Deep Dive into River logic
            foreach (var expRiver in expected.Rivers)
            {
                var actRiver = pack.Rivers.FirstOrDefault(r => r.Id == expRiver.Id);
                Assert.True(actRiver != null, $"River ID {expRiver.Id} missing in C# output");

                // Hierarchy and Connectivity
                Assert.Equal(expRiver.Parent, actRiver.Parent);
                Assert.Equal(expRiver.Source, actRiver.Source);
                Assert.Equal(expRiver.Mouth, actRiver.Mouth);
                Assert.Equal(expRiver.CellCount, actRiver.Cells.Count);

                // Simulation Data (Hydrology) - Tolerance allowed for floating point
                Assert.InRange(actRiver.Discharge, expRiver.Discharge - 0.1, expRiver.Discharge + 0.1);

                // Geometric Data (Meandering result)
                // Note: Width and Length depend on the meandering path being identical
                Assert.InRange(actRiver.Length, expRiver.Length - 0.5, expRiver.Length + 0.5);
                Assert.InRange(actRiver.Width, expRiver.Width - 0.05, expRiver.Width + 0.05);
                Assert.InRange(actRiver.SourceWidth, expRiver.SourceWidth - 0.01, expRiver.SourceWidth + 0.01);

                // Path Sample Integrity
                // Ensuring the first and last parts of the river follow the same cell sequence
                for (int i = 0; i < 3; i++)
                {
                    Assert.Equal(expRiver.CellSample[i], actRiver.Cells[i]);
                }
            }
        }
    }
}
