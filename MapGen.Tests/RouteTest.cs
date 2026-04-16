using MapGen.Core.Modules;
using Newtonsoft.Json;

namespace MapGen.Tests
{
    public class RegressionRoutesData
    {
        public string seed { get; set; }
        public List<RegressionRoute> routes { get; set; }

        // Added to capture pack.cells.routes
        public Dictionary<int, Dictionary<int, int>> routeLinks { get; set; }
    }

    public class RegressionRoute
    {
        public int id { get; set; }
        public string group { get; set; }
        public int featureId { get; set; }
        public List<RegressionRoutePoint> points { get; set; }
    }

    public class RegressionRoutePoint
    {
        public double x { get; set; }
        public double y { get; set; }
        public int cellId { get; set; }
    }

    public class RouteTest
    {
        [Fact]
        public void TestRoutes_MatchesRegression()
        {
            // 1. Load Expected Data
            var json = File.ReadAllText($"data/regression_routes.json");
            var expectedData = JsonConvert.DeserializeObject<RegressionRoutesData>(json);

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
            RiverModule.Generate(pack);
            BiomModule.Define(pack);
            FeatureModule.DefineGroups(pack);
            FeatureModule.RankCells(pack);
            CultureModule.Generate(pack);
            CultureModule.ExpandCultures(pack);
            BurgModule.Generate(pack);
            StateModule.Generate(pack);

            // 3. Execute
            RouteModule.Generate(pack);

            // --- 4. Assertions ---

            Assert.NotNull(pack.Routes);
            Assert.Equal(expectedData.routes.Count, pack.Routes.Count);

            for (int i = 0; i < expectedData.routes.Count; i++)
            {
                var expectedRoute = expectedData.routes[i];
                var actualRoute = pack.Routes[i];

                Assert.Equal(expectedRoute.id, actualRoute.Id);
                Assert.Equal(expectedRoute.group, actualRoute.Group);
                Assert.Equal(expectedRoute.featureId, actualRoute.FeatureId);

                Assert.Equal(expectedRoute.points.Count, actualRoute.Points.Count);

                for (int p = 0; p < expectedRoute.points.Count; p++)
                {
                    var expectedPoint = expectedRoute.points[p];
                    var actualPoint = actualRoute.Points[p];

                    Assert.Equal(expectedPoint.cellId, actualPoint.CellId);
                    //Assert.Equal(expectedPoint.x, actualPoint.X, 2);
                    //Assert.Equal(expectedPoint.y, actualPoint.Y, 2);

                    // 2. VISUAL Parity: Accommodate microscopic CLR vs V8 float rounding crossings
                    Assert.True(Math.Abs(expectedPoint.x - actualPoint.X) <= 0.02,
                        $"X deviation {expectedPoint.x} vs {actualPoint.X} on Route {actualRoute.Id}, Cell {actualPoint.CellId}");

                    Assert.True(Math.Abs(expectedPoint.y - actualPoint.Y) <= 0.02,
                        $"Y deviation {expectedPoint.y} vs {actualPoint.Y} on Route {actualRoute.Id}, Cell {actualPoint.CellId}");
                }
            }

            // --- 5. RouteLinks Assertions ---

            Assert.NotNull(pack.RouteLinks);
            Assert.Equal(expectedData.routeLinks.Count, pack.RouteLinks.Count);

            foreach (var expectedOuterKvp in expectedData.routeLinks)
            {
                int fromCell = expectedOuterKvp.Key;
                var expectedInnerDict = expectedOuterKvp.Value;

                // Ensure C# generated links for this cell
                Assert.True(pack.RouteLinks.ContainsKey(fromCell), $"RouteLinks missing from-cell {fromCell}");

                var actualInnerDict = pack.RouteLinks[fromCell];
                Assert.Equal(expectedInnerDict.Count, actualInnerDict.Count);

                foreach (var expectedInnerKvp in expectedInnerDict)
                {
                    int toCell = expectedInnerKvp.Key;
                    int expectedRouteId = expectedInnerKvp.Value;

                    // Ensure C# generated the specific connection to the target cell
                    Assert.True(actualInnerDict.ContainsKey(toCell), $"RouteLinks missing to-cell {toCell} for from-cell {fromCell}");

                    // Ensure the connection maps to the exact same Route ID
                    Assert.Equal(expectedRouteId, actualInnerDict[toCell]);
                }
            }

        }
    }
}
