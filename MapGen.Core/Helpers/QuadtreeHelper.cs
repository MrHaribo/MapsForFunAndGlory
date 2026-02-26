using D3Sharp.QuadTree;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MapGen.Core.Helpers
{
    public class QuadPoint : IQuadData
    {
        public double X { get; set; }
        public double Y { get; set; }
        public int DataIndex { get; set; } // The 'i' from the pack points list
    }

    // Standard node for the data above
    public class QuadPointNode : QuadNode<QuadPoint> { }

    public static class QuadtreeHelper
    {
        public static (ClosestCell Find, ClosestCellInRange FindInRange) CreateLookupDelegates(List<MapPoint> points)
        {
            var quadDatas = points.Select((p, i) => new QuadPoint
            {
                X = p.X,
                Y = p.Y,
                DataIndex = i
            }).ToList();

            var tree = new QuadTree<QuadPoint, QuadPointNode>(quadDatas);

            // 1. Closest Cell (Matches JS find(x, y) with default Infinity)
            int find(double x, double y)
            {
                var foundNode = tree.Find(x, y, double.PositiveInfinity);
                return foundNode != null ? foundNode.DataIndex : -1;
            }

            // 2. Closest Cell within a specific radius
            int findInRange(double x, double y, double radius)
            {
                var foundNode = tree.Find(x, y, radius);
                return foundNode != null ? foundNode.DataIndex : -1;
            }

            return (find, findInRange);
        }
    }
}
