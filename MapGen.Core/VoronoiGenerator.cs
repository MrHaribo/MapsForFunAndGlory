using DelaunatorSharp;
using System;

namespace MapGen.Core
{
    public static class VoronoiGenerator
    {
        public static void CalculateVoronoi(MapData data)
        {
            // 1. Combine Playable and Boundary points into our domain array
            int playableCount = data.Points.Length;
            int boundaryCount = data.BoundaryPoints.Length;
            int totalCount = playableCount + boundaryCount;

            MapPoint[] allPoints = new MapPoint[totalCount];
            Array.Copy(data.Points, 0, allPoints, 0, playableCount);
            Array.Copy(data.BoundaryPoints, 0, allPoints, playableCount, boundaryCount);

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
            data.Cells = voronoi.Cells;
            data.Vertices = voronoi.Vertices;
            data.CellsCount = voronoi.Cells.Length;
        }
    }
}
