using MapGen.Core;
using MapGen.Core.Helpers;
using MapGen.Core.Modules;
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
        const string foo = "foo";

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
        [InlineData("data/regression_heightmap_template_highIsland.json", HeightmapTemplates.HighIsland)]
        [InlineData("data/regression_heightmap_template_archipelago.json", HeightmapTemplates.Archipelago)]
        [InlineData("data/regression_heightmap_template_shattered.json", HeightmapTemplates.Shattered)]
        [InlineData("data/regression_heightmap_template_volcano.json", HeightmapTemplates.Volcano)]
        [InlineData("data/regression_heightmap_template_fractious.json", HeightmapTemplates.Fractious)]
        [InlineData("data/regression_heightmap_template_continents.json", HeightmapTemplates.Continents)]
        public void HeightmapGenerator_MatchesJsOutput(string filename, string testRecipe)
        {
            // 1. Load the specific heightmap dump
            var json = File.ReadAllText(filename);
            var expected = JsonConvert.DeserializeObject<HeightmapRegressionData>(json);

            // 2. Setup MapData with the exact topology from the dump
            // We need the same points/neighbors to ensure the BFS spreads correctly
            var options = GenerationOptions.TestOptions;
            var rng = new AleaRandom(options.Seed);
            var generator = new MapGenerator();
            generator.Generate(options, rng); // This runs Voronoi to build the graph
            var data = generator.Data;

            // 3. Run ONLY the tool from the JS recipe
            rng = new AleaRandom(options.Seed);
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
        [InlineData("data/regression_heightmap_addLakesInDeepDepressions_template_continents.json", HeightmapTemplates.Continents)]
        [InlineData("data/regression_heightmap_addLakesInDeepDepressions_template_lakeTest.json", "Hill 1 80-85 60-80 40-60;Hill 1 80-85 20-30 40-60")]
        public void LakeModule_AddLakes_MatchesJsOutput(string filename, string testRecipe)
        {
            // 1. Load the specific heightmap dump from JS
            var json = File.ReadAllText(filename);
            var expected = JsonConvert.DeserializeObject<HeightmapRegressionData>(json);

            // 2. Setup MapData (Voronoi Graph)
            var options = GenerationOptions.TestOptions;
            var rng = new AleaRandom(options.Seed);
            var generator = new MapGenerator();
            generator.Generate(options, rng);
            var data = generator.Data;

            // 3. Run Template Heightmap
            rng = new AleaRandom(options.Seed);
            HeightmapGenerator.Generate(data, testRecipe, rng);

            // 4. Required: Feature detection must run before LakeModule
            FeatureModule.MarkupGrid(data);

            // 5. Execute Lake Filling
            LakeModule.AddLakesInDeepDepressions(data, MapConstants.DEFAULT_LAKE_ELEV_LIMIT);

            // 6. Verify Height Parity
            for (int i = 0; i < expected.Heights.Length; i++)
            {
                Assert.Equal(expected.Heights[i], data.Cells[i].H);
            }
        }

        [Theory]
        [InlineData("data/regression_heightmap_openNearSeaLakes_template_continents.json", HeightmapTemplates.Continents)]
        [InlineData("data/regression_heightmap_openNearSeaLakes_template_lakeTest.json", "Hill 1 80-85 60-80 40-60;Hill 1 80-85 20-30 40-60")]
        public void LakeModule_OpenLakes_MatchesJsOutput(string filename, string testRecipe)
        {
            // 1. Load the specific heightmap dump from JS
            var json = File.ReadAllText(filename);
            var expected = JsonConvert.DeserializeObject<HeightmapRegressionData>(json);

            // 2. Setup MapData
            var options = GenerationOptions.TestOptions;
            var rng = new AleaRandom(options.Seed);
            var generator = new MapGenerator();
            generator.Generate(options, rng);
            var data = generator.Data;

            // 3. Run Template Heightmap
            rng = new AleaRandom(options.Seed);
            HeightmapGenerator.Generate(data, testRecipe, rng);

            // 4. Feature detection
            FeatureModule.MarkupGrid(data);

            // 5. Run sequential processing (Match JS order)
            LakeModule.AddLakesInDeepDepressions(data, MapConstants.DEFAULT_LAKE_ELEV_LIMIT);
            LakeModule.OpenNearSeaLakes(data, HeightmapTemplate.Test);

            // 6. Verify Height Parity
            for (int i = 0; i < expected.Heights.Length; i++)
            {
                Assert.Equal(expected.Heights[i], data.Cells[i].H);
            }
        }

        //[Theory]
        //[InlineData("data/regression_heightmap_range_cs.json", "Add 15 all; Range 1 60 10-20 10-20; Smooth 2")]
        //[InlineData("data/regression_heightmap_trough_cs.json", "Add 70 all; Trough 1 40 40-60 5-10; Smooth 1.5")]
        //[InlineData("data/regression_heightmap_strait_cs.json", "Add 50 all; Strait 15 vertical; Strait 15 horizontal")]
        //[InlineData("data/regression_heightmap_template_continents_cs.json", HeightmapTemplates.Continents)]
        //public void Dump_Heightmap_After_Pit_Recipe(string filename, string testRecipe)
        //{
        //    // 1. Setup Map
        //    var options = GenerationOptions.TestOptions;
        //    var rng = new AleaRandom(options.Seed);
        //    var generator = new MapGenerator();
        //    generator.Generate(options, rng); // This runs Voronoi to build the graph
        //    var data = generator.Data;

        //    // 2. Run the recipe that matches your JS test
        //    // Example: Initial height of 50, then one Pit
        //    rng = new AleaRandom(options.Seed);
        //    HeightmapGenerator.Generate(data, testRecipe, rng);

        //    // 3. Create the Export Object
        //    var dump = new
        //    {
        //        Seed = "42",
        //        Width = data.Width,
        //        Height = data.Height,
        //        // Extracting the H (byte) values from the Cell array
        //        Heights = data.Cells.Select(c => (int)c.H).ToArray()
        //    };

        //    // 4. Serialize and Save
        //    string json = JsonConvert.SerializeObject(dump, Formatting.Indented);
        //    File.WriteAllText(filename, json);
        //}
    }
}
