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
        [Theory]
        [InlineData("data/regression_heightmap_hill.json", "Hill 1 90-100 44-56 40-60")]
        [InlineData("data/regression_heightmap_add.json", "Add 30 0-100")]
        [InlineData("data/regression_heightmap_pit.json", "Add 50 0-100; Pit 1 30 50 50")]
        public void HeightmapGenerator_MatchesJsOutput(string filename, string testRecipe)
        {
            // 1. Load the specific heightmap dump
            var json = File.ReadAllText(filename);
            var expected = JsonConvert.DeserializeObject<HeightmapRegressionData>(json);

            // 2. Setup MapData with the exact topology from the dump
            // We need the same points/neighbors to ensure the BFS spreads correctly
            var options = GenerationOptions.TestOptions;
            var rng = new Alea(options.Seed);
            var generator = new MapGenerator();
            generator.Generate(options, rng); // This runs Voronoi to build the graph
            var data = generator.Data;

            // 3. Run ONLY the tool from the JS recipe
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

        [Fact]
        public void Dump_Heightmap_After_Pit_Recipe()
        {
            // 1. Setup Map
            var options = GenerationOptions.TestOptions;
            var rng = new Alea(options.Seed);
            var generator = new MapGenerator();
            generator.Generate(options, rng); // This runs Voronoi to build the graph
            var data = generator.Data;

            // 2. Run the recipe that matches your JS test
            // Example: Initial height of 50, then one Pit
            HeightmapGenerator.Generate(data, "Add 50 0-100; Pit 1 30 50 50", rng);

            // 3. Create the Export Object
            var dump = new
            {
                Seed = "42",
                Width = data.Width,
                Height = data.Height,
                // Extracting the H (byte) values from the Cell array
                Heights = data.Cells.Select(c => (int)c.H).ToArray()
            };

            // 4. Serialize and Save
            string json = JsonConvert.SerializeObject(dump, Formatting.Indented);
            File.WriteAllText("regression_heightmap_pit_csharp.json", json);
        }
    }
}
