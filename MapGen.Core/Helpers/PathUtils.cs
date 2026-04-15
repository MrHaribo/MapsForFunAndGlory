using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace MapGen.Core.Helpers
{
    public static class PathUtils
    {
        #region Connect Vertices

        public static List<int> ConnectVertices(MapPack pack, int startingVertex, Func<int, bool> ofSameType, Action<int> addToChecked = null, bool closeRing = false)
        {
            var vertices = pack.Vertices;
            int maxIterations = vertices.Length;
            var chain = new List<int>();

            int next = startingVertex;
            int previous = -1;

            for (int i = 0; i == 0 || next != startingVertex; i++)
            {
                int current = next;
                chain.Add(current);

                var neibCells = vertices[current].AdjacentCells;
                if (addToChecked != null)
                {
                    foreach (var c in neibCells)
                        if (ofSameType(c)) addToChecked(c);
                }

                // D3 vertices usually have 3 adjacent cells and 3 adjacent vertices
                // We check the 'type' of the 3 cells to decide which vertex to follow
                var cTypes = neibCells.Select(ofSameType).ToArray();
                var vNeighbors = vertices[current].NeighborVertices;

                // Logic: Follow the boundary where the cell type changes
                if (vNeighbors.Count > 0 && vNeighbors[0] != previous && cTypes[0] != cTypes[1]) next = vNeighbors[0];
                else if (vNeighbors.Count > 1 && vNeighbors[1] != previous && cTypes[1] != cTypes[2]) next = vNeighbors[1];
                else if (vNeighbors.Count > 2 && vNeighbors[2] != previous && cTypes[0] != cTypes[2]) next = vNeighbors[2];

                if (next == current || i == maxIterations) break;
                previous = current;
            }

            if (closeRing) chain.Add(startingVertex);
            return chain;
        }

        #endregion

        #region Polygon Area

        public static double CalculatePolygonArea(MapCell cell, MapVertex[] vertices)
        {
            double area = 0;
            for (int i = 0; i < cell.Verticies.Count; i++)
            {
                var p1 = vertices[cell.Verticies[i]].Point;
                var p2 = vertices[cell.Verticies[(i + 1) % cell.Verticies.Count]].Point;
                area += (p1.X * p2.Y) - (p2.X * p1.Y);
            }
            return area / 2.0;
        }

        public static double CalculatePolygonArea(List<MapPoint> points)
        {
            if (points.Count < 3) return 0;
            double area = 0;
            for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
            {
                // Aligned with D3: (prevY * currX) - (prevX * currY)
                area += (points[j].Y * points[i].X) - (points[j].X * points[i].Y);
            }
            return area / 2.0;
        }

        // D3 polygonArea equivalent
        public static double CalculatePolygonArea(List<double[]> points)
        {
            double area = 0;
            for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
            {
                area += (points[j][0] + points[i][0]) * (points[j][1] - points[i][1]);
            }
            return area / 2.0;
        }

        #endregion

        #region PolesOfInaccessibility

        /// <summary>
        /// Finds the Pole of Inaccessibility (the point furthest from the borders) 
        /// for groups of cells defined by a specific type accessor.
        /// </summary>
        public static Dictionary<int, MapPoint> GetPolesOfInaccessibility(MapPack pack, Func<int, int> getType)
        {
            var cells = pack.Cells;
            var poles = new Dictionary<int, MapPoint>();

            // 1. Group cells by their Type (e.g., StateId)
            var cellsByType = new Dictionary<int, List<int>>();
            for (int i = 0; i < cells.Length; i++)
            {
                int type = getType(i);
                if (type == 0) continue; // 0 usually means None/Neutral

                if (!cellsByType.ContainsKey(type))
                    cellsByType[type] = new List<int>();

                cellsByType[type].Add(i);
            }

            // 2. Find the deepest cell for each type using a Distance Transform
            foreach (var kvp in cellsByType)
            {
                int type = kvp.Key;
                var typeCells = kvp.Value;

                var distances = new Dictionary<int, double>();
                var queue = new Queue<int>();

                // Step A: Identify borders
                foreach (int cellId in typeCells)
                {
                    bool isBorder = cells[cellId].Border > 0; // Touches map edge

                    if (!isBorder)
                    {
                        foreach (int n in cells[cellId].NeighborCells)
                        {
                            if (getType(n) != type)
                            {
                                isBorder = true;
                                break;
                            }
                        }
                    }

                    if (isBorder)
                    {
                        distances[cellId] = 0;
                        queue.Enqueue(cellId);
                    }
                    else
                    {
                        distances[cellId] = double.MaxValue;
                    }
                }

                // Step B: Multi-source BFS to propagate distance inward
                int deepestCell = -1;
                double maxDist = -1;

                while (queue.Count > 0)
                {
                    int curr = queue.Dequeue();
                    double currDist = distances[curr];

                    // Track the cell with the highest distance
                    if (currDist > maxDist)
                    {
                        maxDist = currDist;
                        deepestCell = curr;
                    }

                    foreach (int n in cells[curr].NeighborCells)
                    {
                        if (getType(n) == type)
                        {
                            // Calculate actual geographic distance between cell centers
                            double distToNeighbor = currDist + Math.Sqrt(
                                Math.Pow(cells[curr].Point.X - cells[n].Point.X, 2) +
                                Math.Pow(cells[curr].Point.Y - cells[n].Point.Y, 2));

                            if (distToNeighbor < distances[n])
                            {
                                distances[n] = distToNeighbor;
                                queue.Enqueue(n);
                            }
                        }
                    }
                }

                // Step C: Record the pole
                if (deepestCell != -1)
                {
                    poles[type] = cells[deepestCell].Point;
                }
                else if (typeCells.Count > 0)
                {
                    // Fallback: If it's a 1-cell state, just use its center
                    poles[type] = cells[typeCells[0]].Point;
                }
            }

            return poles;
        }

        #endregion

        #region Find Path

        /// <summary>
        /// Finds the shortest path between two cells using a cost-based pathfinding algorithm.
        /// </summary>
        public static List<int> FindPath(MapPack pack, int start, Func<int, bool> isExit, Func<int, int, double> getCost)
        {
            if (isExit(start)) return null;

            int cellsCount = pack.Cells.Length;

            // Using arrays instead of Dictionaries for O(1) lookups to match JS performance
            int[] from = new int[cellsCount];
            double[] cost = new double[cellsCount];

            for (int i = 0; i < cellsCount; i++)
            {
                from[i] = -1;
                cost[i] = double.PositiveInfinity;
            }

            // Using your existing PriorityQueue
            var queue = new PriorityQueue<int, double>();
            queue.Enqueue(start, 0.0);
            cost[start] = 0.0;

            while (queue.Count > 0)
            {
                // Extract the current cell and its cost
                queue.TryDequeue(out int current, out double currentCost);

                foreach (int next in pack.Cells[current].NeighborCells)
                {
                    // Greedily check for the exit to match JS early-exit optimization
                    if (isExit(next))
                    {
                        from[next] = current;
                        return RestorePath(next, start, from);
                    }

                    double nextCost = getCost(current, next);
                    if (double.IsInfinity(nextCost)) continue; // Impassable cell

                    double totalCost = currentCost + nextCost;

                    if (totalCost >= cost[next]) continue; // Has cheaper path

                    from[next] = current;
                    cost[next] = totalCost;

                    queue.Enqueue(next, totalCost);
                }
            }

            return null; // No path found
        }

        private static List<int> RestorePath(int exit, int start, int[] from)
        {
            var pathCells = new List<int>();

            int current = exit;
            while (current != start)
            {
                pathCells.Add(current);
                current = from[current];
            }

            pathCells.Add(start);
            pathCells.Reverse();

            return pathCells;
        }

        #endregion

        public static double Dist2(MapPoint p1, MapPoint p2)
        {
            return Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2);
        }
    }
}
