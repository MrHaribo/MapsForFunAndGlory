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
            public string Seed { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public int CellsCount { get; set; }
            public int[][] Neighbors { get; set; } // JS: grid.cells.c
            public double[][] Vertices { get; set; } // JS: grid.vertices.p (nested [[x,y]])
            public int VertexCount { get; set; }
        }

        [Fact]
        public void VoronoiGenerator_Graph_MatchesJsOutput()
        {
            // 1. Load the JS dump
            var json = File.ReadAllText("regression_voronoi.json");
            var expected = JsonConvert.DeserializeObject<VoronoiRegressionData>(json);

            // 2. Run the full pipeline
            var options = new GenerationOptions
            {
                Seed = "42",
                Width = 1920,
                Height = 1080,
                PointsCount = 2000
            };

            var generator = new MapGenerator();
            generator.Generate(options);
            var data = generator.Data;

            // 3. Verify Cell Neighbor Parity (Topology)
            // Check total length of the Cells array
            Assert.Equal(expected.CellsCount, data.Cells.Length);

            for (int i = 0; i < expected.CellsCount; i++)
            {
                // Accessing neighbor list via the MapCell object
                // expected.Neighbors[i] is int[], data.Cells[i].C is List<int>
                Assert.Equal(expected.Neighbors[i], data.Cells[i].C);
            }

            // 4. Verify Vertex Geometry (Circumcenters)
            // Check total length of the Vertices array
            Assert.Equal(expected.VertexCount, data.Vertices.Length);


            for (int i = 0; i < expected.VertexCount; i++)
            {
                // Accessing coordinate via the MapVertex object
                var actualVertex = data.Vertices[i].P;

                // expected.Vertices[i][0] is X, expected.Vertices[i][1] is Y
                // Using precision 0 to account for Azgaar's Math.floor snapping
                Assert.Equal(expected.Vertices[i][0], actualVertex.X, 0);
                Assert.Equal(expected.Vertices[i][1], actualVertex.Y, 0);
            }
        }
    }
}
