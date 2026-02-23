using MapGen.Core;
using Newtonsoft.Json;

namespace MapGen.Tests
{
    public class HeightmapRegressionData
    {
        public string Seed { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public byte[] Heights { get; set; }
    }
    public class HeightmapTests
    {
        [Fact]
        public void HeightmapGenerator_Hill_MatchesJsOutput()
        {
            // 1. Load the specific Hill dump
            var json = File.ReadAllText("data/regression_heightmap_hill.json");
            var expected = JsonConvert.DeserializeObject<HeightmapRegressionData>(json);

            // 2. Setup MapData with the exact topology from the dump
            // We need the same points/neighbors to ensure the BFS spreads correctly
            var options = GenerationOptions.TestOptions;
            var rng = new Alea(options.Seed);
            var generator = new MapGenerator();
            generator.Generate(options, rng); // This runs Voronoi to build the graph
            var data = generator.Data;

            // 3. Run ONLY the Hill tool with the JS recipe
            // Ensure the RNG is reset to the same state as the JS run
            string testRecipe = "Hill 1 90-100 44-56 40-60";

            rng = new Alea(options.Seed);
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
