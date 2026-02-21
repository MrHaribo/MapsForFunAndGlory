using DelaunatorSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MapGen.Core
{
    public class Voronoi
    {
        public Delaunator Delaunay { get; }
        public IPoint[] Points { get; }
        public int PointsN { get; }

        public CellData Cells { get; } = new CellData();
        public VertexData Vertices { get; } = new VertexData();

        public Voronoi(Delaunator delaunay, IPoint[] points, int pointsN)
        {
            Delaunay = delaunay;
            Points = points;
            PointsN = pointsN;

            // Initialize collections
            Cells.V = new List<int>[PointsN];
            Cells.C = new List<int>[PointsN];
            Cells.B = new byte[PointsN];

            // The number of triangles determines the number of Voronoi vertices
            int triangleCount = Delaunay.Triangles.Length / 3;
            Vertices.P = new IPoint[triangleCount];
            Vertices.V = new List<int>[triangleCount];
            Vertices.C = new List<int>[triangleCount];

            for (int e = 0; e < Delaunay.Triangles.Length; e++)
            {
                // Point ID where the half-edge starts
                int p = Delaunay.Triangles[NextHalfedge(e)];

                if (p < PointsN && Cells.C[p] == null)
                {
                    var edges = EdgesAroundPoint(e);
                    Cells.V[p] = edges.Select(ex => TriangleOfEdge(ex)).ToList();
                    Cells.C[p] = edges.Select(ex => Delaunay.Triangles[ex]).Where(c => c < PointsN).ToList();
                    Cells.B[p] = (byte)(edges.Count > Cells.C[p].Count ? 1 : 0);
                }

                int t = TriangleOfEdge(e);
                if (Vertices.P[t] == null)
                {
                    Vertices.P[t] = GetTriangleCenter(t);
                    Vertices.V[t] = TrianglesAdjacentToTriangle(t);
                    Vertices.C[t] = PointsOfTriangle(t).ToList();
                }
            }
        }

        private int NextHalfedge(int e) => (e % 3 == 2) ? e - 2 : e + 1;
        private int TriangleOfEdge(int e) => (int)Math.Floor(e / 3.0);

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

        private IPoint GetTriangleCenter(int t)
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
            foreach (var edge in new[] { 3 * t, 3 * t + 1, 3 * t + 2 })
            {
                int opposite = Delaunay.Halfedges[edge];
                if (opposite != -1) adjacent.Add(TriangleOfEdge(opposite));
            }
            return adjacent;
        }

        private IPoint Circumcenter(IPoint a, IPoint b, IPoint c)
        {
            double ax = a.X, ay = a.Y;
            double bx = b.X, by = b.Y;
            double cx = c.X, cy = c.Y;
            double ad = ax * ax + ay * ay;
            double bd = bx * bx + by * by;
            double cd = cx * cx + cy * cy;
            double D = 2 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));

            // Match Azgaar's Math.floor snapping
            return new Point(
                Math.Floor(1 / D * (ad * (by - cy) + bd * (cy - ay) + cd * (ay - by))),
                Math.Floor(1 / D * (ad * (cx - bx) + bd * (ax - cx) + cd * (bx - ax)))
            );
        }
    }
}
