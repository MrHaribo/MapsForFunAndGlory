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

        public class PackFeatureRegressionData
        {
            public ushort[] cells_f { get; set; }
            public sbyte[] cells_t { get; set; }
            public List<PackFeatureRegressionItem> features { get; set; }
        }

        public class PackFeatureRegressionItem
        {
            public int id { get; set; }
            public string type { get; set; }
            public bool land { get; set; }
            public bool border { get; set; }
            public int cells { get; set; }
            public int firstCell { get; set; }
            public List<int> vertices { get; set; } // Sequence matters!
            public double area { get; set; }
            public double height { get; set; }
            public List<int> shoreline { get; set; } // Sequence matters!
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

        [Fact]
        public void TestPackFeatureDetection()
        {
            // 1. Load the specific feature dump from JS
            var json = File.ReadAllText("data/regression_features_pack.json");
            var expected = JsonConvert.DeserializeObject<PackFeatureRegressionData>(json);

            // 2. Setup MapPack (Logic leading up to MarkupPack)
            var mapData = TestMapData.TestData;
            GridGenerator.Generate(mapData);
            VoronoiGenerator.CalculateVoronoi(mapData);
            HeightmapGenerator.Generate(mapData);
            FeatureModule.MarkupGrid(mapData);
            LakeModule.AddLakesInDeepDepressions(mapData);
            LakeModule.OpenNearSeaLakes(mapData);

            var pack = PackModule.ReGraph(mapData);

            // 3. Execute markup pack (The target of this test)
            FeatureModule.MarkupPack(pack);

            // 4. Assert: Cell-level Data (Feature IDs and Distance to Shore)
            Assert.Equal(expected.cells_f, pack.Cells.Select(c => c.FeatureId).ToArray());
            Assert.Equal(expected.cells_t, pack.Cells.Select(c => c.Distance).ToArray());

            // 5. Assert: Feature-level Metadata
            var actualFeatures = pack.Features;
            Assert.Equal(expected.features.Count, actualFeatures.Count);

            for (int i = 0; i < expected.features.Count; i++)
            {
                var exp = expected.features[i];
                var act = actualFeatures[i];

                // Metadata
                Assert.Equal(exp.id, act.Id);
                Assert.Equal(exp.type, act.Type.ToString().ToLower());
                Assert.Equal(exp.land, act.IsLand);
                Assert.Equal(exp.border, act.IsBorder);
                Assert.Equal(exp.cells, act.CellsCount);
                Assert.Equal(exp.firstCell, act.FirstCell);

                // Geometry (The sequence check is vital for the winding order fix)
                Assert.Equal(exp.vertices, act.Vertices);
                Assert.Equal(exp.area, act.Area); // Bitwise parity expected here now!
                Assert.Equal(exp.height, act.Height);

                // Shoreline sequence affects how rivers/neighbors interact later
                Assert.Equal(exp.shoreline, act.Shoreline);
            }
        }


    }
}
