using DelaunatorSharp;

namespace MapGen.Core
{
    public static class VoronoiGenerator
    {
        public static void CalculateVoronoi(MapData data)
        {
            // 1. Combine Playable and Boundary points
            int playableCount = data.PointsCount;
            int boundaryCount = data.BoundaryX.Length;
            int totalCount = playableCount + boundaryCount;

            IPoint[] points = new IPoint[totalCount];
            for (int i = 0; i < playableCount; i++)
                points[i] = new Point(data.X[i], data.Y[i]);

            for (int i = 0; i < boundaryCount; i++)
                points[i + playableCount] = new Point(data.BoundaryX[i], data.BoundaryY[i]);

            // 2. Run Delaunator
            var delaunator = new Delaunator(points);

            // 3. Create the Voronoi Wrapper
            var voronoi = new Voronoi(delaunator, points, playableCount);

            // 4. Store the results in _data (to be implemented)
            data.Cells = voronoi.Cells;
            data.Vertices = voronoi.Vertices;
        }
    }
}
