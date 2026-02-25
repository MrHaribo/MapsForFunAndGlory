using MapGen.Core;
using MapGen.Core.Helpers;
using MapGen.Core.Modules;
using Newtonsoft.Json;

namespace MapGen.Tests
{
    public class FeatureTests
    {
        public class GridFeatureRegressionData
        {
            // Matches "cells_f" (Feature IDs)
            public ushort[] cells_f { get; set; }

            // Matches "cells_t" (Distance Field)
            public sbyte[] cells_t { get; set; }

            // Matches "features" list
            public List<FeatureRegressionItem> features { get; set; }
        }

        public class FeatureRegressionItem
        {
            public int id { get; set; }
            public string type { get; set; } // "island", "ocean", or "lake"
            public bool land { get; set; }
        }

        [Fact]
        public void TestGridFeatureDetection()
        {
            // 1. Load the specific feature dump
            var json = File.ReadAllText("data/regression_features_grid.json");
            var expected = JsonConvert.DeserializeObject<GridFeatureRegressionData>(json);

            // 2. Setup MapData with the exact topology from our test setup
            var options = GenerationOptions.TestOptions; // Ensure this is Seed 42
            var rng = new AleaRandom(options.Seed);
            var generator = new MapGenerator();
            generator.Generate(options, rng); // Builds the Voronoi Graph
            var data = generator.Data;

            // 3. Run the same template as in JS (resetting seed to match JS heightmap state)
            rng = new AleaRandom(options.Seed);
            HeightmapGenerator.Generate(data, HeightmapTemplate.Continents, rng);

            // 4. Execute markup grid
            FeatureModule.MarkupGrid(data);

            // 5. Assert: Cell-level Data
            Assert.Equal(expected.cells_f, data.FeatureIds);
            Assert.Equal(expected.cells_t, data.DistanceField);

            // 6. Assert: Feature-level Metadata
            // We skip index 0 in data.Features because JS export filtered out the null at index 0
            var actualFeatures = data.Features.Where(f => f != null).ToList();

            Assert.Equal(expected.features.Count, actualFeatures.Count);

            for (int i = 0; i < expected.features.Count; i++)
            {
                var exp = expected.features[i];
                var act = actualFeatures[i];

                Assert.Equal(exp.id, act.Id);
                Assert.Equal(exp.land, act.IsLand);

                // Compare types (mapping Enum to lowercase string)
                Assert.Equal(exp.type, act.Type.ToString().ToLower());
            }
        }
    }
}
