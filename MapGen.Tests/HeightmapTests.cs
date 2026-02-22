using MapGen.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapGen.Tests
{
    public class HeightmapRegressionData
    {
        public string Seed { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int PointsCount { get; set; }
        public byte[] Heights { get; set; } // JS grid.cells.h
    }
    public class HeightmapTests
    {
        [Fact]
        public void HeightmapGenerator_Hill_MatchesJsOutput()
        {
            // 1. Load the specific Hill dump
            var json = File.ReadAllText("regression_heightmap_hill.json");
            var expected = JsonConvert.DeserializeObject<HeightmapRegressionData>(json);

            // 2. Setup MapData with the exact topology from the dump
            // We need the same points/neighbors to ensure the BFS spreads correctly
            var options = new GenerationOptions
            {
                Seed = "42",
                Width = 1920,
                Height = 1080,
                PointsCount = 2000
            };

            var rng = new Alea(options.Seed);
            var generator = new MapGenerator();
            generator.Generate(options, rng); // This runs Voronoi to build the graph
            var data = generator.Data;

            // 3. Run ONLY the Hill tool with the JS recipe
            // Ensure the RNG is reset to the same state as the JS run
            string testRecipe = "Hill 1 90-100 44-56 40-60";

            HeightmapGenerator.Generate(data, testRecipe, rng);

            // 4. Verify Parity
            for (int i = 0; i < expected.Heights.Length; i++)
            {
                // We use 0 tolerance because heights are bytes (0-100)
                // If the RNG or BFS decay math is off by even 1, this fails.
                Assert.Equal(expected.Heights[i], data.Cells[i].H);
            }
        }
    }
}
