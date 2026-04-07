
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

    public class CultureTests
    {
        [Fact]
        public void TestCultureGeneration()
        {
            // 1. Load Expected Data
            var json = File.ReadAllText("data/regression_cultures.json");
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
            RiverModule.Generate(pack, mapData, allowErosion: true);
            BiomModule.Define(pack, mapData);
            FeatureModule.DefineGroups(pack);
            FeatureModule.RankCells(pack);

            mapData.Rng.Init(mapData.Options.Seed);

            var a = mapData.Rng.Next();
            var b = mapData.Rng.Next();
            var c = mapData.Rng.Next();


            // 3. Execution
            // Note: Use a fixed seed in your IRandom to match the JS dump!
            CultureModule.Generate(pack, mapData, 9);

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

                // Geographical Logic Result
                Assert.Equal(exp.Center, act.CenterCell);

                if (exp.Type == null)
                    Assert.Equal(CultureType.Undefined, act.Type);
                else
                    Assert.Equal(exp.Type.ToLower(), act.Type.ToString().ToLower());

                // Scaling Logic
                Assert.Equal(exp.Expansionism, act.Expansionism, 1); // 1 decimal precision
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
    }
}
