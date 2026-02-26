using System;
using System.Collections.Generic;
using System.Text;

namespace MapGen.Core
{
    public delegate int ClosestCell(double x, double y);
    public delegate int ClosestCellInRange(double x, double y, double radius);

    public class MapPack
    {
        public MapCell[] Cells { get; set; }
        public MapVertex[] Vertices { get; set; }
        public MapPoint[] Points { get; set; }

        // Spatial queries
        public ClosestCell FindCell { get; set; }
        public ClosestCellInRange FindCellInRange { get; set; }

        public MapOptions Options { get; set; }
    }
}
