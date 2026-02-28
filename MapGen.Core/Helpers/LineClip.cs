using System.Collections.Generic;
using System.Linq;

namespace MapGen.Core.Helpers
{
    public static class LineClip
    {
        // Bitcodes to match JS bitCode function
        private const int LEFT = 1;
        private const int RIGHT = 2;
        private const int TOP = 4;
        private const int BOTTOM = 8;

        public static List<MapPoint> PolygonClip(List<MapPoint> points, double width, double height)
        {
            double[] bbox = { 0, 0, width, height }; // [xMin, yMin, xMax, yMax]
            var t = points.Select(p => new double[] { p.X, p.Y }).ToList();

            // Match JS: for (var f = 1; f <= 8; f *= 2)
            for (int f = 1; f <= 8; f *= 2)
            {
                var result = new List<double[]>();
                if (t.Count == 0) break;

                var prev = t[t.Count - 1];
                bool prevInside = (BitCode(prev, bbox) & f) == 0;

                for (int i = 0; i < t.Count; i++)
                {
                    var current = t[i];
                    bool currInside = (BitCode(current, bbox) & f) == 0;

                    if (currInside != prevInside)
                    {
                        result.Add(Intersect(prev, current, f, bbox));
                    }

                    if (currInside)
                    {
                        result.Add(current);
                    }

                    prev = current;
                    prevInside = currInside;
                }
                t = result;
            }

            return t.Select(p => new MapPoint(p[0], p[1])).ToList();
        }

        private static int BitCode(double[] p, double[] bbox)
        {
            int code = 0;
            if (p[0] < bbox[0]) code |= LEFT;
            else if (p[0] > bbox[2]) code |= RIGHT;
            if (p[1] < bbox[1]) code |= TOP;
            else if (p[1] > bbox[3]) code |= BOTTOM;
            return code;
        }

        private static double[] Intersect(double[] t, double[] e, int edge, double[] r)
        {
            // Logic directly from JS intersect(t, e, n, r)
            if ((edge & BOTTOM) != 0) return new[] { t[0] + (e[0] - t[0]) * (r[3] - t[1]) / (e[1] - t[1]), r[3] };
            if ((edge & TOP) != 0) return new[] { t[0] + (e[0] - t[0]) * (r[1] - t[1]) / (e[1] - t[1]), r[1] };
            if ((edge & RIGHT) != 0) return new[] { r[2], t[1] + (e[1] - t[1]) * (r[2] - t[0]) / (e[0] - t[0]) };
            if ((edge & LEFT) != 0) return new[] { r[0], t[1] + (e[1] - t[1]) * (r[0] - t[0]) / (e[0] - t[0]) };
            return null;
        }
    }
}
