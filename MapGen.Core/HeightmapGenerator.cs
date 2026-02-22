using System;
using System.Collections.Generic;
using System.Text;

namespace MapGen.Core
{
    public enum HeightmapTemplate
    {
        HighIsland,
        Volcano,
        MountainRange, // New one for testing later
        Test
    }

    public enum HeightmapTool
    {
        Hill,
        Pit,
        Range,
        Trough,
        Strait,
        Mask,
        Invert,
        Add,
        Multiply,
        Smooth
    }

    public enum HeightmapRangeType
    {
        All, Land, Custom
    }

    public static class HeightmapGenerator
    {
        private const double BlobPower = 0.98;
        private const double LinePower = 0.81;

        public static void Generate(MapData data, HeightmapTemplate template, IRandom rng)
        {
            data.H = new byte[data.PointsCount];

            // This matches the "Default" sequence you found in JS
            string recipe = template switch
            {
                HeightmapTemplate.HighIsland =>
                    "Hill 1 90-100 65-75 47-53\n" +
                    "Add 7 all\n" +
                    "Hill 5-6 20-30 25-55 45-55\n" +
                    "Range 1 40-50 45-55 45-55\n" +
                    "Multiply 0.8 land\n" +
                    "Mask 3\n" +
                    "Smooth 2\n" +
                    "Trough 2-3 20-30 20-30 20-30\n" +
                    "Trough 2-3 20-30 60-80 70-80\n" +
                    "Hill 1 10-15 60-60 50-50\n" +
                    "Hill 1.5 13-16 15-20 20-75\n" +
                    "Range 1.5 30-40 15-85 30-40\n" +
                    "Range 1.5 30-40 15-85 60-70\n" +
                    "Pit 3-5 10-30 15-85 20-80",
                HeightmapTemplate.Test =>
                    "Hill 1 90-100 44-56 40-60",
                _ => ""
            };

            foreach (var line in recipe.Split('\n'))
            {
                var args = line.Trim().Split(' ');
                if (Enum.TryParse<HeightmapTool>(args[0], true, out var tool))
                    ApplyTool(data, tool, args, rng);
            }
        }

        private static void ApplyTool(MapData data, HeightmapTool tool, string[] args, IRandom rng)
        {
            switch (tool)
            {
                case HeightmapTool.Hill: AddHill(data, rng, args[1], args[2], args[3], args[4]); break;
                case HeightmapTool.Pit: AddPit(data, rng, args[1], args[2], args[3], args[4]); break;
                case HeightmapTool.Range: AddRange(data, rng, args[1], args[2], args[3], args[4]); break;
                case HeightmapTool.Trough: AddTrough(data, rng, args[1], args[2], args[3], args[4]); break;
                case HeightmapTool.Strait: AddStrait(data, rng, args[1], args.Length > 2 ? args[2] : "vertical"); break;
                case HeightmapTool.Add: Modify(data, ToolRange.Parse(args[2]), double.Parse(args[1]), 1); break;
                case HeightmapTool.Multiply: Modify(data, ToolRange.Parse(args[2]), 0, double.Parse(args[1])); break;
                case HeightmapTool.Smooth: Smooth(data, double.Parse(args[1])); break;
                case HeightmapTool.Mask: Mask(data, double.Parse(args[1])); break;
            }
        }

        private static void AddHill(MapData data, IRandom rng, string countStr, string heightStr, string rX, string rY)
        {
            int count = GetNumberInRange(countStr, rng);
            for (int i = 0; i < count; i++)
            {
                double h = GetNumberInRange(heightStr, rng);
                int start = FindNearestCell(data, GetPointInRange(rX, data.Width, rng), GetPointInRange(rY, data.Height, rng));

                var change = new double[data.H.Length];
                change[start] = h;
                var queue = new Queue<int>();
                queue.Enqueue(start);

                while (queue.Count > 0)
                {
                    int q = queue.Dequeue();
                    foreach (int n in data.Cells.C[q])
                    {
                        if (change[n] > 0) continue;
                        double decay = Math.Pow(change[q], BlobPower) * (rng.Next() * 0.2 + 0.9);
                        if (decay > 1) { change[n] = decay; queue.Enqueue(n); }
                    }
                }
                for (int j = 0; j < data.H.Length; j++) data.H[j] = Lim(data.H[j] + change[j]);
            }
        }

        private static void AddRange(MapData data, IRandom rng, string countStr, string heightStr, string rX, string rY)
        {
            int count = GetNumberInRange(countStr, rng);
            for (int i = 0; i < count; i++)
            {
                double h = GetNumberInRange(heightStr, rng);
                int start = FindNearestCell(data, GetPointInRange(rX, data.Width, rng), GetPointInRange(rY, data.Height, rng));

                double endX = rng.Next() * data.Width * 0.8 + data.Width * 0.1;
                double endY = rng.Next() * data.Height * 0.7 + data.Height * 0.15;
                int end = FindNearestCell(data, endX, endY);

                var ridge = GetRangePath(data, start, end, rng);
                ExpandRange(data, ridge, h, rng, LinePower);
            }
        }

        private static List<int> GetRangePath(MapData data, int cur, int end, IRandom rng)
        {
            var path = new List<int> { cur };
            var used = new HashSet<int> { cur };
            while (cur != end)
            {
                int best = -1; double minD = double.MaxValue;
                foreach (int n in data.Cells.C[cur])
                {
                    if (used.Contains(n)) continue;
                    double d = Math.Pow(data.X[end] - data.X[n], 2) + Math.Pow(data.Y[end] - data.Y[n], 2);
                    if (rng.Next() > 0.85) d /= 2;
                    if (d < minD) { minD = d; best = n; }
                }
                if (best == -1) break;
                cur = best; path.Add(cur); used.Add(cur);
            }
            return path;
        }

        private static void ExpandRange(MapData data, List<int> range, double h, IRandom rng, double power)
        {
            var used = new HashSet<int>(range);
            var queue = new Queue<int>(range);
            while (queue.Count > 0)
            {
                int levelSize = queue.Count;
                // Changed 'i' to 'j'
                for (int j = 0; j < levelSize; j++)
                {
                    int q = queue.Dequeue();
                    data.H[q] = Lim(data.H[q] + h * (rng.Next() * 0.3 + 0.85));
                    foreach (int n in data.Cells.C[q])
                        if (used.Add(n)) queue.Enqueue(n);
                }
                h = Math.Pow(h, power) - 1;
                if (h < 2) break;
            }
        }

        private static void AddPit(MapData data, IRandom rng, string countStr, string heightStr, string rX, string rY)
        {
            int count = GetNumberInRange(countStr, rng);
            for (int i = 0; i < count; i++)
            {
                double h = GetNumberInRange(heightStr, rng);
                int start = FindNearestCell(data, GetPointInRange(rX, data.Width, rng), GetPointInRange(rY, data.Height, rng));
                var queue = new Queue<int>(); queue.Enqueue(start);
                var used = new HashSet<int> { start };
                while (queue.Count > 0)
                {
                    int q = queue.Dequeue();
                    h = Math.Pow(h, BlobPower) * (rng.Next() * 0.2 + 0.9);
                    if (h < 1) break;
                    foreach (int n in data.Cells.C[q])
                        if (used.Add(n)) { data.H[n] = Lim(data.H[n] - h * (rng.Next() * 0.2 + 0.9)); queue.Enqueue(n); }
                }
            }
        }

        private static void AddTrough(MapData data, IRandom rng, string countStr, string heightStr, string rX, string rY)
        {
            int count = GetNumberInRange(countStr, rng);
            for (int i = 0; i < count; i++)
            {
                double h = GetNumberInRange(heightStr, rng);
                int start = FindNearestCell(data, GetPointInRange(rX, data.Width, rng), GetPointInRange(rY, data.Height, rng));
                int end = FindNearestCell(data, rng.Next() * data.Width, rng.Next() * data.Height);
                var ridge = GetRangePath(data, start, end, rng);

                var used = new HashSet<int>(ridge);
                var queue = new Queue<int>(ridge);
                while (queue.Count > 0 && h > 2)
                {
                    int levelSize = queue.Count;
                    for (int j = 0; j < levelSize; j++)
                    {
                        int q = queue.Dequeue();
                        data.H[q] = Lim(data.H[q] - h * (rng.Next() * 0.3 + 0.85));
                        foreach (int n in data.Cells.C[q]) if (used.Add(n)) queue.Enqueue(n);
                    }
                    h = Math.Pow(h, LinePower) - 1;
                }
            }
        }

        private static void AddStrait(MapData data, IRandom rng, string widthStr, string direction = "vertical")
        {
            int width = GetNumberInRange(widthStr, rng);
            bool vert = direction.Equals("vertical", StringComparison.OrdinalIgnoreCase);

            // Pick start/end points on opposite edges
            double startX = vert ? rng.Next() * data.Width * 0.4 + data.Width * 0.3 : 5;
            double startY = vert ? 5 : rng.Next() * data.Height * 0.4 + data.Height * 0.3;

            double endX = vert ? data.Width - startX : data.Width - 5;
            double endY = vert ? data.Height - 5 : data.Height - startY;

            int startCell = FindNearestCell(data, startX, startY);
            int endCell = FindNearestCell(data, endX, endY);

            var path = GetRangePath(data, startCell, endCell, rng);
            var used = new HashSet<int>();
            var query = new List<int>();

            double step = 0.1 / width;
            while (width > 0)
            {
                double exp = 0.9 - step * width;
                foreach (int r in path)
                {
                    foreach (int e in data.Cells.C[r])
                    {
                        if (used.Add(e))
                        {
                            query.Add(e);
                            // JS: heights[e] **= exp; if (heights[e] > 100) heights[e] = 5;
                            double h = Math.Pow(data.H[e], exp);
                            data.H[e] = (h > 100) ? (byte)5 : Lim(h);
                        }
                    }
                }
                path = new List<int>(query);
                query.Clear();
                width--;
            }
        }

        private static void Modify(MapData data, ToolRange range, double add, double mult)
        {
            for (int i = 0; i < data.PointsCount; i++)
            {
                if (data.H[i] < range.Min || data.H[i] > range.Max) continue;
                double h = data.H[i];
                if (add != 0) h = (range.Type == HeightmapRangeType.Land) ? Math.Max(h + add, 20) : h + add;
                if (mult != 1) h = (range.Type == HeightmapRangeType.Land) ? (h - 20) * mult + 20 : h * mult;
                data.H[i] = Lim(h);
            }
        }

        private static void Smooth(MapData data, double fr)
        {
            byte[] result = new byte[data.H.Length];
            for (int i = 0; i < data.H.Length; i++)
            {
                double sum = data.H[i];
                foreach (var n in data.Cells.C[i]) sum += data.H[n];
                result[i] = Lim((data.H[i] * (fr - 1) + (sum / (data.Cells.C[i].Count + 1))) / fr);
            }
            data.H = result;
        }

        private static void Mask(MapData data, double power)
        {
            double fr = power != 0 ? Math.Abs(power) : 1;
            for (int i = 0; i < data.PointsCount; i++)
            {
                double nx = (2 * data.X[i]) / data.Width - 1;
                double ny = (2 * data.Y[i]) / data.Height - 1;
                double dist = (1 - nx * nx) * (1 - ny * ny);
                if (power < 0) dist = 1 - dist;
                data.H[i] = Lim((data.H[i] * (fr - 1) + (data.H[i] * dist)) / fr);
            }
        }

        private static byte Lim(double val) => (byte)Math.Clamp(val, 0, 100);

        private static int GetNumberInRange(string range, IRandom rng)
        {
            if (!range.Contains('-'))
            {
                // Handle fractional numbers like "1.5"
                double val = double.Parse(range, System.Globalization.CultureInfo.InvariantCulture);
                int floor = (int)Math.Floor(val);
                return rng.Next() < (val - floor) ? floor + 1 : floor;
            }

            var p = range.Split('-');
            double min = double.Parse(p[0], System.Globalization.CultureInfo.InvariantCulture);
            double max = double.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture);
            // Standard Azgaar integer range logic: floor(rand * (max - min + 1) + min)
            return (int)Math.Floor(rng.Next() * (max - min + 1) + min);
        }

        private static double GetPointInRange(string range, int length, IRandom rng)
        {
            var p = range.Split('-');
            double min = double.Parse(p[0]) / 100.0, max = p.Length > 1 ? double.Parse(p[1]) / 100.0 : min;
            return (min + (max - min) * rng.Next()) * length;
        }

        private static int FindNearestCell(MapData data, double x, double y)
        {
            int best = 0; double minD = double.MaxValue;
            for (int i = 0; i < data.PointsCount; i++)
            {
                double d = Math.Pow(x - data.X[i], 2) + Math.Pow(y - data.Y[i], 2);
                if (d < minD) { minD = d; best = i; }
            }
            return best;
        }
    }

    public struct ToolRange
    {
        public HeightmapRangeType Type;
        public int Min;
        public int Max;

        public static ToolRange Parse(string range)
        {
            if (range.Equals("all", StringComparison.OrdinalIgnoreCase))
                return new ToolRange { Type = HeightmapRangeType.All, Min = 0, Max = 100 };
            if (range.Equals("land", StringComparison.OrdinalIgnoreCase))
                return new ToolRange { Type = HeightmapRangeType.Land, Min = 20, Max = 100 };

            var parts = range.Split('-');
            int min = int.Parse(parts[0]);
            int max = parts.Length > 1 ? int.Parse(parts[1]) : min;
            return new ToolRange { Type = HeightmapRangeType.Custom, Min = min, Max = max };
        }
    }
}
