using DelaunatorSharp;
using System;

namespace MapGen.Core.Modules
{
    public static class VoronoiGenerator
    {
        public static void CalculateVoronoi(MapData data)
        {
            // 1. Calculate the Voronoi
            var (cells, vertices) = CalculateVoronoi(data.Points, data.BoundaryPoints);

            // 2. Store the results in MapData
            data.Cells = cells;
            data.CellsCount = cells.Length;
            data.Vertices = vertices;
        }

        public static (MapCell[] Cells, MapVertex[] Vertices) CalculateVoronoi(MapPoint[] points, MapPoint[] boundary)
        {
            // 1. Combine Playable and Boundary points into our domain array
            int playableCount = points.Length;
            int boundaryCount = boundary.Length;
            int totalCount = playableCount + boundaryCount;

            MapPoint[] allPoints = new MapPoint[totalCount];
            Array.Copy(points, 0, allPoints, 0, playableCount);
            Array.Copy(boundary, 0, allPoints, playableCount, boundaryCount);

            // 2. Project MapPoints to the library's expected Point type
            // This keeps the IPoint dependency local to this method
            IPoint[] libraryPoints = new IPoint[totalCount];
            for (int i = 0; i < totalCount; i++)
            {
                libraryPoints[i] = new Point(allPoints[i].X, allPoints[i].Y);
            }

            // 3. Run Delaunator using the projected library points
            var delaunator = new Delaunator(libraryPoints);

            // 4. Create the Voronoi Wrapper 
            // We pass our clean MapPoint[] domain array so the Voronoi class 
            // doesn't have to deal with library-specific types.
            var voronoi = new Voronoi(delaunator, allPoints, playableCount);

            // 5. Store the results in MapData
            return (voronoi.Cells, voronoi.Vertices);
        }
    }
}
