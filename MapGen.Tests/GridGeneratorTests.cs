using MapGen.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapGen.Tests
{
    public class RegressionData
    {
        public string Seed { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int PointsCount { get; set; }
        public float Spacing { get; set; }

        // This matches the flat [...] array from JS
        public List<float> Points { get; set; }
    }

    public class BoundaryRegressionData
    {
        public double Spacing { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int BoundaryCount { get; set; }
        public float[] Points { get; set; }
    }

    public class GridGeneratorTests
    {
        [Fact]
        public void GridGenerator_Points_MatchJsOutput()
        {
            var json = File.ReadAllText("regression_points_azgaar.json");
            var expected = JsonConvert.DeserializeObject<RegressionData>(json);

            var options = new GenerationOptions
            {
                Seed = expected.Seed,
                Width = expected.Width,
                Height = expected.Height,
                // The Magic Fix:
                FixedSpacing = expected.Spacing,
            };

            var generator = new MapGenerator();
            generator.Generate(options);

            // Verify the point count matches first
            Assert.Equal(expected.PointsCount, generator.Data.PointsCount);

            for (int i = 0; i < expected.PointsCount; i++)
            {
                Assert.Equal(expected.Points[i * 2], generator.Data.X[i], 2);
                Assert.Equal(expected.Points[i * 2 + 1], generator.Data.Y[i], 2);
            }
        }

        [Fact]
        public void GridGenerator_BoundaryPoints_MatchJsOutput()
        {
            // 1. Load the JS dump
            var json = File.ReadAllText("regression_boundary_azgaar.json");
            var expected = JsonConvert.DeserializeObject<BoundaryRegressionData>(json);

            // 2. Setup MapData with the exact spacing from JS
            var data = new MapData(0, expected.Width, expected.Height);
            data.Spacing = expected.Spacing;

            // 3. Run the generator
            GridGenerator.PlaceBoundaryPoints(data, expected.Width, expected.Height);

            // 4. Verify
            Assert.Equal(expected.BoundaryCount, data.BoundaryX.Length);

            for (int i = 0; i < expected.BoundaryCount; i++)
            {
                // Check X and Y with a small epsilon for float precision
                Assert.Equal(expected.Points[i * 2], data.BoundaryX[i], 1);
                Assert.Equal(expected.Points[i * 2 + 1], data.BoundaryY[i], 1);
            }
        }

        [Fact]
        public void GridGenerator_DumpPoints_ToMatchJs()
        {
            // 1. Setup exact same options as JS
            var options = new GenerationOptions
            {
                Seed = "azgaar",
                Width = 1920,
                Height = 1080,
                PointsCount = 9975, // Matching your JS dump
                FixedSpacing = 14.4,
                Jitter = 0.8        // Ensure this matches FMG v1.109 default
            };

            var generator = new MapGenerator();
            generator.Generate(options);

            // 2. Flatten points to [x0, y0, x1, y1...] to match JS [...grid.points].flat()
            var flatPoints = new List<float>();
            for (int i = 0; i < generator.Data.PointsCount; i++)
            {
                flatPoints.Add(generator.Data.X[i]);
                flatPoints.Add(generator.Data.Y[i]);
            }

            // 3. Create Anonymous Object to match JS JSON keys
            var dumpData = new
            {
                seed = options.Seed,
                width = options.Width,
                height = options.Height,
                spacing = options.FixedSpacing,
                pointsCount = generator.Data.PointsCount,
                points = flatPoints
            };

            // 4. Serialize with 2-space indentation
            string json = JsonConvert.SerializeObject(dumpData, Formatting.Indented);

            // 5. Write to file
            File.WriteAllText("regression_points_azgaar_csharp.json", json);
        }
    }
}
