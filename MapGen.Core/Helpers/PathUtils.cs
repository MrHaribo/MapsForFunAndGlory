using System;
using System.Collections.Generic;
using System.Linq;

namespace MapGen.Core.Helpers
{
    public static class PathUtils
    {
        public static List<int> ConnectVertices(MapPack pack, int startingVertex, Func<int, bool> ofSameType, Action<int> addToChecked = null, bool closeRing = false)
        {
            var vertices = pack.Vertices;
            int maxIterations = vertices.Length;
            var chain = new List<int>();

            int next = startingVertex;
            int previous = -1;

            for (int i = 0; i == 0 || next != startingVertex; i++)
            {
                int current = next;
                chain.Add(current);

                var neibCells = vertices[current].C;
                if (addToChecked != null)
                {
                    foreach (var c in neibCells)
                        if (ofSameType(c)) addToChecked(c);
                }

                // D3 vertices usually have 3 adjacent cells and 3 adjacent vertices
                // We check the 'type' of the 3 cells to decide which vertex to follow
                var cTypes = neibCells.Select(ofSameType).ToArray();
                var vNeighbors = vertices[current].V;

                // Logic: Follow the boundary where the cell type changes
                if (vNeighbors.Count > 0 && vNeighbors[0] != previous && cTypes[0] != cTypes[1]) next = vNeighbors[0];
                else if (vNeighbors.Count > 1 && vNeighbors[1] != previous && cTypes[1] != cTypes[2]) next = vNeighbors[1];
                else if (vNeighbors.Count > 2 && vNeighbors[2] != previous && cTypes[0] != cTypes[2]) next = vNeighbors[2];

                if (next == current || i == maxIterations) break;
                previous = current;
            }

            if (closeRing) chain.Add(startingVertex);
            return chain;
        }

        public static double CalculatePolygonArea(MapCell cell, MapVertex[] vertices)
        {
            double area = 0;
            for (int i = 0; i < cell.V.Count; i++)
            {
                var p1 = vertices[cell.V[i]].P;
                var p2 = vertices[cell.V[(i + 1) % cell.V.Count]].P;
                area += (p1.X * p2.Y) - (p2.X * p1.Y);
            }
            return area / 2.0;
        }

        //public static double CalculatePolygonArea(List<MapPoint> points)
        //{
        //    if (points.Count < 3) return 0;
        //    double area = 0;
        //    var b = points[points.Count - 1];

        //    for (int i = 0; i < points.Count; i++)
        //    {
        //        var a = b;
        //        b = points[i];
        //        area += a.Y * b.X - a.X * b.Y;
        //    }
        //    return area / 2.0;
        //}

        public static double CalculatePolygonArea(List<MapPoint> points)
        {
            if (points.Count < 3) return 0;
            double area = 0;
            for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
            {
                // Aligned with D3: (prevY * currX) - (prevX * currY)
                area += (points[j].Y * points[i].X) - (points[j].X * points[i].Y);
            }
            return area / 2.0;
        }

        // D3 polygonArea equivalent
        public static double CalculatePolygonArea(List<double[]> points)
        {
            double area = 0;
            for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
            {
                area += (points[j][0] + points[i][0]) * (points[j][1] - points[i][1]);
            }
            return area / 2.0;
        }
    }
}
