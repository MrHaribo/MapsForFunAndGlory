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

        public double[] BoundaryX { get; set; }
        public double[] BoundaryY { get; set; }

        // Raw Coordinates (The "Grid")
        public double[] X { get; set; }
        public double[] Y { get; set; }

        public int CellsCountX { get; set; }
        public int CellsCountY { get; set; }
        public int CellsCount { get; set; }

        // Voronoi Data
        public CellData Cells { get; set; }
        public VertexData Vertices { get; set; }

        // The "Cells" (The simulation data / "Pack")
        public byte[] H { get; set; } // Heights


        public MapData(int count, int width, int height)
        {
            PointsCount = count;
            Width = width;
            Height = height;
        }
    }

    public class CellData
    {
        public List<int>[] V { get; set; } // Cell vertices
        public List<int>[] C { get; set; } // Neighbor cells
        public byte[] B { get; set; }      // Border flag
    }

    public class VertexData
    {
        public IPoint[] P { get; set; }    // Vertex coordinates
        public List<int>[] V { get; set; } // Neighbor vertices
        public List<int>[] C { get; set; } // Adjacent cells
    }
}