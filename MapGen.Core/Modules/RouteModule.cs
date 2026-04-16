using DelaunatorSharp;
using MapGen.Core.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MapGen.Core.Modules
{
    public static class RouteModule
    {
        private const double ROUTES_SHARP_ANGLE = 135.0;
        private const double ROUTES_VERY_SHARP_ANGLE = 115.0;
        private const int MIN_PASSABLE_SEA_TEMP = -4;

        private static readonly Dictionary<int, double> RouteTypeModifiers = new Dictionary<int, double>
        {
            { -1, 1.0 }, // coastline
            { -2, 1.8 }, // sea
            { -3, 4.0 }, // open sea
            { -4, 6.0 }  // ocean
        };

        // Temporary internal class to track route segments before finalizing
        private class RawRoute
        {
            public int Feature { get; set; }
            public List<int> Cells { get; set; }
            public bool Merged { get; set; }
        }

        public static void Generate(MapPack pack, List<MapRoute> lockedRoutes = null)
        {
            lockedRoutes ??= new List<MapRoute>();

            // 1. Sort Burgs
            var (burgsByFeature, capitalsByFeature, portsByFeature) = SortBurgsByFeature(pack.Burgs);

            // 2. Track connections using a ValueTuple HashSet instead of string concatenation for performance
            var connectionsSet = new HashSet<(int, int)>();

            var bioms = BiomModule.GetDefaultBiomes();

            // Add locked route connections
            foreach (var route in lockedRoutes)
            {
                var cellIds = route.Points.Select(p => p.CellId).ToList();
                AddConnections(cellIds);
            }

            // 3. Generate initial raw routes
            var mainRoads = GenerateMainRoads();
            var trails = GenerateTrails();
            var seaRoutes = GenerateSeaRoutes();

            // 4. Finalize and map to cells
            pack.Routes = CreateRoutesData(lockedRoutes);
            pack.RouteLinks = BuildLinks(pack.Routes);


            // ==============================================================================
            // LOCAL FUNCTIONS (Acting as JS Closures sharing the scope of 'pack' and 'connectionsSet')
            // ==============================================================================

            (Dictionary<int, List<MapBurg>>, Dictionary<int, List<MapBurg>>, Dictionary<int, List<MapBurg>>) SortBurgsByFeature(List<MapBurg> burgs)
            {
                var bbf = new Dictionary<int, List<MapBurg>>();
                var cbf = new Dictionary<int, List<MapBurg>>();
                var pbf = new Dictionary<int, List<MapBurg>>();

                void AddBurg(Dictionary<int, List<MapBurg>> collection, int feature, MapBurg burg)
                {
                    if (!collection.ContainsKey(feature)) collection[feature] = new List<MapBurg>();
                    collection[feature].Add(burg);
                }

                foreach (var burg in burgs)
                {
                    if (burg.Id > 0)
                    {
                        AddBurg(bbf, burg.FeatureId, burg);
                        if (burg.IsCapital) AddBurg(cbf, burg.FeatureId, burg);
                        if (burg.IsPort) AddBurg(pbf, burg.PortId, burg);
                    }
                }

                return (bbf, cbf, pbf);
            }

            List<RawRoute> GenerateMainRoads()
            {
                var routes = new List<RawRoute>();
                foreach (var kvp in capitalsByFeature)
                {
                    var featureCapitals = kvp.Value;
                    var points = featureCapitals.Select(b => new MapPoint(b.Position.X, b.Position.Y)).ToList();
                    var urquhartEdges = CalculateUrquhartEdges(points);

                    foreach (var edge in urquhartEdges)
                    {
                        int start = featureCapitals[edge.Item1].CellId;
                        int exit = featureCapitals[edge.Item2].CellId;

                        var segments = FindPathSegments(false, start, exit);
                        foreach (var segment in segments)
                        {
                            AddConnections(segment);
                            routes.Add(new RawRoute { Feature = kvp.Key, Cells = segment });
                        }
                    }
                }
                return routes;
            }

            List<RawRoute> GenerateTrails()
            {
                var routes = new List<RawRoute>();
                foreach (var kvp in burgsByFeature)
                {
                    var featureBurgs = kvp.Value;
                    var points = featureBurgs.Select(b => new MapPoint(b.Position.X, b.Position.Y)).ToList();
                    var urquhartEdges = CalculateUrquhartEdges(points);

                    foreach (var edge in urquhartEdges)
                    {
                        int start = featureBurgs[edge.Item1].CellId;
                        int exit = featureBurgs[edge.Item2].CellId;

                        var segments = FindPathSegments(false, start, exit);
                        foreach (var segment in segments)
                        {
                            AddConnections(segment);
                            routes.Add(new RawRoute { Feature = kvp.Key, Cells = segment });
                        }
                    }
                }
                return routes;
            }

            List<RawRoute> GenerateSeaRoutes()
            {
                var routes = new List<RawRoute>();
                foreach (var kvp in portsByFeature)
                {
                    var featurePorts = kvp.Value;
                    var points = featurePorts.Select(b => new MapPoint(b.Position.X, b.Position.Y)).ToList();
                    var urquhartEdges = CalculateUrquhartEdges(points);

                    foreach (var edge in urquhartEdges)
                    {
                        int start = featurePorts[edge.Item1].CellId;
                        int exit = featurePorts[edge.Item2].CellId;

                        var segments = FindPathSegments(true, start, exit);
                        foreach (var segment in segments)
                        {
                            AddConnections(segment);
                            routes.Add(new RawRoute { Feature = kvp.Key, Cells = segment });
                        }
                    }
                }
                return routes;
            }

            void AddConnections(List<int> segment)
            {
                for (int i = 0; i < segment.Count - 1; i++)
                {
                    int cellId = segment[i];
                    int nextCellId = segment[i + 1];
                    connectionsSet.Add((cellId, nextCellId));
                    connectionsSet.Add((nextCellId, cellId));
                }
            }

            List<List<int>> FindPathSegments(bool isWater, int start, int exit)
            {
                var getCost = CreateCostEvaluator(isWater);

                // Note: We'll need your pathfinding utility (A* or Dijkstra) injected here
                var pathCells = PathUtils.FindPath(pack, start, current => current == exit, getCost);

                if (pathCells == null || pathCells.Count == 0) return new List<List<int>>();
                return GetRouteSegments(pathCells);
            }

            Func<int, int, double> CreateCostEvaluator(bool isWater)
            {
                if (isWater)
                {
                    return (current, next) =>
                    {
                        var nextCell = pack.Cells[next];

                        if (nextCell.Height >= 20) return double.PositiveInfinity;

                        // Because you mapped Temp directly to MapCell, we can check it right here!
                        if (nextCell.Temp < MIN_PASSABLE_SEA_TEMP) return double.PositiveInfinity;

                        double distanceCost = PathUtils.Dist2(pack.Cells[current].Point, nextCell.Point);

                        // FIX: Use nextCell.Distance to get the sea depth (-1, -2, -3, -4)
                        double typeModifier = RouteTypeModifiers.TryGetValue(nextCell.Distance, out double mod) ? mod : 8.0;
                        double connectionModifier = connectionsSet.Contains((current, next)) ? 0.5 : 1.0;

                        return distanceCost * typeModifier * connectionModifier;
                    };
                }
                else
                {
                    return (current, next) =>
                    {
                        var nextCell = pack.Cells[next];

                        if (nextCell.Height < 20) return double.PositiveInfinity;

                        int biome = nextCell.BiomeId;

                        // NOTE: Ensure `bioms` is accessible in your scope (e.g., passed in or a static config)
                        int habitability = bioms[biome].Habitability;

                        if (habitability == 0) return double.PositiveInfinity;

                        double distanceCost = PathUtils.Dist2(pack.Cells[current].Point, nextCell.Point);
                        double habitabilityModifier = 1.0 + Math.Max(100 - habitability, 0) / 1000.0;
                        double heightModifier = 1.0 + Math.Max(nextCell.Height - 25, 25) / 25.0;
                        double connectionModifier = connectionsSet.Contains((current, next)) ? 0.5 : 1.0;

                        // FIX: Use your BurgId property to check if a burg exists
                        double burgModifier = nextCell.BurgId > 0 ? 1.0 : 3.0;

                        return distanceCost * habitabilityModifier * heightModifier * connectionModifier * burgModifier;
                    };
                }
            }

            List<List<int>> GetRouteSegments(List<int> pathCells)
            {
                var segments = new List<List<int>>();
                var segment = new List<int>();

                for (int i = 0; i < pathCells.Count; i++)
                {
                    int cellId = pathCells[i];
                    int nextCellId = (i + 1 < pathCells.Count) ? pathCells[i + 1] : -1;

                    bool isConnected = nextCellId != -1 &&
                        (connectionsSet.Contains((cellId, nextCellId)) || connectionsSet.Contains((nextCellId, cellId)));

                    if (isConnected)
                    {
                        if (segment.Count > 0)
                        {
                            segment.Add(cellId);
                            segments.Add(segment);
                            segment = new List<int>();
                        }
                        continue;
                    }

                    segment.Add(cellId);
                }

                if (segment.Count > 1) segments.Add(segment);

                return segments;
            }

            List<MapRoute> CreateRoutesData(List<MapRoute> routes)
            {
                var pointsArray = PreparePointsArray();

                MergeRoutes(mainRoads);
                foreach (var route in mainRoads.Where(r => !r.Merged))
                {
                    var points = GetPoints("roads", route.Cells, pointsArray);
                    routes.Add(new MapRoute { Id = routes.Count, Group = "roads", FeatureId = route.Feature, Points = points });
                }

                MergeRoutes(trails);
                foreach (var route in trails.Where(r => !r.Merged))
                {
                    var points = GetPoints("trails", route.Cells, pointsArray);
                    routes.Add(new MapRoute { Id = routes.Count, Group = "trails", FeatureId = route.Feature, Points = points });
                }

                MergeRoutes(seaRoutes);
                foreach (var route in seaRoutes.Where(r => !r.Merged))
                {
                    var points = GetPoints("searoutes", route.Cells, pointsArray);
                    routes.Add(new MapRoute { Id = routes.Count, Group = "searoutes", FeatureId = route.Feature, Points = points });
                }

                return routes;
            }

            // Using iterative loop rather than JS recursive iteration to avoid stack depth issues
            void MergeRoutes(List<RawRoute> routesList)
            {
                int routesMerged;
                do
                {
                    routesMerged = 0;
                    for (int i = 0; i < routesList.Count; i++)
                    {
                        var thisRoute = routesList[i];
                        if (thisRoute.Merged) continue;

                        for (int j = i + 1; j < routesList.Count; j++)
                        {
                            var nextRoute = routesList[j];
                            if (nextRoute.Merged) continue;

                            if (nextRoute.Cells.First() == thisRoute.Cells.Last())
                            {
                                routesMerged++;
                                thisRoute.Cells.AddRange(nextRoute.Cells.Skip(1));
                                nextRoute.Merged = true;
                            }
                        }
                    }
                }
                while (routesMerged > 1); // EXACT PARITY: Replicates Azgaar's > 1 early-termination bug!
            }

            List<MapPoint> PreparePointsArray()
            {
                var points = new List<MapPoint>(pack.Cells.Length);
                for (int i = 0; i < pack.Cells.Length; i++)
                {
                    int burgId = pack.Cells[i].BurgId;
                    if (burgId > 0)
                    {
                        var burg = pack.GetBurg(burgId);


                        // --- DIAGNOSTIC TRAP 1 ---
                        if (Math.Abs(burg.Position.X - 685.85) < 0.1 || Math.Abs(burg.Position.Y - 685.85) < 0.1)
                        {
                            // HOVER OVER `burgId` to find out WHICH burg this is!
                            System.Diagnostics.Debugger.Break();
                        }
                        // -------------------------


                        points.Add(new MapPoint(burg.Position.X, burg.Position.Y));
                    }
                    else
                    {
                        points.Add(new MapPoint(pack.Cells[i].Point.X, pack.Cells[i].Point.Y));
                    }
                }
                return points;
            }

            List<MapRoutePoint> GetPoints(string group, List<int> cells, List<MapPoint> pointsArray)
            {
                var data = cells.Select(cellId => new MapRoutePoint(pointsArray[cellId].X, pointsArray[cellId].Y, cellId)).ToList();

                if (group != "searoutes")
                {
                    for (int i = 1; i < cells.Count - 1; i++)
                    {
                        int cellId = cells[i];
                        if (pack.Cells[cellId].BurgId > 0) continue;

                        var prev = data[i - 1];
                        var curr = data[i];
                        var next = data[i + 1];

                        double dAx = prev.X - curr.X;
                        double dAy = prev.Y - curr.Y;
                        double dBx = next.X - curr.X;
                        double dBy = next.Y - curr.Y;

                        double angle = Math.Abs((Math.Atan2(dAx * dBy - dAy * dBx, dAx * dBx + dAy * dBy) * 180.0) / Math.PI);

                        if (angle < ROUTES_SHARP_ANGLE)
                        {
                            double middleX = (prev.X + next.X) / 2.0;
                            double middleY = (prev.Y + next.Y) / 2.0;
                            double newX, newY;

                            if (angle < ROUTES_VERY_SHARP_ANGLE)
                            {
                                newX = NumberUtils.Round((curr.X + middleX * 2) / 3.0, 2);
                                newY = NumberUtils.Round((curr.Y + middleY * 2) / 3.0, 2);
                            }
                            else
                            {
                                newX = NumberUtils.Round((curr.X + middleX) / 2.0, 2);
                                newY = NumberUtils.Round((curr.Y + middleY) / 2.0, 2);
                            }

                            if (pack.FindCell(newX, newY) == cellId)
                            {
                                data[i] = new MapRoutePoint(newX, newY, cellId);
                                pointsArray[cellId] = new MapPoint(newX, newY); // modify global array like JS
                            }
                        }
                    }
                }
                return data;
            }

            Dictionary<int, Dictionary<int, int>> BuildLinks(List<MapRoute> parsedRoutes)
            {
                var links = new Dictionary<int, Dictionary<int, int>>();

                foreach (var route in parsedRoutes)
                {
                    var routeCells = route.Points.Select(p => p.CellId).ToList();

                    for (int i = 0; i < routeCells.Count - 1; i++)
                    {
                        int cellId = routeCells[i];
                        int nextCellId = routeCells[i + 1];

                        if (cellId != nextCellId)
                        {
                            if (!links.ContainsKey(cellId)) links[cellId] = new Dictionary<int, int>();
                            links[cellId][nextCellId] = route.Id;

                            if (!links.ContainsKey(nextCellId)) links[nextCellId] = new Dictionary<int, int>();
                            links[nextCellId][cellId] = route.Id;
                        }
                    }
                }

                return links;
            }

            // Port of observablehq.com/@mbostock/urquhart-graph
            List<(int, int)> CalculateUrquhartEdges(List<MapPoint> points)
            {
                var edges = new List<(int, int)>();

                // 1. Guard against < 3 points (DelaunatorSharp explicitly throws here)
                if (points.Count < 3)
                {
                    // STRICT JS PARITY: 
                    // JS returns an empty list, meaning 2 burgs on an island get no road.
                    // If you want strict JS parity, leave this returning empty.
                    //
                    // OPTIONAL FIX: If there are exactly 2 points, connect them!
                    //if (points.Count == 2)
                    //{
                    //    edges.Add((0, 1));
                    //}
                    return edges;
                }

                double Score(int p0, int p1) => PathUtils.Dist2(points[p0], points[p1]);

                Delaunator delaunay;
                try
                {
                    delaunay = new Delaunator(points.Select(p => (IPoint)new Point(p.X, p.Y)).ToArray());
                }
                catch (Exception)
                {
                    // 2. Guard against Collinear points.
                    // DelaunatorSharp throws "No seed triangle found" if points are on a straight line.
                    // JS natively swallows this and produces 0 triangles. 
                    // Returning an empty list maintains perfect JS parity.
                    return edges;
                }

                var halfedges = delaunay.Halfedges;
                var triangles = delaunay.Triangles;
                int n = triangles.Length;

                var removed = new byte[n];

                for (int e = 0; e < n; e += 3)
                {
                    int p0 = triangles[e];
                    int p1 = triangles[e + 1];
                    int p2 = triangles[e + 2];

                    double p01 = Score(p0, p1);
                    double p12 = Score(p1, p2);
                    double p20 = Score(p2, p0);

                    int maxEdge;
                    if (p20 > p01 && p20 > p12)
                    {
                        maxEdge = Math.Max(e + 2, halfedges[e + 2]);
                    }
                    else if (p12 > p01 && p12 > p20)
                    {
                        maxEdge = Math.Max(e + 1, halfedges[e + 1]);
                    }
                    else
                    {
                        maxEdge = Math.Max(e, halfedges[e]);
                    }

                    removed[maxEdge] = 1;
                }

                for (int e = 0; e < n; ++e)
                {
                    if (e > halfedges[e] && removed[e] == 0)
                    {
                        int t0 = triangles[e];
                        int t1 = triangles[e % 3 == 2 ? e - 2 : e + 1];
                        edges.Add((t0, t1));
                    }
                }

                return edges;
            }
        }
    }
}