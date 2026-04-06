using MapGen.Core;
using MapGen.Core.Modules;
using Newtonsoft.Json;
using static MapGen.Tests.BiomeTests;

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
            public ushort[] cells_haven { get; set; }
            public ushort[] cells_harbor { get; set; }
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

        public class FeatureGroupRegressionData
        {
            public string Seed { get; set; }
            public List<FeatureGroupEntry> Features { get; set; }
        }

        public class FeatureGroupEntry
        {
            public int Id { get; set; }
            public string Type { get; set; }
            public string Group { get; set; } // The JS string value
            public int Cells { get; set; }
            public double Height { get; set; }
            public double Temp { get; set; }
        }

        public class CellRankRegressionData
        {
            public string Seed { get; set; }
            public double MeanFlux { get; set; }
            public double MaxFlux { get; set; }
            public double MeanArea { get; set; }
            public short[] Suitability { get; set; }
            public float[] Population { get; set; }
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

            // Haven (Forced River flow targets)
            // If these diverge, rivers will flow to different neighbors even if heights are the same
            Assert.Equal(expected.cells_haven, pack.Cells.Select(c => c.Haven).ToArray());

            // Harbor (Forced port/coastal targets)
            Assert.Equal(expected.cells_harbor, pack.Cells.Select(c => c.Harbor).ToArray());

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
                if (act.Type == FeatureType.Lake)
                    Assert.Equal(exp.shoreline, act.ShorelineCells);
            }
        }

        [Fact]
        public void TestPackFeatureGroups()
        {
            // 1. Load Expected Data
            var json = File.ReadAllText("data/regression_feature_groups.json");
            var expected = JsonConvert.DeserializeObject<FeatureGroupRegressionData>(json);

            // 2. Setup (Full Pipeline)
            var mapData = TestMapData.TestData;
            GridGenerator.Generate(mapData);
            VoronoiGenerator.CalculateVoronoi(mapData);
            HeightmapGenerator.Generate(mapData);
            FeatureModule.MarkupGrid(mapData);
            GlobeModule.DefineMapSize(mapData);
            GlobeModule.CalculateMapCoordinates(mapData);
            ClimateModule.CalculateTemperatures(mapData);
            ClimateModule.GeneratePrecipitation(mapData);

            var pack = PackModule.ReGraph(mapData);
            FeatureModule.MarkupPack(pack);
            RiverModule.Generate(pack, mapData, allowErosion: true);
            BiomModule.Define(pack, mapData);

            // 3. Run the Logic under test
            FeatureModule.DefineGroups(pack);

            // 4. Verification
            foreach (var expectedFeature in expected.Features)
            {
                // JS skips to group oceans, we do it here, but have to skip it in the test to keep parity
                if (expectedFeature.Type == "ocean") continue;

                // Get our feature (id - 1 because Pack.Features is 0-indexed while FMG IDs are 1-based)
                var actualFeature = pack.GetFeature(expectedFeature.Id);

                // Map JS string to our Enum
                FeatureGroup expectedEnum = MapJsGroupToEnum(expectedFeature.Group);

                Assert.True(actualFeature != null, $"Feature {expectedFeature.Id} missing in Pack");
                Assert.Equal(expectedEnum, actualFeature.Group);
            }
        }

        private FeatureGroup MapJsGroupToEnum(string jsGroup)
        {
            return jsGroup switch
            {
                "ocean" => FeatureGroup.Ocean,
                "sea" => FeatureGroup.Sea,
                "gulf" => FeatureGroup.Gulf,
                "continent" => FeatureGroup.Continent,
                "island" => FeatureGroup.Island,
                "isle" => FeatureGroup.Isle,
                "lake_island" => FeatureGroup.LakeIsland,
                "freshwater" => FeatureGroup.Freshwater,
                "salt" => FeatureGroup.Salt,
                "frozen" => FeatureGroup.Frozen,
                "dry" => FeatureGroup.Dry,
                "sinkhole" => FeatureGroup.Sinkhole,
                "lava" => FeatureGroup.Lava,
                _ => throw new ArgumentException($"Unknown JS feature group: {jsGroup}")
            };
        }

        [Fact]
        public void TestPackCellRanks()
        {
            // 1. Load Expected Data
            var json = File.ReadAllText("data/regression_cell_ranks.json");
            var expected = JsonConvert.DeserializeObject<CellRankRegressionData>(json);

            // 2. Setup (Full Pipeline)
            var mapData = TestMapData.TestData;
            GridGenerator.Generate(mapData);
            VoronoiGenerator.CalculateVoronoi(mapData);
            HeightmapGenerator.Generate(mapData);
            FeatureModule.MarkupGrid(mapData);
            GlobeModule.DefineMapSize(mapData);
            GlobeModule.CalculateMapCoordinates(mapData);
            ClimateModule.CalculateTemperatures(mapData);
            ClimateModule.GeneratePrecipitation(mapData);

            var pack = PackModule.ReGraph(mapData);
            FeatureModule.MarkupPack(pack);
            RiverModule.Generate(pack, mapData, allowErosion: true);
            BiomModule.Define(pack, mapData);
            FeatureModule.DefineGroups(pack);

            // 3. Run Logic
            FeatureModule.RankCells(pack);

            // 4. Verification
            Assert.Equal(expected.Suitability.Length, pack.Cells.Length);

            for (int i = 0; i < pack.Cells.Length; i++)
            {
                var cell = pack.Cells[i];

                // Verify Suitability(Int16) -Should be bit-perfect
                Assert.Equal(expected.Suitability[i], cell.Suitability);

                //Verify Population(Float) -Using precision tolerance
                // FMG population can be 0 or a positive float.
                Assert.Equal(expected.Population[i], cell.Population);
            }
        }
    }
}
