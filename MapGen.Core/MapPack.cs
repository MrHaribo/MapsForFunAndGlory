using System;
using System.Collections.Generic;
using System.Text;

namespace MapGen.Core
{
    public class MapPack
    {
        public MapCell[] Cells { get; set; }
        public MapVertex[] Vertices { get; set; }
        public MapPoint[] Points { get; set; }

        // The JS-equivalent of pack.cells.q.find(x, y)
        // Returns the Index of the closest cell
        public Func<double, double, double, int> FindCell { get; set; }

        public MapOptions Options { get; set; }
    }
}
