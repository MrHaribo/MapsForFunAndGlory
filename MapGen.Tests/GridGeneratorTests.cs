using MapGen.Core;
using MapGen.Core.Modules;
using Newtonsoft.Json;

namespace MapGen.Tests
{
    public class PointsRegressionData
    {
        public string Seed { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int ExpectedPointsCount { get; set; }
        public int ActualPointsCount { get; set; }
        public double Spacing { get; set; }
        public int CellsCountX { get; set; }
        public int CellsCountY { get; set; }
        public double[][] Points { get; set; }
    }

    public class BoundaryRegressionData
    {
        public string Seed { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public double[][] BoundaryPoints { get; set; }
    }

    public class GridGeneratorTests
    {
        [Fact]
        public void GridGenerator_Points_MatchJsOutput()
        {
            var json = File.ReadAllText("data/regression_points.json");
            var expected = JsonConvert.DeserializeObject<PointsRegressionData>(json);

            // Act
            var mapData = TestMapData.TestData;
            GridGenerator.Generate(mapData);

            // 1. Meta-Asserts: Verify the JS setup matches our C# options
            Assert.Equal(mapData.Seed, expected.Seed);
            Assert.Equal(mapData.Width, expected.Width);
            Assert.Equal(mapData.Height, expected.Height);

            // 2. State-Asserts: Verify spacing and grid dimensions match
            Assert.Equal(expected.Spacing, mapData.Spacing, 1);
            Assert.Equal(expected.CellsCountX, mapData.CellsCountX);
            Assert.Equal(expected.CellsCountY, mapData.CellsCountY);

            // 3. Count-Assert: The generated array length must match the JS actual count
            Assert.Equal(expected.ExpectedPointsCount, mapData.PointsCount);
            Assert.Equal(expected.ActualPointsCount, mapData.Points.Length);

            // 4. Parity-Assert: Coordinate check
            for (int i = 0; i < expected.ActualPointsCount; i++)
            {
                // Points[i][0] is X, Points[i][1] is Y
                Assert.Equal(expected.Points[i][0], mapData.Points[i].X, 1);
                Assert.Equal(expected.Points[i][1], mapData.Points[i].Y, 1);
            }
        }

        [Fact]
        public void GridGenerator_BoundaryPoints_MatchJsOutput()
        {
            var json = File.ReadAllText("data/regression_boundary.json");
            var expected = JsonConvert.DeserializeObject<BoundaryRegressionData>(json);

            var mapData = TestMapData.TestData;
            GridGenerator.Generate(mapData);

            Assert.Equal(expected.BoundaryPoints.Length, mapData.BoundaryPoints.Length);

            for (int i = 0; i < expected.BoundaryPoints.Length; i++)
            {
                // Direct comparison with MapPoint struct
                Assert.Equal(expected.BoundaryPoints[i][0], mapData.BoundaryPoints[i].X, 1);
                Assert.Equal(expected.BoundaryPoints[i][1], mapData.BoundaryPoints[i].Y, 1);
            }
        }

        //[Fact]
        //public void GridGenerator_DumpPoints_ToMatchJs()
        //{
        //    // 1. Setup exact same options as your JS environment
        //    var mapData = MapData.TestData;
        //    GridGenerator.GenerateGrid(mapData);

        //    // 2. Create nested array structure: [[x0, y0], [x1, y1]...]
        //    var nestedPoints = new List<double[]>();
        //    for (int i = 0; i < mapData.PointsCount; i++)
        //    {
        //        nestedPoints.Add(new double[] { mapData.Points[i].X, mapData.Points[i].Y });
        //    }

        //    // 3. Create Anonymous Object matching your JS JSON keys exactly
        //    // Note: Use the exact casing from your JS dump (Seed, Width, Height, etc.)
        //    var dumpData = new
        //    {
        //        Seed = mapData.Seed,
        //        Width = mapData.Width,
        //        Height = mapData.Height,
        //        ExpectedPointsCount = mapData.PointsCount,
        //        ActualPointsCount = mapData.CellsCount,
        //        Spacing = mapData.Spacing,
        //        CellsCountX = mapData.CellsCountX,
        //        CellsCountY = mapData.CellsCountY,
        //        Points = nestedPoints
        //    };

        //    // 4. Serialize with Formatting.Indented to match JS stringify(data, null, 2)
        //    string json = JsonConvert.SerializeObject(dumpData, Formatting.Indented);

        //    // 5. Write to file
        //    File.WriteAllText("regression_points_csharp.json", json);
        //}
    }
}
