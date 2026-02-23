using MapGen.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            var options = GenerationOptions.TestOptions;
            var generator = new MapGenerator();
            generator.Generate(options);

            // 1. Meta-Asserts: Verify the JS setup matches our C# options
            Assert.Equal(options.Seed, expected.Seed);
            Assert.Equal(options.Width, expected.Width);
            Assert.Equal(options.Height, expected.Height);

            // 2. State-Asserts: Verify spacing and grid dimensions match
            Assert.Equal(expected.Spacing, generator.Data.Spacing, 1);
            Assert.Equal(expected.CellsCountX, generator.Data.CellsCountX);
            Assert.Equal(expected.CellsCountY, generator.Data.CellsCountY);

            // 3. Count-Assert: The generated array length must match the JS actual count
            Assert.Equal(expected.ExpectedPointsCount, generator.Data.PointsCount);
            Assert.Equal(expected.ActualPointsCount, generator.Data.Points.Length);

            // 4. Parity-Assert: Coordinate check
            for (int i = 0; i < expected.ActualPointsCount; i++)
            {
                // Points[i][0] is X, Points[i][1] is Y
                Assert.Equal(expected.Points[i][0], generator.Data.Points[i].X, 1);
                Assert.Equal(expected.Points[i][1], generator.Data.Points[i].Y, 1);
            }
        }

        [Fact]
        public void GridGenerator_BoundaryPoints_MatchJsOutput()
        {
            var json = File.ReadAllText("data/regression_boundary.json");
            var expected = JsonConvert.DeserializeObject<BoundaryRegressionData>(json);

            var options = new GenerationOptions
            {
                Seed = "42",
                Width = 1920,
                Height = 1080,
                PointsCount = 2000
            };

            var generator = new MapGenerator();
            // Generate calculates Spacing -> Cells -> BoundaryPoints
            generator.Generate(options);

            Assert.Equal(expected.BoundaryPoints.Length, generator.Data.BoundaryPoints.Length);

            for (int i = 0; i < expected.BoundaryPoints.Length; i++)
            {
                // Direct comparison with MapPoint struct
                Assert.Equal(expected.BoundaryPoints[i][0], generator.Data.BoundaryPoints[i].X, 1);
                Assert.Equal(expected.BoundaryPoints[i][1], generator.Data.BoundaryPoints[i].Y, 1);
            }
        }

        [Fact]
        public void GridGenerator_DumpPoints_ToMatchJs()
        {
            // 1. Setup exact same options as your JS environment
            var options = new GenerationOptions
            {
                Seed = "42",         // Matching the seed you are currently testing
                Width = 1920,
                Height = 1080,
                PointsCount = 2000,  // The "Expected" parameter
            };

            var generator = new MapGenerator();
            generator.Generate(options);

            // 2. Create nested array structure: [[x0, y0], [x1, y1]...]
            var nestedPoints = new List<double[]>();
            for (int i = 0; i < generator.Data.PointsCount; i++)
            {
                nestedPoints.Add(new double[] { generator.Data.Points[i].X, generator.Data.Points[i].Y });
            }

            // 3. Create Anonymous Object matching your JS JSON keys exactly
            // Note: Use the exact casing from your JS dump (Seed, Width, Height, etc.)
            var dumpData = new
            {
                Seed = options.Seed,
                Width = options.Width,
                Height = options.Height,
                ExpectedPointsCount = options.PointsCount,
                ActualPointsCount = generator.Data.PointsCount,
                Spacing = generator.Data.Spacing,
                CellsCountX = generator.Data.CellsCountX,
                CellsCountY = generator.Data.CellsCountY,
                Points = nestedPoints
            };

            // 4. Serialize with Formatting.Indented to match JS stringify(data, null, 2)
            string json = JsonConvert.SerializeObject(dumpData, Formatting.Indented);

            // 5. Write to file
            File.WriteAllText("regression_points_csharp.json", json);
        }
    }
}
