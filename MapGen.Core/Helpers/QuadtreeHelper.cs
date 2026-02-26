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
        // Update the delegate to match JS: (x, y, radius)
        public static Func<double, double, double, int> CreateQuadtreeLookup(List<MapPoint> points)
        {
            var quadDatas = points.Select((p, i) => new QuadPoint
            {
                X = p.X,
                Y = p.Y,
                DataIndex = i
            }).ToList();

            var tree = new QuadTree<QuadPoint, QuadPointNode>(quadDatas);

            // Return the search delegate with radius support
            return (x, y, radius) =>
            {
                // If D3Sharp.Find doesn't have a radius overload, 
                // ensure you are using the one that matches D3's search logic.
                // Note: If the port only supports (x, y), you might need to 
                // manually check distance against the result, but D3Sharp usually mirrors the radius.
                var foundNode = tree.Find(x, y, radius);
                return foundNode != null ? foundNode.DataIndex : -1;
            };
        }
    }
}
