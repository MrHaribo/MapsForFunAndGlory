using DelaunatorSharp;
using System.Collections.Generic;

namespace MapGen.Core
{
    public enum FeatureType { Ocean, Lake, Island }

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

        // Features
        public List<MapFeature> Features { get; set; } = new List<MapFeature>();
        public sbyte[] DistanceField { get; set; } // grid.cells.t
        public ushort[] FeatureIds { get; set; }  // grid.cells.f

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
        public ushort FeatureId { get; set; } // f
        public sbyte Distance { get; set; }   // t
    }

    public class MapVertex
    {
        public int Index { get; set; }
        public MapPoint P { get; set; }           // The actual coordinate [x, y]
        public List<int> V { get; set; } = new List<int>(); // Neighboring vertex indices
        public List<int> C { get; set; } = new List<int>(); // Adjacent cell indices
    }

    public class MapFeature
    {
        public int Id { get; set; }
        public bool IsLand { get; set; }
        public bool IsBorder { get; set; }
        public FeatureType Type { get; set; }
    }

    public readonly struct MapPoint
    {
        public readonly double X;
        public readonly double Y;

        public MapPoint(double x, double y) => (X, Y) = (x, y);
        public override string ToString() => $"[{X}, {Y}]";
    }
}