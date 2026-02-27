using MapGen.Core;
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
            var mapData = TestMapData.TestData;
            GridGenerator.Generate(mapData);
            VoronoiGenerator.CalculateVoronoi(mapData);
            HeightmapGenerator.Generate(mapData);

            // 4. Execute markup grid
            FeatureModule.MarkupGrid(mapData);

            // 5. Assert: Cell-level Data
            // We project the data from cells back into arrays to match the expected JSON structure
            var actualFeatureIds = mapData.Cells.Select(c => c.FeatureId).ToArray();
            var actualDistances = mapData.Cells.Select(c => c.Distance).ToArray();

            Assert.Equal(expected.cells_f, actualFeatureIds);
            Assert.Equal(expected.cells_t, actualDistances);

            // 6. Assert: Feature-level Metadata
            var actualFeatures = mapData.Features.Where(f => f != null).ToList();

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
