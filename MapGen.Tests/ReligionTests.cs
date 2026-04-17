using MapGen.Core.Modules;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapGen.Tests
{
    public class RegressionReligionsData
    {
        public List<RegressionReligion> religions { get; set; }
        public int[] cells_religion { get; set; }
    }

    public class RegressionReligion
    {
        public int id { get; set; }
        public string name { get; set; }
        public string color { get; set; }
        public int cultureId { get; set; }
        public string group { get; set; } // Maps to ReligionGroup enum
        public string form { get; set; }
        public string deity { get; set; }
        public string expansion { get; set; }
        public double expansionism { get; set; }
        public int centerCell { get; set; }
        public int cellsCount { get; set; }
        public double totalArea { get; set; }
        public double ruralPopulation { get; set; }
        public double urbanPopulation { get; set; }
        public List<int> origins { get; set; }
        public string code { get; set; }
    }

    public class ReligionTests
    {
        [Fact]
        public void TestReligions_MatchesRegression()
        {
            // 1. Load Expected Data
            var json = File.ReadAllText($"data/regression_religions.json");
            var expectedData = JsonConvert.DeserializeObject<RegressionReligionsData>(json);

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
            CultureModule.Generate(pack);
            CultureModule.ExpandCultures(pack);
            BurgModule.Generate(pack);
            StateModule.Generate(pack);
            RouteModule.Generate(pack);

            // 3. Execute
            ReligionModule.Generate(pack);

            // --- 4. Assertions ---

            Assert.NotNull(pack.Religions);
            Assert.Equal(expectedData.religions.Count, pack.Religions.Count);

            for (int i = 0; i < expectedData.religions.Count; i++)
            {
                var exp = expectedData.religions[i];
                var act = pack.Religions[i];

                Assert.Equal(exp.id, act.Id);
                Assert.Equal(exp.name, act.Name);
                Assert.Equal(exp.color, act.Color);
                Assert.Equal(exp.cultureId, act.CultureId);

                // ToString matches "Folk", "Organized", "Cult", "Heresy"
                if (act.Id != 0)
                    Assert.Equal(exp.group, act.Group.ToString());

                Assert.Equal(exp.form, act.Form);
                Assert.Equal(exp.deity, act.Deity);
                Assert.Equal(exp.expansion, act.Expansion);
                Assert.Equal(exp.centerCell, act.CenterCell);
                Assert.Equal(exp.code, act.Code);

                // Floats from Gaussian distributions/mixes might drift slightly, so use tolerance
                Assert.Equal(exp.expansionism, act.Expansionism, 2);

                // Stats (Often 0 at this stage of generation, but good to assert)
                Assert.Equal(exp.cellsCount, act.CellsCount);
                Assert.Equal(exp.totalArea, act.TotalArea, 2);
                Assert.Equal(exp.ruralPopulation, act.RuralPopulation, 2);
                Assert.Equal(exp.urbanPopulation, act.UrbanPopulation, 2);

                // Assert Origins
                Assert.NotNull(act.Origins);
                Assert.True(exp.origins.SequenceEqual(act.Origins), $"Origins mismatch on religion {act.Id}: expected [{string.Join(",", exp.origins)}] but got [{string.Join(",", act.Origins)}]");
            }

            // 5. Assert Cell Religion Map (The Expansion Algorithm output)
            Assert.Equal(expectedData.cells_religion.Length, pack.Cells.Length);
            for (int i = 0; i < pack.Cells.Length; i++)
            {
                Assert.Equal(expectedData.cells_religion[i], pack.Cells[i].ReligionId);
            }
        }
    }
}
