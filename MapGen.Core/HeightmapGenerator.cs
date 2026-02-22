using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace MapGen.Core
{
    public enum HeightmapTemplate { HighIsland, Volcano, MountainRange, Archipelago, Test }
    public enum HeightmapTool { Hill, Pit, Range, Trough, Strait, Mask, Invert, Add, Multiply, Smooth }
    public enum HeightmapRangeType { All, Land, Water, Range }

    public static class HeightmapGenerator
    {
        private const double BlobPower = 0.98;
        private const double LinePower = 0.81;

        // Overload 1: Standard Enum-based generation
        public static void Generate(MapData data, HeightmapTemplate template, IRandom rng)
        {
            string recipe = HeightmapTemplates.GetRecipe(template);
            Generate(data, recipe, rng);
        }

        // Overload 2: Direct string-based generation (for Regression/Isolated tests)
        public static void Generate(MapData data, string recipe, IRandom rng)
        {
            // Reset heights to 0 before applying tools
            foreach (var cell in data.Cells) cell.H = 0;

            var lines = recipe.Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                // Trim() handles the leading whitespace from the JS template blocks
                var args = line.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (args.Length > 0 && Enum.TryParse<HeightmapTool>(args[0], true, out var tool))
                {
                    ApplyTool(data, tool, args, rng);
                }
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

        private static void AddHill(MapData data, IRandom rng, string countArg, string heightArg, string rangeX, string rangeY)
        {
            int count = ToolRange.Parse(countArg).GetNumber(rng);
            double blobPower = 0.98;

            while (count > 0)
            {
                byte[] change = new byte[data.Cells.Length];
                int limit = 0;
                int start;
                int h = Math.Clamp(ToolRange.Parse(heightArg).GetNumber(rng), 0, 100);

                do
                {
                    // Use the new GetPoint method to handle the 0-100 to pixel conversion
                    double x = ToolRange.Parse(rangeX).GetPoint(data.Width, rng);
                    double y = ToolRange.Parse(rangeY).GetPoint(data.Height, rng);

                    // Renamed to match your likely method name
                    start = FindNearestCell(data, x, y);
                    limit++;
                } while (data.Cells[start].H + h > 90 && limit < 50);

                change[start] = (byte)h;
                var queue = new Queue<int>();
                queue.Enqueue(start);

                while (queue.Count > 0)
                {
                    int q = queue.Dequeue();
                    foreach (int n in data.Cells[q].C)
                    {
                        if (change[n] > 0) continue;

                        // Precision: Using NextDouble to match Math.random()
                        double newValue = Math.Pow(change[q], blobPower) * (rng.Next() * 0.2 + 0.9);
                        change[n] = (byte)newValue;

                        if (change[n] > 1) queue.Enqueue(n);
                    }
                }

                for (int i = 0; i < data.Cells.Length; i++)
                {
                    data.Cells[i].H = (byte)Math.Clamp(data.Cells[i].H + change[i], 0, 100);
                }
                count--;
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

        private static void ExpandRange(MapData data, List<int> range, double h, IRandom rng, double power)
        {
            var used = new HashSet<int>(range);
            var queue = new Queue<int>(range);
            while (queue.Count > 0)
            {
                int levelSize = queue.Count;
                for (int j = 0; j < levelSize; j++)
                {
                    int q = queue.Dequeue();
                    data.Cells[q].H = Lim(data.Cells[q].H + h * (rng.Next() * 0.3 + 0.85));
                    foreach (int n in data.Cells[q].C)
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
                    foreach (int n in data.Cells[q].C)
                    {
                        if (used.Add(n))
                        {
                            data.Cells[n].H = Lim(data.Cells[n].H - h * (rng.Next() * 0.2 + 0.9));
                            queue.Enqueue(n);
                        }
                    }
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
                        data.Cells[q].H = Lim(data.Cells[q].H - h * (rng.Next() * 0.3 + 0.85));
                        foreach (int n in data.Cells[q].C) if (used.Add(n)) queue.Enqueue(n);
                    }
                    h = Math.Pow(h, LinePower) - 1;
                }
            }
        }

        private static void AddStrait(MapData data, IRandom rng, string widthStr, string direction = "vertical")
        {
            int width = GetNumberInRange(widthStr, rng);
            bool vert = direction.Equals("vertical", StringComparison.OrdinalIgnoreCase);

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
                    foreach (int e in data.Cells[r].C)
                    {
                        if (used.Add(e))
                        {
                            query.Add(e);
                            double h = Math.Pow(data.Cells[e].H, exp);
                            data.Cells[e].H = (h > 100) ? (byte)5 : Lim(h);
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
            foreach (var cell in data.Cells)
            {
                if (cell.H < range.Min || cell.H > range.Max) continue;
                double h = cell.H;
                if (add != 0) h = (range.Type == HeightmapRangeType.Land) ? Math.Max(h + add, 20) : h + add;
                if (mult != 1) h = (range.Type == HeightmapRangeType.Land) ? (h - 20) * mult + 20 : h * mult;
                cell.H = Lim(h);
            }
        }

        private static void Smooth(MapData data, double fr)
        {
            byte[] result = new byte[data.Cells.Length];
            for (int i = 0; i < data.Cells.Length; i++)
            {
                double sum = data.Cells[i].H;
                foreach (var n in data.Cells[i].C) sum += data.Cells[n].H;
                result[i] = Lim((data.Cells[i].H * (fr - 1) + (sum / (data.Cells[i].C.Count + 1))) / fr);
            }
            for (int i = 0; i < data.Cells.Length; i++) data.Cells[i].H = result[i];
        }

        private static void Mask(MapData data, double power)
        {
            double fr = power != 0 ? Math.Abs(power) : 1;
            for (int i = 0; i < data.Points.Length; i++)
            {
                var p = data.Points[i];
                double nx = (2 * p.X) / data.Width - 1;
                double ny = (2 * p.Y) / data.Height - 1;
                double dist = (1 - nx * nx) * (1 - ny * ny);
                if (power < 0) dist = 1 - dist;
                data.Cells[i].H = Lim((data.Cells[i].H * (fr - 1) + (data.Cells[i].H * dist)) / fr);
            }
        }

        private static byte Lim(double val) => (byte)Math.Clamp(val, 0, 100);

        // Utility and Pathing methods updated to object structure
        private static List<int> GetRangePath(MapData data, int cur, int end, IRandom rng)
        {
            var path = new List<int> { cur };
            var used = new HashSet<int> { cur };
            var target = data.Points[end];

            while (cur != end)
            {
                int best = -1;
                double minD = double.MaxValue;
                foreach (int n in data.Cells[cur].C)
                {
                    if (used.Contains(n)) continue;
                    var neighbor = data.Points[n];
                    double d = Math.Pow(target.X - neighbor.X, 2) + Math.Pow(target.Y - neighbor.Y, 2);
                    if (rng.Next() > 0.85) d /= 2;
                    if (d < minD) { minD = d; best = n; }
                }
                if (best == -1) break;
                cur = best; path.Add(cur); used.Add(cur);
            }
            return path;
        }

        private static int GetNumberInRange(string range, IRandom rng)
        {
            if (!range.Contains('-'))
            {
                double val = double.Parse(range, System.Globalization.CultureInfo.InvariantCulture);
                int floor = (int)Math.Floor(val);
                return rng.Next() < (val - floor) ? floor + 1 : floor;
            }
            var p = range.Split('-');
            double min = double.Parse(p[0], System.Globalization.CultureInfo.InvariantCulture);
            double max = double.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture);
            return (int)Math.Floor(rng.Next() * (max - min + 1) + min);
        }

        private static double GetPointInRange(string range, int length, IRandom rng)
        {
            var p = range.Split('-');
            double min = double.Parse(p[0]) / 100.0, max = p.Length > 1 ? double.Parse(p[1]) / 100.0 : min;
            return (min + (max - min) * rng.Next()) * length;
        }

        // Ensure this method exists in your Generator class
        private static int FindNearestCell(MapData data, double x, double y)
        {
            int nearest = 0;
            double minDist = double.MaxValue;

            for (int i = 0; i < data.Cells.Length; i++)
            {
                // Accessing the point from the flat point array in MapData
                var point = data.Points[i];
                double dx = point.X - x;
                double dy = point.Y - y;
                double distSq = dx * dx + dy * dy;

                if (distSq < minDist)
                {
                    minDist = distSq;
                    nearest = i;
                }
            }
            return nearest;
        }
    }

    public class ToolRange
    {
        public double Min { get; set; }
        public double Max { get; set; }
        public HeightmapRangeType Type { get; set; }

        public static ToolRange Parse(string input)
        {
            var range = new ToolRange { Type = HeightmapRangeType.Range };

            if (input == "all") { range.Type = HeightmapRangeType.All; range.Min = 0; range.Max = 100; }
            else if (input == "land") { range.Type = HeightmapRangeType.Land; range.Min = 20; range.Max = 100; }
            else if (input == "water") { range.Type = HeightmapRangeType.Water; range.Min = 0; range.Max = 19; }
            else if (input.Contains("-"))
            {
                var parts = input.Split('-');
                range.Min = double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
                range.Max = double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
            }
            else
            {
                var val = double.Parse(input, System.Globalization.CultureInfo.InvariantCulture);
                range.Min = val; range.Max = val;
            }
            return range;
        }

        // Fixed: Added the missing GetNumber method
        public int GetNumber(IRandom rng)
        {
            return (int)Math.Floor(rng.Next(Min, Max));
        }

        // Fixed: Added the missing GetPoint method
        public double GetPoint(double scale, IRandom rng)
        {
            return (rng.Next(Min, Max) / 100.0) * scale;
        }
    }
}