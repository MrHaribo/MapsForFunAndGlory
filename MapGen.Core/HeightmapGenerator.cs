using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace MapGen.Core
{
    public enum HeightmapTemplate { HighIsland, Volcano, MountainRange, Archipelago, Test }
    public enum HeightmapTool { Hill, Pit, Range, Trough, Strait, Mask, Invert, Add, Multiply, Smooth }
    public enum HeightmapSelection { All, Land, Water }

    public static class HeightmapGenerator
    {
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

            var lines = recipe.Split(new[] { "\n", "\r\n", ";" }, StringSplitOptions.RemoveEmptyEntries);
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
                case HeightmapTool.Hill:
                    AddHill(data, rng, args[1], args[2], args[3], args[4]); break;

                case HeightmapTool.Pit:
                    AddPit(data, rng, args[1], args[2], args[3], args[4]); break;

                case HeightmapTool.Range: 
                    AddRange(data, rng, args[1], args[2], args[3], args[4]); break;

                case HeightmapTool.Trough: 
                    AddTrough(data, rng, args[1], args[2], args[3], args[4]); break;

                case HeightmapTool.Invert:
                    Invert(data, args.Length > 2 ? args[2] : "both");break;

                case HeightmapTool.Add:
                    Modify(data, args.Length > 2 ? args[2] : "all", double.Parse(args[1], CultureInfo.InvariantCulture), 1.0); break;

                case HeightmapTool.Multiply:
                    Modify(data, args.Length > 2 ? args[2] : "all", 0.0, double.Parse(args[1], CultureInfo.InvariantCulture)); break;

                case HeightmapTool.Strait:
                    AddStrait(data, rng, args[1], args.Length > 2 ? args[2] : "vertical", HeightmapSelection.Land); break;

                case HeightmapTool.Smooth:
                    Smooth(data, double.Parse(args[1], CultureInfo.InvariantCulture)); break;

                case HeightmapTool.Mask: 
                    Mask(data, double.Parse(args[1])); break;
            }
        }

        private static void AddHill(MapData data, IRandom rng, string countArg, string heightArg, string rangeX, string rangeY)
        {
            int count = Probability.GetNumberInRange(rng, countArg);
            while (count > 0)
            {
                AddOneHill(data, rng, rangeX, rangeY, heightArg);
                count--;
            }
        }

        private static void AddOneHill(MapData data, IRandom rng, string rangeX, string rangeY, string heightArg)
        {
            // 1. Setup
            double blobPower = GetBlobPower(data.PointsCount);
            double[] change = new double[data.Cells.Length];
            int h = Math.Clamp(Probability.GetNumberInRange(rng, heightArg), 0, 100);

            int start = -1;
            int limit = 0;

            // 2. Find Start Point
            do
            {
                double x = GetPointInRange(rangeX, data.Width, rng);
                double y = GetPointInRange(rangeY, data.Height, rng);
                start = FindGridCell(data, x, y);
                limit++;
            } while (data.Cells[start].H + h > 90 && limit < 50);

            // 3. BFS Hill Generation
            change[start] = h;
            Queue<int> queue = new Queue<int>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                int q = queue.Dequeue();
                foreach (int c in data.Cells[q].C)
                {
                    if (c == -1 || change[c] > 0) continue;

                    double r = rng.Next();
                    double parentHeight = change[q];
                    double exponentResult = Math.Pow(parentHeight, blobPower);
                    double finalValue = exponentResult * (r * 0.2 + 0.9);

                    // Correct: BFS values are floored
                    change[c] = Math.Floor(Math.Clamp(finalValue, 0, 255));

                    if (change[c] > 1)
                    {
                        queue.Enqueue(c);
                    }
                }
            }

            // 4. Final Merge (CRITICAL CHANGE)
            for (int i = 0; i < data.Cells.Length; i++)
            {
                if (change[i] <= 0) continue;

                // JS: heights[i] = lim(heights[i] + change[i])
                // Assignment to Uint8Array always floors in JS.
                double mergedHeight = Math.Floor(data.Cells[i].H + change[i]);
                data.Cells[i].H = (byte)Math.Clamp(mergedHeight, 0, 100);
            }
        }

        private static void AddPit(MapData data, IRandom rng, string countArg, string heightArg, string rangeX, string rangeY)
        {
            int count = Probability.GetNumberInRange(rng, countArg);
            while (count > 0)
            {
                AddOnePit(data, rng, rangeX, rangeY, heightArg);
                count--;
            }
        }

        private static void AddOnePit(MapData data, IRandom rng, string rangeX, string rangeY, string heightArg)
        {
            byte[] used = new byte[data.Cells.Length];
            double blobPower = GetBlobPower(data.PointsCount);
            int limit = 0, start = -1;
            double h = Math.Clamp(Probability.GetNumberInRange(rng, heightArg), 0, 100);

            // 1. Find Start Point
            do
            {
                double x = GetPointInRange(rangeX, data.Width, rng);
                double y = GetPointInRange(rangeY, data.Height, rng);
                start = FindGridCell(data, x, y);
                limit++;
            } while (data.Cells[start].H < 20 && limit < 50);

            // 2. BFS Mutation Loop
            Queue<int> queue = new Queue<int>();
            queue.Enqueue(start);
            // JS parity: do NOT set used[start] = 1 here if you want the "pimple/flat" center.
            // However, if the JS dump shows the center is exactly 50, uncomment the line below:
            // used[start] = 1; 

            while (queue.Count > 0)
            {
                int q = queue.Dequeue();

                // Mutate global intensity h (consumes 1 RNG)
                h = Math.Pow(h, blobPower) * (rng.Next() * 0.2 + 0.9);
                if (h < 1) return;

                foreach (int c in data.Cells[q].C)
                {
                    // Skip border cells and already processed cells
                    if (c == -1 || used[c] == 1) continue;

                    // Calculate reduction for this neighbor (consumes 1 RNG)
                    double reduction = h * (rng.Next() * 0.2 + 0.9);

                    // Apply reduction with Floor to match JS Uint8Array behavior
                    double currentH = data.Cells[c].H;
                    double newH = Math.Floor(currentH - reduction);
                    data.Cells[c].H = (byte)Math.Clamp(newH, 0, 100);

                    used[c] = 1;
                    queue.Enqueue(c);
                }
            }
        }

        private static void AddRange(MapData data, IRandom rng, string countStr, string heightStr, string rX, string rY)
        {
            double linePower = GetLinePower(data.PointsCount);
            int count = GetNumberInRange(countStr, rng);
            for (int i = 0; i < count; i++)
            {
                double h = GetNumberInRange(heightStr, rng);
                int start = FindNearestCell(data, GetPointInRange(rX, data.Width, rng), GetPointInRange(rY, data.Height, rng));

                double endX = rng.Next() * data.Width * 0.8 + data.Width * 0.1;
                double endY = rng.Next() * data.Height * 0.7 + data.Height * 0.15;
                int end = FindNearestCell(data, endX, endY);

                var ridge = GetRangePath(data, start, end, rng);
                ExpandRange(data, ridge, h, rng, linePower);
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

        private static void AddTrough(MapData data, IRandom rng, string countStr, string heightStr, string rX, string rY)
        {
            double linePower = GetLinePower(data.PointsCount);
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
                    h = Math.Pow(h, linePower) - 1;
                }
            }
        }

        private static void AddStrait(MapData data, IRandom rng, string widthArg, string direction, HeightmapSelection selection)
        {
            // 1. Get width using the new GetPointInRange helper
            // In Azgaar, width is relative to the total map width
            double width = GetPointInRange(widthArg, data.Width, rng);

            // 2. Determine start and end points based on direction
            // Logic for picking coordinates on the edges of the map
            (double x1, double y1, double x2, double y2) = GetStraitPath(data, direction, rng);

            // 3. Find cells along the line
            // This typically uses a line-drawing algorithm or distance-to-segment check
            var affectedCells = new List<int>();// FindCellsNearLine(data, x1, y1, x2, y2, width);

            foreach (var cellIdx in affectedCells)
            {
                var cell = data.Cells[cellIdx];
                bool isLand = cell.H >= 20;

                // 4. Apply the Selection Filter
                if (selection == HeightmapSelection.Land && !isLand) continue;
                if (selection == HeightmapSelection.Water && isLand) continue;

                // 5. Carve the strait (Setting to 5 = shallow water)
                // We use Lim() to ensure it stays within 0-100 range
                cell.H = Lim(5);
            }
        }

        // Helper to determine the strait's vector based on the direction string
        private static (double, double, double, double) GetStraitPath(MapData data, string direction, IRandom rng)
        {
            if (direction == "vertical")
            {
                double x = rng.Next(0.0, data.Width);
                return (x, 0, x, data.Height);
            }
            else // horizontal
            {
                double y = rng.Next(0.0, data.Height);
                return (0, y, data.Width, y);
            }
        }

        private static void Invert(MapData data, string axes = "both")
        {
            bool invertX = axes != "y";
            bool invertY = axes != "x";

            int cellsX = data.CellsCountX;
            int cellsY = data.CellsCountY;

            // 1. Snapshot the current heights into a flat array
            byte[] oldHeights = new byte[data.Cells.Length];
            for (int i = 0; i < data.Cells.Length; i++)
            {
                oldHeights[i] = data.Cells[i].H;
            }

            // 2. Map the snapped heights back to the cells using inverted coordinates
            for (int i = 0; i < data.Cells.Length; i++)
            {
                int x = i % cellsX;
                int y = i / cellsX;

                int nx = invertX ? cellsX - x - 1 : x;
                int ny = invertY ? cellsY - y - 1 : y;

                int invertedI = nx + ny * cellsX;

                // Parity Check: JS returns heights[invertedI]
                data.Cells[i].H = oldHeights[invertedI];
            }
        }

        private static void Modify(MapData data, string range, double add, double mult, double power = 0)
        {
            // JS: const min = range === "land" ? 20 : range === "all" ? 0 : +range.split("-")[0];
            // JS: const max = range === "land" || range === "all" ? 100 : +range.split("-")[1];
            var split = range.Split('-');
            double min = range == "land" ? 20 : range == "all" ? 0 : double.Parse(split[0], CultureInfo.InvariantCulture);
            double max = (range == "land" || range == "all") ? 100 : (split.Length > 1 ? double.Parse(split[1], CultureInfo.InvariantCulture) : min);

            // JS: const isLand = min === 20;
            bool isLand = (min == 20);

            foreach (var cell in data.Cells)
            {
                double h = cell.H;

                // JS: if (h < min || h > max) return h;
                if (h < min || h > max) continue;

                // JS: if (add) h = isLand ? Math.max(h + add, 20) : h + add;
                if (add != 0)
                    h = isLand ? Math.Max(h + add, 20) : h + add;

                // JS: if (mult !== 1) h = isLand ? (h - 20) * mult + 20 : h * mult;
                if (mult != 1)
                    h = isLand ? (h - 20) * mult + 20 : h * mult;

                // JS: if (power) h = isLand ? (h - 20) ** power + 20 : h ** power;
                if (power != 0)
                    h = isLand ? Math.Pow(h - 20, power) + 20 : Math.Pow(h, power);

                cell.H = Lim(h);
            }
        }

        private static void Smooth(MapData data, double fr = 2, double add = 0)
        {
            byte[] result = new byte[data.Cells.Length];

            for (int i = 0; i < data.Cells.Length; i++)
            {
                // Calculate the mean of self + neighbors
                double sum = data.Cells[i].H;
                foreach (int n in data.Cells[i].C)
                {
                    if (n == -1) continue;
                    sum += data.Cells[n].H;
                }

                double mean = sum / (data.Cells[i].C.Count + 1);
                double finalValue;

                if (Math.Abs(fr - 1.0) < 0.0001)
                {
                    finalValue = mean + add;
                }
                else
                {
                    // JS: lim((h * (fr - 1) + d3.mean(a) + add) / fr)
                    finalValue = (data.Cells[i].H * (fr - 1) + mean + add) / fr;
                }

                // Apply Lim and Floor to match JS Uint8Array behavior
                result[i] = (byte)Math.Clamp(Math.Floor(finalValue), 0, 100);
            }

            // Apply the buffer back to the map
            for (int i = 0; i < data.Cells.Length; i++)
            {
                data.Cells[i].H = result[i];
            }
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



        private static int FindGridCell(MapData data, double x, double y)
        {
            // Math.Min ensures we don't go out of bounds on the right/bottom edges
            int row = (int)Math.Floor(Math.Min(y / data.Spacing, data.CellsCountY - 1));
            int col = (int)Math.Floor(Math.Min(x / data.Spacing, data.CellsCountX - 1));

            return (row * data.CellsCountX) + col;
        }

        private static int FindNearestCell(MapData data, double x, double y)
        {
            int nearest = 0;
            double minDist = double.MaxValue;

            for (int i = 0; i < data.Points.Length; i++)
            {
                double dx = data.Points[i].X - x;
                double dy = data.Points[i].Y - y;
                // Do not use Math.Sqrt for comparison to avoid rounding errors
                double dist = dx * dx + dy * dy;

                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = i;
                }
            }
            return nearest;
        }

        public static double GetPointInRange(string range, int length, IRandom rng)
        {
            if (string.IsNullOrWhiteSpace(range))
            {
                throw new ArgumentException("Range should be a string and not null");
            }

            string[] parts = range.Split('-');

            if (!double.TryParse(parts[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double minPercent))
            {
                minPercent = 0;
            }
            double min = minPercent / 100.0;

            double max;
            if (parts.Length > 1 && double.TryParse(parts[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double maxPercent))
            {
                max = maxPercent / 100.0;
            }
            else
            {
                max = min;
            }

            // Now using the extension method: rng.Next(double min, double max)
            return rng.Next(min * length, max * length);
        }

        public static double GetBlobPower(int cells) => cells switch
        {
            1000 => 0.93,
            2000 => 0.95,
            5000 => 0.97,
            10000 => 0.98,
            20000 => 0.99,
            30000 => 0.991,
            40000 => 0.993,
            50000 => 0.994,
            60000 => 0.995,
            70000 => 0.9955,
            80000 => 0.996,
            90000 => 0.9964,
            100000 => 0.9973,
            _ => throw new ArgumentException($"Invalid cell count: {cells}. Power map requires a standard Azgaar point tier.")
        };

        public static double GetLinePower(int cells) => cells switch
        {
            1000 => 0.75,
            2000 => 0.77,
            5000 => 0.79,
            10000 => 0.81,
            20000 => 0.82,
            30000 => 0.83,
            40000 => 0.84,
            50000 => 0.86,
            60000 => 0.87,
            70000 => 0.88,
            80000 => 0.91,
            90000 => 0.92,
            100000 => 0.93,
            _ => throw new ArgumentException($"Invalid cell count: {cells}. Power map requires a standard Azgaar point tier.")
        };
    }
}