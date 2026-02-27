using MapGen.Core.Helpers;
using MapGen.Core.Modules;
using System.Collections.Generic;

namespace MapGen.Core
{
    public delegate int ClosestCell(double x, double y);
    public delegate int ClosestCellInRange(double x, double y, double radius);

    public enum FeatureType { Ocean, Lake, Island }

    public class MapData
    {
        // Options
        public MapOptions Options { get; set; }
        public HeightmapTemplate Template => Options.Template;

        // Rng
        public string Seed => Options.Seed;
        public IRandom Rng { get; set; }

        // Grid Constants
        public int Width => Options.Width;
        public int Height => Options.Height;
        public int PointsCount => Options.PointsCount;
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

        // Globe Settings (Inputs)
        public double MapSize { get; set; }      // 1-100%
        public double Latitude { get; set; }     // 0-100 shift
        public double Longitude { get; set; }    // 0-100 shift

        // Calculated Coordinates (Outputs)
        public MapCoordinates Coords { get; set; } = new MapCoordinates();


    }

    public class MapPack
    {
        public MapCell[] Cells { get; set; }
        public MapVertex[] Vertices { get; set; }
        public MapPoint[] Points { get; set; }

        // Spatial queries
        public ClosestCell FindCell { get; set; }
        public ClosestCellInRange FindCellInRange { get; set; }

        public MapOptions Options { get; set; }

        public List<MapFeature> Features { get; set; } = new List<MapFeature>();
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
        public sbyte Temp { get; set; }
        public byte Prec { get; set; }

        // Map Pack Properties
        public ushort Area { get; set; } // Added for MapPack parity (Pack only)
        public int G { get; set; }       // Mapping back to Grid cell index (Pack only)
        
        // Geographical
        public int Haven { get; set; }   // Index of the closest water cell (for land cells)
        public byte Harbor { get; set; } // Number of adjacent water cells
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

        // Delta for markupPack
        public int CellsCount { get; set; }
        public int FirstCell { get; set; }
        public List<int> Vertices { get; set; } = new List<int>();
        public double Area { get; set; }
        public List<int> Shoreline { get; set; } // Only for lakes/islands
    }

    public class MapCoordinates
    {
        public double LatT { get; set; } // Total Latitude span
        public double LatN { get; set; } // Northern Latitude
        public double LatS { get; set; } // Southern Latitude
        public double LonT { get; set; } // Total Longitude span
        public double LonW { get; set; } // Western Longitude
        public double LonE { get; set; } // Eastern Longitude
    }

    public readonly struct MapPoint
    {
        public readonly double X;
        public readonly double Y;

        public MapPoint(double x, double y) => (X, Y) = (x, y);
        public override string ToString() => $"[{X}, {Y}]";
    }
}