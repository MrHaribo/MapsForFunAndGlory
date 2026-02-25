using System.Text.Json;
using System.Text.Json.Serialization;
using MapGen.Core;
using Xunit;

namespace MapGen.Tests
{
    public class VoronoiTests
    {
        // Model matching the JS dump structure
        public class JsGridDump
        {
            public string Seed { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public JsGrid Grid { get; set; }
        }

        public class JsGrid
        {
            public double spacing { get; set; }
            public int cellsDesired { get; set; }
            public double[][] boundary { get; set; }
            public double[][] points { get; set; }
            public int cellsX { get; set; }
            public int cellsY { get; set; }
            public JsCells cells { get; set; }
            public JsVertices vertices { get; set; }
        }

        public class JsCells
        {
            public int[][] v { get; set; } // Vertices of cell
            public int[][] c { get; set; } // Adjacent cells
            public int[] b { get; set; }   // Boundary flag
        }

        public class JsVertices
        {
            public double[][] p { get; set; } // Vertex coordinates
            public int[][] v { get; set; }    // Vertex-to-vertex
            public int[][] c { get; set; }    // Adjacent cells
        }

        [Fact]
        public void VoronoiGenerator_Graph_MatchesJsOutput()
        {
            // 1. Load the JS dump using System.Text.Json
            var json = File.ReadAllText("data/regression_voronoi.json");
            var expectedRoot = JsonSerializer.Deserialize<JsGridDump>(json);
            var expected = expectedRoot.Grid;

            // 2. Run the full C# pipeline
            var options = GenerationOptions.TestOptions;
            var generator = new MapGenerator();
            generator.Generate(options);
            var actual = generator.Data;

            // 3. Verify Global Properties
            Assert.Equal(expected.spacing, actual.Spacing, 2);
            Assert.Equal(expected.cells.c.Length, actual.Cells.Length);

            // 4. Verify Cell Topology and Boundary Flags
            for (int i = 0; i < actual.Cells.Length; i++)
            {
                // Verify adjacent cells (c)
                Assert.Equal(expected.cells.c[i], actual.Cells[i].C.ToArray());

                // Verify cell vertices (v)
                Assert.Equal(expected.cells.v[i], actual.Cells[i].V.ToArray());

                // Verify boundary flag (b)
                Assert.Equal(expected.cells.b[i], (int)actual.Cells[i].B);
            }

            // 5. Verify Vertex Geometry and Topology
            Assert.Equal(expected.vertices.p.Length, actual.Vertices.Length);
            for (int i = 0; i < actual.Vertices.Length; i++)
            {
                var actualP = actual.Vertices[i].P;
                var expectedP = expected.vertices.p[i];

                // Geometry: Use precision 0 for snapped coordinates, or 2 for exact
                Assert.Equal(expectedP[0], actualP.X, 0);
                Assert.Equal(expectedP[1], actualP.Y, 0);

                // Topology: Vertex-to-cell and Vertex-to-vertex
                Assert.Equal(expected.vertices.c[i], actual.Vertices[i].C.ToArray());
                Assert.Equal(expected.vertices.v[i], actual.Vertices[i].V.ToArray());
            }
        }

        //[Fact]
        //public void ExportStrictGrid()
        //{
        //    var options = GenerationOptions.TestOptions;
        //    var generator = new MapGenerator();
        //    generator.Generate(options);
        //    var data = generator.Data;

        //    // Build the object following the exact JS structure from snippets
        //    var rootExport = new JsGridDump
        //    {
        //        Seed = options.Seed,
        //        Width = options.Width,
        //        Height = options.Height,
        //        Grid = new JsGrid
        //        {
        //            spacing = data.Spacing,
        //            cellsDesired = options.PointsCount,
        //            boundary = data.BoundaryPoints.Select(p => new[] { p.X, p.Y }).ToArray(),
        //            points = data.Points.Select(p => new[] { p.X, p.Y }).ToArray(),
        //            cellsX = data.CellsCountX,
        //            cellsY = data.CellsCountY,
        //            cells = new JsCells
        //            {
        //                v = data.Cells.Select(c => c.V.ToArray()).ToArray(),
        //                c = data.Cells.Select(c => c.C.ToArray()).ToArray(),
        //                b = data.Cells.Select(c => (int)c.B).ToArray()
        //            },
        //            vertices = new JsVertices
        //            {
        //                p = data.Vertices.Select(v => new[] { v.P.X, v.P.Y }).ToArray(),
        //                v = data.Vertices.Select(v => v.V.ToArray()).ToArray(),
        //                c = data.Vertices.Select(v => v.C.ToArray()).ToArray()
        //            }
        //        }
        //    };

        //    var serializerOptions = new JsonSerializerOptions
        //    {
        //        WriteIndented = true,
        //        PropertyNamingPolicy = null // Keeps PascalCase for root, but we use camelCase in models
        //    };

        //    string json = JsonSerializer.Serialize(rootExport, serializerOptions);
        //    File.WriteAllText("grid_csharp_dump.json", json);
        //}
    }
}