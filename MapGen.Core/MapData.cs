using DelaunatorSharp;
using System.Collections.Generic;

namespace MapGen.Core
{
    public class MapData
    {
        // Grid Constants
        public int Width { get; set; }
        public int Height { get; set; }
        public int PointsCount { get; set; }
        public double Spacing { get; set; }

        // Raw Coordinates (The "Grid")
        public MapPoint[] Points { get; set; }
        public MapPoint[] BoundaryPoints { get; set; }
        public int CellsCountX { get; set; }
        public int CellsCountY { get; set; }
        public int CellsCount { get; set; }

        // Voronoi Data
        public MapCell[] Cells { get; set; }
        public MapVertex[] Vertices { get; set; }

        public MapData(int count, int width, int height)
        {
            PointsCount = count;
            Width = width;
            Height = height;
        }
    }

    public class MapCell
    {
        public int Index { get; set; }
        public List<int> V { get; set; } = new List<int>(); // Indices of vertices forming this cell
        public List<int> C { get; set; } = new List<int>(); // Indices of neighboring cells (Adjacency)
        public byte B { get; set; }               // Border flag (1 if it touches the edge)
        public byte H { get; set; }               // Height value
    }

    public class MapVertex
    {
        public int Index { get; set; }
        public MapPoint P { get; set; }           // The actual coordinate [x, y]
        public List<int> V { get; set; } = new List<int>(); // Neighboring vertex indices
        public List<int> C { get; set; } = new List<int>(); // Adjacent cell indices
    }

    public readonly struct MapPoint
    {
        public readonly double X;
        public readonly double Y;

        public MapPoint(double x, double y)
        {
            X = x;
            Y = y;
        }

        // Handy for debugging or matching JS output exactly
        public override string ToString() => $"[{X}, {Y}]";
    }
}