using MapGen.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace MapGen.Tests
{
    public class VoronoiTests
    {
        public class VoronoiRegressionData
        {
            public int CellsCount { get; set; }
            public List<int>[] Neighbors { get; set; } // Matches grid.cells.c
            public int VertexCount { get; set; }
            public float[] Vertices { get; set; }      // Flattened [x, y, x, y...]
        }

        [Fact]
        public void VoronoiGenerator_Graph_MatchesJsOutput()
        {
            // 1. Load the JS dump
            var json = File.ReadAllText("regression_voronoi_azgaar.json");
            var expected = JsonConvert.DeserializeObject<VoronoiRegressionData>(json);

            // 2. Run the full pipeline
            var options = new GenerationOptions
            {
                Seed = "azgaar",
                Width = 1920,
                Height = 1080,
                PointsCount = 9975,
                Jitter = 0.8
            };

            var generator = new MapGenerator();
            generator.Generate(options);
            var data = generator.Data;

            // 3. Verify Cell Neighbor Parity (Topology)
            Assert.Equal(expected.CellsCount, data.Cells.C.Length);
            for (int i = 0; i < expected.CellsCount; i++)
            {
                // Assert.Equal on Lists compares sequence content
                Assert.Equal(expected.Neighbors[i], data.Cells.C[i]);
            }

            // 4. Verify Vertex Geometry (Circumcenters)
            Assert.Equal(expected.VertexCount, data.Vertices.P.Length);
            for (int i = 0; i < expected.VertexCount; i++)
            {
                // Using precision 0 because of Azgaar's Math.floor()
                Assert.Equal(expected.Vertices[i * 2], (float)data.Vertices.P[i].X, 0);
                Assert.Equal(expected.Vertices[i * 2 + 1], (float)data.Vertices.P[i].Y, 0);
            }
        }
    }
}
