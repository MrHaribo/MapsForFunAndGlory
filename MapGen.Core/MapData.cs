using MapGen.Core.Helpers;
using MapGen.Core.Modules;
using System;
using System.Collections.Generic;

namespace MapGen.Core
{
    public delegate int ClosestCell(double x, double y);
    public delegate int ClosestCellInRange(double x, double y, double radius);

    public interface IMapGraph
    {
        public IRandom Rng { get; }
        string Seed { get; }

        int Width { get; }
        int Height { get; }
        int PointsCount { get; }

        MapCell[] Cells { get; set; }
        MapVertex[] Vertices { get; set; }
        MapPoint[] Points { get; set; }

        public List<MapFeature> Features { get; set; }
        MapFeature GetFeature(int id);
    }

    public class MapData : IMapGraph
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

        public MapFeature GetFeature(int id) => Features[id];
        public int FindGridCell(double x, double y)
        {
            // Math.Min ensures we don't go out of bounds on the right/bottom edges
            int row = (int)Math.Floor(Math.Min(y / Spacing, CellsCountY - 1));
            int col = (int)Math.Floor(Math.Min(x / Spacing, CellsCountX - 1));

            return row * CellsCountX + col;
        }
    }

    public class MapPack : IMapGraph
    {
        public MapOptions Options { get; set; }

        // Rng
        public string Seed => Options.Seed;
        public IRandom Rng { get; set; }

        public int Width => Options.Width;
        public int Height => Options.Height;
        public int PointsCount => Options.PointsCount;
        public int GridPointsCount { get; set; }

        public MapCell[] Cells { get; set; }
        public MapVertex[] Vertices { get; set; }
        public MapPoint[] Points { get; set; }

        // Spatial queries
        public ClosestCell FindCell { get; set; }
        public ClosestCellInRange FindCellInRange { get; set; }

        public List<MapFeature> Features { get; set; } = new List<MapFeature>();
        public List<MapRiver> Rivers { get; set; } = new List<MapRiver>();
        public List<MapCulture> Cultures { get; set; } = new List<MapCulture>();
        public List<MapBurg> Burgs { get; set; } = new List<MapBurg>();
        public List<MapState> States { get; set; } = new List<MapState>();

        public MapFeature GetFeature(int id) => Features[id - 1];
        public MapBurg GetBurg(int id) => Burgs[id - 1];
    }

    public class MapCell
    {
        public int Index { get; set; }
        public MapPoint Point { get; set; }
        public List<int> Verticies { get; set; } = new List<int>(); // Indices of vertices forming this cell
        public List<int> NeighborCells { get; set; } = new List<int>(); // Indices of neighboring cells (Adjacency)
        public byte Border { get; set; }               // Border flag (1 if it touches the edge)
        public byte Height { get; set; }               // Height value
        public ushort FeatureId { get; set; } // f
        public sbyte Distance { get; set; }   // t
        public sbyte Temp { get; set; }
        public byte Prec { get; set; }

        // Map Pack Properties
        public ushort Area { get; set; } // Added for MapPack parity (Pack only)
        public int GridId { get; set; }       // Mapping back to Grid cell index (Pack only)
        
        // Geographical
        public ushort Haven { get; set; }   // Index of the closest water cell (for land cells)
        public ushort Harbor { get; set; } // Number of adjacent water cells

        // Rivers
        public double Flux { get; set; }        // cells.fl: water flow volume
        public ushort RiverId { get; set; }     // cells.r: ID of the river passing through
        public double Confluence { get; set; }    // cells.conf: marking confluence points

        // Bioms
        public byte BiomeId { get; set; }

        // Population
        public short Suitability { get; set; } // cells.s
        public float Population { get; set; }   // cells.pop
        public int CultureId { get; set; }
        public ushort BurgId { get; set; }
    }

    public class MapVertex
    {
        public int Index { get; set; }
        public MapPoint Point { get; set; }           // The actual coordinate [x, y]
        public List<int> NeighborVertices { get; set; } = new List<int>(); // Neighboring vertex indices
        public List<int> AdjacentCells { get; set; } = new List<int>(); // Adjacent cell indices
    }

    public class MapFeature
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
        public bool IsLand { get; set; }
        public bool IsBorder { get; set; }
        public FeatureType Type { get; set; }

        // Delta for markupPack
        public int CellsCount { get; set; }
        public int FirstCell { get; set; }
        public double Area { get; set; }
        public double Height { get; set; }
        public List<int> Vertices { get; set; }         // Original raw data (Regression-safe)
        public List<int> ShorelineVertices { get; set; }  // Consistent winding (Mesh-safe)
        public List<int> ShorelineCells { get; set; }        // Coastal neighbors

        // Added for Lake-River interactions
        public ushort RiverId { get; set; }     // The main river associated with this lake
        public double EnteringFlux { get; set; }
        public double Flux { get; set; }        // Total flux passing through the lake
        public List<ushort> Inlets { get; set; } = new List<ushort>();
        public int OutCell { get; set; }        // The cell where the lake drains
        public double Evaporation { get; set; } // Calculated based on area/climate
        public bool IsClosed { get; set; }
        public double Temp { get; set; }
        public FeatureGroup Group { get; set; }
    }

    public class MapCoordinates
    {
        public double LatTotal { get; set; } // Total Latitude span
        public double LatNorth { get; set; } // Northern Latitude
        public double LatSouth { get; set; } // Southern Latitude
        public double LonTotal { get; set; } // Total Longitude span
        public double LonWest { get; set; } // Western Longitude
        public double LonEast { get; set; } // Eastern Longitude
    }

    public class MapRiver
    {
        public int Id { get; set; }
        public int Source { get; set; }
        public int Mouth { get; set; }
        public double Discharge { get; set; }
        public double Length { get; set; }
        public double Width { get; set; }
        public double WidthFactor { get; set; }
        public double SourceWidth { get; set; }
        public int Parent { get; set; }
        public List<int> Cells { get; set; } = new List<int>();
    }

    public class MapCulture
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Code { get; set; }
        public string Color { get; set; }
        public int CenterCell { get; set; }
        public int BaseNameId { get; set; }
        public CultureType Type { get; set; }
        public double Expansionism { get; set; }
        public string Shield { get; set; }

        // Growth/Stats tracking
        public int CellCount { get; set; }
        public double TotalArea { get; set; }
        public double RuralPopulation { get; set; }
        public double UrbanPopulation { get; set; }
    }

    public class MapBurg
    {
        public int Id { get; set; }
        public int Cell { get; set; }
        public MapPoint Position { get; set; }
        public int StateId { get; set; }
        public int CultureId { get; set; }
        public int PortId { get; set; }
        public string Name { get; set; }
        public ushort FeatureId { get; set; }
        public bool IsCapital { get; set; }
    }

    public class MapState
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Color { get; set; }

        // Expansion logic properties
        public double Expansionism { get; set; }
        public int CenterCell { get; set; }
        public int CapitalId { get; set; }

        // Demographic/Culture properties
        public int CultureId { get; set; }
        public CultureType Type { get; set; } // e.g., "Naval", "Nomadic", "Highland"

        // Visuals
        public MapCoA CoA { get; set; }

        // Geographic properties (calculated in later steps)
        public double[] Pole { get; set; } // [x, y] coordinates for label placement
        public List<int> Neighbors { get; set; } = new List<int>();

        // Stats
        public int Area { get; set; }
        public int Population { get; set; }
        public int BurgsCount { get; set; }
    }

    public class MapCoA
    {
        public string Shield { get; set; }
        public CultureType Type { get; set; }
    }

    public readonly struct PointFlux
    {
        public readonly double X, Y, Flux;
        public PointFlux(double x, double y, double f) { X = x; Y = y; Flux = f; }
        public override string ToString() => $"[{X}, {Y}][{Flux}]";
    }

    public readonly struct MapPoint
    {
        public readonly double X, Y;
        public MapPoint(double x, double y) => (X, Y) = (x, y);
        public override string ToString() => $"[{X}, {Y}]";
    }
}