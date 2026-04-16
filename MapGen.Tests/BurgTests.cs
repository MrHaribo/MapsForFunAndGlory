using MapGen.Core;
using MapGen.Core.Modules;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapGen.Tests
{
    public class RegressionBurgData
    {
        public string Seed { get; set; }
        public int CellsCount { get; set; }
        public ushort[] BurgMap { get; set; }
        public List<ExpectedBurg> Burgs { get; set; }
    }

    public class ExpectedBurg
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Culture { get; set; }
        public bool Capital { get; set; }
        public int State { get; set; }
        public int Cell { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public int Port { get; set; }
    }

    public class BurgTests
    {
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void TestBurgGeneration(int dataCount)
        {
            // 1. Load Expected Data
            var json = File.ReadAllText($"data/regression_burgs_{dataCount}.json");
            var expected = JsonConvert.DeserializeObject<RegressionBurgData>(json);

            // 2. Setup (Full Pipeline as per your streamlined sequence)
            var mapData = TestMapData.TestData; // Ensure this uses expected.Seed
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

            for (int i = 0; i < dataCount; i++)
                pack.Rng.Next();

            // 3. Execution
            BurgModule.Generate(pack);

            // 4. Assertions

            // Assert Burg Count
            Assert.Equal(expected.Burgs.Count, pack.Burgs.Count);

            // Assert Individual Burg Properties
            for (int i = 0; i < expected.Burgs.Count; i++)
            {
                var exp = expected.Burgs[i];
                var act = pack.Burgs[i]; // List is 0-indexed, so GetBurg(exp.Id) would be Burgs[i]

                Assert.Equal(exp.Id, act.Id);
                Assert.Equal(exp.Cell, act.CellId);
                Assert.Equal(exp.State, act.StateId);
                Assert.Equal(exp.Culture, act.CultureId);
                Assert.Equal(exp.Capital, act.IsCapital);
                Assert.Equal(exp.Port, act.PortId);
                Assert.Equal(exp.Name, act.Name);

                // Coordinate Check: Tolerance of 0.01 due to potential Math.Round/float precision diffs
                Assert.Equal(act.Position.X, exp.X);
                Assert.Equal(act.Position.Y, exp.Y);
            }

            // Assert Cell-to-Burg Mapping (the 1-based ID array)
            for (int i = 0; i < expected.BurgMap.Length; i++)
            {
                Assert.Equal(expected.BurgMap[i], pack.Cells[i].BurgId);
            }
        }
    }
}
