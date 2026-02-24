using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace MapGen.Core
{
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

                case HeightmapTool.Strait:
                    AddStrait(data, rng, args[1], args.Length > 2 ? args[2] : "vertical", HeightmapSelection.Land); break;

                case HeightmapTool.Invert:
                    Invert(data, args.Length > 2 ? args[2] : "both");break;

                case HeightmapTool.Add:
                    Modify(data, args.Length > 2 ? args[2] : "all", double.Parse(args[1], CultureInfo.InvariantCulture), 1.0); break;

                case HeightmapTool.Multiply:
                    Modify(data, args.Length > 2 ? args[2] : "all", 0.0, double.Parse(args[1], CultureInfo.InvariantCulture)); break;

                case HeightmapTool.Smooth:
                    Smooth(data, double.Parse(args[1], CultureInfo.InvariantCulture)); break;

                case HeightmapTool.Mask: 
                    Mask(data, args[1]); break;
            }
        }

        #region Hill, Pit

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

        #endregion

        #region Trough, Range, Strait

        private static void AddRange(MapData data, IRandom rng, string countStr, string heightStr, string rX, string rY)
        {
            double linePower = GetLinePower(data.PointsCount);
            int count = GetNumberInRange(countStr, rng);
            Trace.WriteLine($"AddRange: count={count}, height={heightStr}");

            while (count-- > 0)
            {
                byte[] used = new byte[data.Cells.Length];
                double h = Lim(GetNumberInRange(heightStr, rng));

                // 1. Point Selection
                double startX = GetPointInRange(rX, data.Width, rng);
                double startY = GetPointInRange(rY, data.Height, rng);

                double endX = 0, endY = 0, dist = 0;
                int limit = 0;
                do
                {
                    endX = rng.Next() * data.Width * 0.8 + data.Width * 0.1;
                    endY = rng.Next() * data.Height * 0.7 + data.Height * 0.15;
                    dist = Math.Abs(endY - startY) + Math.Abs(endX - startX);
                    limit++;
                } while ((dist < data.Width / 8.0 || dist > data.Width / 3.0) && limit < 50);

                int startCell = FindGridCell(data, startX, startY);
                int endCell = FindGridCell(data, endX, endY);
                Trace.WriteLine($"Points: start({startX:F2}, {startY:F2}) end({endX:F2}, {endY:F2}) cells: {startCell}->{endCell}");

                List<int> getRange(int cur, int end)
                {
                    List<int> rPath = new List<int> { cur };
                    used[cur] = 1;
                    while (cur != end)
                    {
                        double min = double.PositiveInfinity;
                        int nextStep = -1;
                        foreach (int e in data.Cells[cur].C)
                        {
                            if (e == -1 || used[e] == 1) continue;
                            double diff = Math.Pow(data.Points[end].X - data.Points[e].X, 2) +
                                         Math.Pow(data.Points[end].Y - data.Points[e].Y, 2);
                            if (rng.Next() > 0.85) diff /= 2.0;
                            if (diff < min) { min = diff; nextStep = e; }
                        }
                        if (nextStep == -1) return rPath;
                        cur = nextStep;
                        rPath.Add(cur);
                        used[cur] = 1;
                    }
                    return rPath;
                }

                List<int> ridge = getRange(startCell, endCell);
                Trace.WriteLine($"Ridge path length: {ridge.Count}");

                List<int> queue = new List<int>(ridge);
                int iterations = 0;
                while (queue.Count > 0)
                {
                    List<int> frontier = new List<int>(queue);
                    queue.Clear();
                    iterations++;
                    foreach (int idx in frontier)
                    {
                        double noise = rng.Next() * 0.3 + 0.85;
                        double newH = data.Cells[idx].H + h * noise;
                        data.Cells[idx].H = Lim((int)Math.Floor(newH));
                    }
                    h = Math.Pow(h, linePower) - 1;
                    if (h < 2) break;
                    foreach (int f in frontier)
                    {
                        foreach (int n in data.Cells[f].C)
                        {
                            if (n != -1 && used[n] == 0) { queue.Add(n); used[n] = 1; }
                        }
                    }
                }
                Trace.WriteLine($"Expansion: iterations={iterations}, finalH={h:F2}");

                for (int d = 0; d < ridge.Count; d++)
                {
                    if (d % 6 != 0) continue;
                    int cur = ridge[d];
                    for (int l = 0; l < iterations; l++)
                    {
                        int minCell = -1;
                        double minH = double.PositiveInfinity;
                        foreach (int n in data.Cells[cur].C)
                        {
                            if (n == -1) continue;
                            if (data.Cells[n].H < minH) { minH = data.Cells[n].H; minCell = n; }
                        }
                        if (minCell == -1) break;
                        double avgH = (data.Cells[cur].H * 2.0 + data.Cells[minCell].H) / 3.0;
                        data.Cells[minCell].H = Lim((int)Math.Floor(avgH));
                        cur = minCell;
                    }
                }
            }
        }

        private static void AddTrough(MapData data, IRandom rng, string countStr, string heightStr, string rX, string rY)
        {
            double linePower = GetLinePower(data.PointsCount);
            int count = GetNumberInRange(countStr, rng);

            while (count-- > 0)
            {
                byte[] used = new byte[data.Cells.Length];
                double h = Lim(GetNumberInRange(heightStr, rng));

                double startX = 0;
                double startY = 0;
                int startCell = 0;
                int endCell = 0;

                // 1. Find Start and End Points
                if (!string.IsNullOrEmpty(rX) && !string.IsNullOrEmpty(rY))
                {
                    int limit = 0;
                    do
                    {
                        startX = GetPointInRange(rX, data.Width, rng);
                        startY = GetPointInRange(rY, data.Height, rng);
                        startCell = FindGridCell(data, startX, startY);
                        limit++;
                    } while (data.Cells[startCell].H < 20 && limit < 50);

                    limit = 0;
                    double dist = 0;
                    do
                    {
                        double endX = rng.Next() * data.Width * 0.8 + data.Width * 0.1;
                        double endY = rng.Next() * data.Height * 0.7 + data.Height * 0.15;

                        //double sX = data.Points[startCell].X;
                        //double sY = data.Points[startCell].Y;
                        dist = Math.Abs(endY - startY) + Math.Abs(endX - startX);

                        endCell = FindGridCell(data, endX, endY);
                        limit++;
                    } while ((dist < data.Width / 8.0 || dist > data.Width / 2.0) && limit < 50);
                }

                // 2. Define Local Function for Pathfinding (Matches JS getRange)
                List<int> GetRange(int cur, int end)
                {
                    List<int> range = new List<int> { cur };
                    used[cur] = 1;

                    while (cur != end)
                    {
                        double min = double.MaxValue;
                        int next = -1;

                        foreach (int e in data.Cells[cur].C)
                        {
                            if (e == -1 || used[e] == 1) continue;

                            double dx = data.Points[end].X - data.Points[e].X;
                            double dy = data.Points[end].Y - data.Points[e].Y;
                            double diff = dx * dx + dy * dy;

                            // Apply the 20% jitter chance from JS
                            if (rng.Next() > 0.8) diff /= 2.0;

                            if (diff < min)
                            {
                                min = diff;
                                next = e;
                            }
                        }

                        if (next == -1) return range; // Dead end

                        cur = next;
                        range.Add(cur);
                        used[cur] = 1;
                    }
                    return range;
                }

                List<int> ridge = GetRange(startCell, endCell);

                // 3. Expansion (BFS)
                List<int> queue = new List<int>(ridge);
                int iterations = 0;
                while (queue.Count > 0)
                {
                    List<int> frontier = new List<int>(queue);
                    queue.Clear();
                    iterations++;

                    foreach (int cellIdx in frontier)
                    {
                        data.Cells[cellIdx].H = Lim(data.Cells[cellIdx].H - h * (rng.Next() * 0.3 + 0.85));
                    }

                    h = Math.Pow(h, linePower) - 1;
                    if (h < 2) break;

                    foreach (int f in frontier)
                    {
                        foreach (int n in data.Cells[f].C)
                        {
                            if (n != -1 && used[n] == 0)
                            {
                                used[n] = 1;
                                queue.Add(n);
                            }
                        }
                    }
                }

                // 4. Generate Prominences
                for (int d = 0; d < ridge.Count; d++)
                {
                    if (d % 6 != 0) continue;
                    int cur = ridge[d];

                    for (int l = 0; l < iterations; l++)
                    {
                        int minCell = -1;
                        double minH = double.MaxValue;

                        foreach (int n in data.Cells[cur].C)
                        {
                            if (n != -1 && data.Cells[n].H < minH)
                            {
                                minH = data.Cells[n].H;
                                minCell = n;
                            }
                        }

                        if (minCell == -1) break;

                        data.Cells[minCell].H = Lim((data.Cells[cur].H * 2.0 + data.Cells[minCell].H) / 3.0);
                        cur = minCell;
                    }
                }
            }
        }

        private static void AddStrait(MapData data, IRandom rng, string widthArg, string direction, HeightmapSelection selection)
        {
            int width = (int)Math.Min(GetNumberInRange(widthArg, rng), data.CellsCountX / 3.0);
            if (width < 1) return;

            byte[] used = new byte[data.Cells.Length];
            bool vert = direction == "vertical";

            double startX = vert ? Math.Floor(rng.Next() * data.Width * 0.4 + data.Width * 0.3) : 5;
            double startY = vert ? 5 : Math.Floor(rng.Next() * data.Height * 0.4 + data.Height * 0.3);
            double endX = vert ? Math.Floor(data.Width - startX - data.Width * 0.1 + rng.Next() * data.Width * 0.2) : data.Width - 5;
            double endY = vert ? data.Height - 5 : Math.Floor(data.Height - startY - data.Height * 0.1 + rng.Next() * data.Height * 0.2);

            int start = FindGridCell(data, startX, startY);
            int end = FindGridCell(data, endX, endY);

            // Strait path is distinct: No 'used' tracking in path selection to allow straighter lines
            List<int> range = new List<int>();
            int cur = start;
            while (cur != end)
            {
                double minD = double.MaxValue;
                int next = -1;
                foreach (int e in data.Cells[cur].C)
                {
                    if (e == -1) continue;
                    double d = Math.Pow(data.Points[end].X - data.Points[e].X, 2) + Math.Pow(data.Points[end].Y - data.Points[e].Y, 2);
                    if (rng.Next() > 0.8) d /= 2.0;
                    if (d < minD) { minD = d; next = e; }
                }
                if (next == -1) break;
                cur = next;
                range.Add(cur);
            }

            double step = 0.1 / width;
            while (width > 0)
            {
                double exp = 0.9 - step * width;
                List<int> query = new List<int>();
                foreach (int r in range)
                {
                    foreach (int e in data.Cells[r].C)
                    {
                        if (e == -1 || used[e] == 1) continue;
                        used[e] = 1;
                        query.Add(e);

                        double newH = Math.Pow(data.Cells[e].H, exp);
                        data.Cells[e].H = newH > 100 ? (byte)5 : Lim(newH);
                    }
                }
                range = query;
                width--;
            }
        }

        // Updated Helper to include 'used' array to match JS scope logic
        private static List<int> GetRangePath(MapData data, int cur, int end, IRandom rng, byte[] used)
        {
            var path = new List<int> { cur };
            used[cur] = 1;
            while (cur != end)
            {
                int best = -1;
                double minD = double.MaxValue;
                foreach (int n in data.Cells[cur].C)
                {
                    if (n == -1 || used[n] == 1) continue;
                    double d = Math.Pow(data.Points[end].X - data.Points[n].X, 2) + Math.Pow(data.Points[end].Y - data.Points[n].Y, 2);
                    if (rng.Next() > 0.85) d /= 2.0; // Random "wiggle"
                    if (d < minD) { minD = d; best = n; }
                }
                if (best == -1) break;
                cur = best; path.Add(cur); used[cur] = 1;
            }
            return path;
        }

        #endregion

        #region Simple Tools

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

        private static void Mask(MapData data, string powerStr)
        {
            // Directly parse the power string as a number, as it does not require a range/random in JS
            if (!double.TryParse(powerStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double power))
            {
                power = 1.0; // Default value matching JS (power = 1)
            }

            double fr = power != 0 ? Math.Abs(power) : 1.0;

            for (int i = 0; i < data.Cells.Length; i++)
            {
                double x = data.Points[i].X;
                double y = data.Points[i].Y;

                // Normalize coordinates to [-1, 1] range where 0 is the center
                double nx = (2.0 * x) / data.Width - 1.0;
                double ny = (2.0 * y) / data.Height - 1.0;

                // Calculate distance factor (1 at center, 0 at edges)
                // JS: let distance = (1 - nx ** 2) * (1 - ny ** 2);
                double distance = (1.0 - nx * nx) * (1.0 - ny * ny);

                // If power is negative, invert the mask (0 at center, 1 at edges)
                if (power < 0)
                {
                    distance = 1.0 - distance;
                }

                double h = data.Cells[i].H;
                double masked = h * distance;

                // Blend the original height with the masked height based on fr
                // JS: lim((h * (fr - 1) + masked) / fr)
                data.Cells[i].H = Lim((h * (fr - 1.0) + masked) / fr);
            }
        }

        #endregion

        #region Helper Functions

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

        private static int GetNumberInRange(string r, IRandom rng)
        {
            if (string.IsNullOrWhiteSpace(r)) return 0;

            // JS: if (!isNaN(+r))
            if (double.TryParse(r, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
            {
                // JS: ~~r + +P(r - ~~r)
                // (int)val in C# performs the same truncation as ~~ in JS
                int intPart = (int)val;
                return intPart + (rng.P(val - intPart) ? 1 : 0);
            }

            double sign = r[0] == '-' ? -1 : 1;
            string s = r;
            // JS: if (isNaN(+r[0])) r = r.slice(1);
            if (!char.IsDigit(s[0])) s = s.Substring(1);

            if (s.Contains('-'))
            {
                string[] range = s.Split('-');
                double min = double.Parse(range[0], System.Globalization.CultureInfo.InvariantCulture) * sign;
                double max = double.Parse(range[1], System.Globalization.CultureInfo.InvariantCulture);
                // JS: rand(min, max) -> Math.floor(Math.random() * (max - min + 1)) + min
                return (int)Math.Floor(rng.Next() * (max - min + 1) + min);
            }

            return 0;
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

            var result = rng.Next(min * length, max * length);

            return result;
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

        #endregion
    }
}