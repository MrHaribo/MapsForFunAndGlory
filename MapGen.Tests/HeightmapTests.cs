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
        [InlineData("data/regression_heightmap_mult.json", "Add 20 all;Hill 1 50 50 50;Multiply 1.5 land;Multiply 0.5 0-20")]
        [InlineData("data/regression_heightmap_pit.json", "Add 50 0-100;Pit 1 30 50 50")]
        [InlineData("data/regression_heightmap_pit_shallow.json", "Add 50 0-100;Pit 1 5 50 50")]
        [InlineData("data/regression_heightmap_smooth.json", "Add 20 all;Hill 1 60 50 50;Smooth 2 0;Smooth 1.5 1")]
        [InlineData("data/regression_heightmap_invert.json", "Add 20 all;Hill 1 60 20 20;Invert 1 x")]
        [InlineData("data/regression_heightmap_range.json", "Add 15 all; Range 1 60 10-20 10-20; Smooth 2")]
        [InlineData("data/regression_heightmap_trough.json", "Add 70 all; Trough 1 40 40-60 5-10; Smooth 1.5")]
        [InlineData("data/regression_heightmap_strait.json", "Add 50 all; Strait 15 vertical; Strait 15 horizontal")]
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

        [Theory]
        [InlineData("data/regression_heightmap_range_cs.json", "Add 15 all; Range 1 60 10-20 10-20; Smooth 2")]
        //[InlineData("data/regression_heightmap_trough_cs.json", "Add 70 all; Trough 1 40 40-60 5-10; Smooth 1.5")]
        //[InlineData("data/regression_heightmap_strait_cs.json", "Add 50 all; Strait 15 vertical; Strait 15 horizontal")]
        public void Dump_Heightmap_After_Pit_Recipe(string filename, string testRecipe)
        {
            // 1. Setup Map
            var options = GenerationOptions.TestOptions;
            var rng = new Alea(options.Seed);
            var generator = new MapGenerator();
            generator.Generate(options, rng); // This runs Voronoi to build the graph
            var data = generator.Data;

            // 2. Run the recipe that matches your JS test
            // Example: Initial height of 50, then one Pit
            HeightmapGenerator.Generate(data, testRecipe, rng);

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
            File.WriteAllText(filename, json);
        }
    }
}
