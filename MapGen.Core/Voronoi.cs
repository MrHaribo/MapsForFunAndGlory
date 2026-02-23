using DelaunatorSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MapGen.Core
{
    public class Voronoi
    {
        public Delaunator Delaunay { get; }
        public MapPoint[] Points { get; }
        public int PointsN { get; }

        // Now matching your MapData structure
        public MapCell[] Cells { get; }
        public MapVertex[] Vertices { get; }

        public Voronoi(Delaunator delaunay, MapPoint[] points, int pointsN)
        {
            Delaunay = delaunay;
            Points = points;
            PointsN = pointsN;

            // 1. Initialize the Object Arrays
            Cells = new MapCell[PointsN];
            for (int i = 0; i < PointsN; i++) Cells[i] = new MapCell { Index = i };

            int triangleCount = Delaunay.Triangles.Length / 3;
            Vertices = new MapVertex[triangleCount];
            for (int i = 0; i < triangleCount; i++) Vertices[i] = new MapVertex { Index = i };

            // 2. Populate Data
            for (int e = 0; e < Delaunay.Triangles.Length; e++)
            {
                // Point ID where the half-edge starts
                int p = Delaunay.Triangles[NextHalfedge(e)];

                // Fill Cell Data (Adjacency and Vertices)
                if (p < PointsN && Cells[p].C.Count == 0)
                {
                    var edges = EdgesAroundPoint(e);

                    // MapCell.V = Indices of triangles (Voronoi vertices)
                    Cells[p].V = edges.Select(ex => TriangleOfEdge(ex)).ToList();

                    // MapCell.C = Indices of neighbor points (Voronoi neighbors)
                    Cells[p].C = edges.Select(ex => Delaunay.Triangles[ex])
                                      .Where(c => c < PointsN).ToList();

                    // MapCell.B = Border flag
                    Cells[p].B = (byte)(edges.Count > Cells[p].C.Count ? 1 : 0);
                }

                // Fill Vertex Data (Circumcenters and Relationships)
                int t = TriangleOfEdge(e);
                if (Vertices[t].P.X == 0 && Vertices[t].P.Y == 0) // Check if uninitialized
                {
                    Vertices[t].P = GetTriangleCenter(t);

                    // MapVertex.V = Neighboring triangles
                    Vertices[t].V = TrianglesAdjacentToTriangle(t);

                    // MapVertex.C = Points forming the triangle
                    Vertices[t].C = PointsOfTriangle(t).ToList();
                }
            }
        }

        private int NextHalfedge(int e) => (e % 3 == 2) ? e - 2 : e + 1;
        private int TriangleOfEdge(int e) => e == -1 ? -1 : e / 3;

        private List<int> EdgesAroundPoint(int start)
        {
            var result = new List<int>();
            int incoming = start;
            do
            {
                result.Add(incoming);
                int outgoing = NextHalfedge(incoming);
                incoming = Delaunay.Halfedges[outgoing];
            } while (incoming != -1 && incoming != start && result.Count < 20);
            return result;
        }

        private MapPoint GetTriangleCenter(int t)
        {
            var pIdx = PointsOfTriangle(t);
            return Circumcenter(Points[pIdx[0]], Points[pIdx[1]], Points[pIdx[2]]);
        }

        private int[] PointsOfTriangle(int t) => new int[]
        {
            Delaunay.Triangles[3 * t],
            Delaunay.Triangles[3 * t + 1],
            Delaunay.Triangles[3 * t + 2]
        };

        private List<int> TrianglesAdjacentToTriangle(int t)
        {
            var adjacent = new List<int>();

            // Explicitly iterate through the 3 edges of triangle t
            for (int i = 0; i < 3; i++)
            {
                int edge = 3 * t + i;
                int opposite = Delaunay.Halfedges[edge];

                // Use the updated TriangleOfEdge that handles -1
                adjacent.Add(TriangleOfEdge(opposite));
            }

            return adjacent;
        }

        private MapPoint Circumcenter(MapPoint a, MapPoint b, MapPoint c)
        {
            double ax = a.X, ay = a.Y;
            double bx = b.X, by = b.Y;
            double cx = c.X, cy = c.Y;
            double ad = ax * ax + ay * ay;
            double bd = bx * bx + by * by;
            double cd = cx * cx + cy * cy;
            double D = 2 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));

            // Using your MapPoint struct with Math.Floor snapping
            return new MapPoint(
                Math.Floor(1 / D * (ad * (by - cy) + bd * (cy - ay) + cd * (ay - by))),
                Math.Floor(1 / D * (ad * (cx - bx) + bd * (ax - cx) + cd * (bx - ax)))
            );
        }
    }
}
