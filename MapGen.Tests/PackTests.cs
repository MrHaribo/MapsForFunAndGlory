using MapGen.Core;
using MapGen.Core.Modules;
using Newtonsoft.Json;

namespace MapGen.Tests
{
    public class PackProbe
    {
        public double X { get; set; }
        public double Y { get; set; }
        public int ExpectedIndex { get; set; }
    }

    public class PackRegressionData
    {
        public List<MapPoint> Points { get; set; }
        public int[] GridMapping { get; set; }
        public byte[] Heights { get; set; }
        public ushort[] Areas { get; set; }
        public List<PackProbe> Probes { get; set; }
    }

    public class PackTests
    {
        [Fact]
        public void ReGraph_MatchesJsPackAndQuadtree()
        {
            // 1. Load the JS Dump
            var json = File.ReadAllText("data/regression_pack_full.json");
            var expected = JsonConvert.DeserializeObject<PackRegressionData>(json);

            // 2. Prepare the Input MapData
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

            // 3. Execute ReGraph
            var pack = PackModule.ReGraph(mapData);

            // 4. Assert Point/Coordinate Parity
            Assert.Equal(expected.Points.Count, pack.Points.Length);
            for (int i = 0; i < expected.Points.Count; i++)
            {
                // We use 1 decimal place precision to match JS Round(..., 1)
                Assert.Equal(expected.Points[i].X, pack.Points[i].X, 1);
                Assert.Equal(expected.Points[i].Y, pack.Points[i].Y, 1);
            }

            // 5. Assert Cell Data Parity (H, G, and Area)
            for (int i = 0; i < pack.Cells.Length; i++)
            {
                var cell = pack.Cells[i];
                Assert.Equal(expected.GridMapping[i], cell.GridId);
                Assert.Equal(expected.Heights[i], cell.H);

                // Area can have tiny float jitter, so we check for near-equality
                Assert.InRange(cell.Area, expected.Areas[i] - 1, expected.Areas[i] + 1);
            }

            // 6. Assert Quadtree Search Parity (The Probes)
            foreach (var probe in expected.Probes)
            {
                // Passing double.PositiveInfinity to match JS 'Infinity' default
                int actualIndex = pack.FindCellInRange(probe.X, probe.Y, double.PositiveInfinity);

                Assert.True(actualIndex != -1, $"Quadtree failed to find a cell for probe at {probe.X},{probe.Y}");
                Assert.Equal(probe.ExpectedIndex, actualIndex);
            }
        }
    }
}