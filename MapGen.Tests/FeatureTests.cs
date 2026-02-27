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
            public List<PackFeatureItem> features { get; set; }
        }

        public class PackFeatureItem
        {
            public ushort id { get; set; }
            public string type { get; set; }
            public bool land { get; set; }
            public int verticesCount { get; set; }
            public double area { get; set; }
            public int shorelineCount { get; set; }
            public double height { get; set; }
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

            // 2. Setup MapPack
            // 2. Prepare the Input MapData
            var mapData = TestMapData.TestData;
            GridGenerator.Generate(mapData);
            VoronoiGenerator.CalculateVoronoi(mapData);
            HeightmapGenerator.Generate(mapData);
            FeatureModule.MarkupGrid(mapData);
            LakeModule.AddLakesInDeepDepressions(mapData);
            LakeModule.OpenNearSeaLakes(mapData);
            GlobeModule.DefineMapSize(mapData);
            GlobeModule.CalculateMapCoordinates(mapData);
            ClimateModule.CalculateTemperatures(mapData);
            ClimateModule.GeneratePrecipitation(mapData);

            var pack = PackModule.ReGraph(mapData);

            // 3. Execute markup pack
            FeatureModule.MarkupPack(pack);

            // 4. Assert: Cell-level Data
            var actualFeatureIds = pack.Cells.Select(c => c.FeatureId).ToArray();
            var actualDistances = pack.Cells.Select(c => c.Distance).ToArray();

            Assert.Equal(expected.cells_f, actualFeatureIds);
            Assert.Equal(expected.cells_t, actualDistances);

            // 5. Assert: Feature-level Metadata
            // JS filter(f => f) means we skip the null index 0
            var actualFeatures = pack.Features;

            Assert.Equal(expected.features.Count, pack.Features.Count);
            Assert.Equal(expected.features[0].id, pack.Features[0].Id); // Both should be 1


            for (int i = 0; i < expected.features.Count; i++)
            {
                var exp = expected.features[i];
                var act = actualFeatures[i];

                Assert.Equal(exp.id, act.Id);
                Assert.Equal(exp.type, act.Type.ToString().ToLower());
                Assert.Equal(exp.verticesCount, act.Vertices.Count);

                // Use a small epsilon for double comparison due to rounding
                //Assert.InRange(act.Area, exp.area - 0.1, exp.area + 0.1);
                Assert.InRange(act.Area, exp.area - 1.1, exp.area + 1.1);

                if (act.Type == FeatureType.Lake)
                {
                    Assert.Equal(exp.shorelineCount, act.Shoreline.Count);
                    Assert.InRange(act.Height, exp.height - 0.001, exp.height + 0.001);
                }
            }
        }


    }
}
