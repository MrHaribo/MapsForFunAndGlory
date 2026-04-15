using System;
using System.Collections.Generic;
using System.Linq;

namespace MapGen.Core.Helpers
{
    public static class PolylabelUtils
    {
        public static Dictionary<int, MapPoint> CalculateStatePoles(MapPack pack)
        {
            var poles = new Dictionary<int, MapPoint>();
            var states = pack.States.Where(s => s.Id > 0).ToList();

            foreach (var state in states)
            {
                var polygonRings = GetStatePolygons(pack, state.Id);
                if (polygonRings.Count == 0)
                {
                    poles[state.Id] = pack.Cells[state.CenterCell].Point;
                    continue;
                }

                var rawPole = GetPole(polygonRings, 20.0);
                poles[state.Id] = rawPole;
            }

            return poles;
        }

        // --- 1. Edge-Cancellation Boundary Extractor (With Inner Lake fix) ---
        private static HashSet<int> GetInnerLakes(MapPack pack, int stateId)
        {
            var lakeLandNeighbors = new Dictionary<int, HashSet<int>>();
            for (int i = 0; i < pack.Cells.Length; i++)
            {
                if (pack.Cells[i].Height >= 20) continue;

                int fId = pack.Cells[i].FeatureId;
                if (pack.Features[fId].Type != FeatureType.Lake) continue;

                if (!lakeLandNeighbors.ContainsKey(fId)) lakeLandNeighbors[fId] = new HashSet<int>();

                foreach (int n in pack.Cells[i].NeighborCells)
                {
                    if (pack.Cells[n].Height >= 20)
                    {
                        lakeLandNeighbors[fId].Add(pack.Cells[n].StateId);
                    }
                }
            }

            var innerLakes = new HashSet<int>();
            foreach (var kvp in lakeLandNeighbors)
            {
                // If the lake only touches land belonging to THIS state, it's an inner lake!
                if (kvp.Value.Count == 1 && kvp.Value.Contains(stateId))
                {
                    innerLakes.Add(kvp.Key);
                }
            }
            return innerLakes;
        }

        private static List<List<MapPoint>> GetStatePolygons(MapPack pack, int stateId)
        {
            var innerLakes = GetInnerLakes(pack, stateId);
            var edges = new HashSet<(int, int)>();

            for (int i = 0; i < pack.Cells.Length; i++)
            {
                var cell = pack.Cells[i];

                bool isState = cell.StateId == stateId;
                // BUG REPLICATION: Pretend inner lakes are part of the state so their borders cancel out!
                if (!isState && innerLakes.Contains(cell.FeatureId)) isState = true;

                if (!isState) continue;

                for (int vIdx = 0; vIdx < cell.Verticies.Count; vIdx++)
                {
                    int v1 = cell.Verticies[vIdx];
                    int v2 = cell.Verticies[(vIdx + 1) % cell.Verticies.Count];

                    int minV = Math.Min(v1, v2);
                    int maxV = Math.Max(v1, v2);
                    var edgeKey = (minV, maxV);

                    if (edges.Contains(edgeKey)) edges.Remove(edgeKey);
                    else edges.Add(edgeKey);
                }
            }

            var rings = new List<List<MapPoint>>();
            var edgeList = edges.ToList();

            while (edgeList.Count > 0)
            {
                var ring = new List<int>();
                var currentEdge = edgeList[0];
                edgeList.RemoveAt(0);

                int nextVertex = currentEdge.Item2;
                ring.Add(currentEdge.Item1);
                ring.Add(nextVertex);

                while (true)
                {
                    int matchIdx = edgeList.FindIndex(e => e.Item1 == nextVertex || e.Item2 == nextVertex);
                    if (matchIdx == -1) break;

                    var nextE = edgeList[matchIdx];
                    edgeList.RemoveAt(matchIdx);

                    nextVertex = nextE.Item1 == nextVertex ? nextE.Item2 : nextE.Item1;
                    if (nextVertex == ring[0]) break;

                    ring.Add(nextVertex);
                }

                var pointRing = ring.Select(v => pack.Vertices[v].Point).ToList();
                rings.Add(pointRing);
            }

            return rings.OrderByDescending(r => r.Count).ToList();
        }

        // --- 2. Mapbox Polylabel Port ---
        private class Cell
        {
            public double X, Y, H, D, Max;
            public Cell(double x, double y, double h, List<List<MapPoint>> polygon)
            {
                X = x; Y = y; H = h;
                D = PointToPolygonDist(X, Y, polygon);
                Max = D + H * Math.Sqrt(2);
            }
        }

        private class CellQueue
        {
            private List<Cell> data = new List<Cell>();
            public int Count => data.Count;

            public void Push(Cell item)
            {
                data.Add(item);
                Up(data.Count - 1);
            }

            public Cell Pop()
            {
                if (data.Count == 0) return null;
                Cell top = data[0];
                Cell bottom = data[data.Count - 1];
                data.RemoveAt(data.Count - 1);
                if (data.Count > 0)
                {
                    data[0] = bottom;
                    Down(0);
                }
                return top;
            }

            private void Up(int i)
            {
                Cell item = data[i];
                while (i > 0)
                {
                    int p = (i - 1) >> 1;
                    Cell parent = data[p];
                    if (item.Max <= parent.Max) break;
                    data[i] = parent;
                    i = p;
                }
                data[i] = item;
            }

            private void Down(int i)
            {
                int halfLength = data.Count >> 1;
                Cell item = data[i];
                while (i < halfLength)
                {
                    int left = (i << 1) + 1;
                    int right = left + 1;
                    Cell bestChild = data[left];

                    if (right < data.Count && data[right].Max > bestChild.Max)
                    {
                        left = right;
                        bestChild = data[right];
                    }

                    if (bestChild.Max <= item.Max) break;
                    data[i] = bestChild;
                    i = left;
                }
                data[i] = item;
            }
        }

        private static Cell GetCentroidCell(List<List<MapPoint>> polygon)
        {
            double area = 0;
            double x = 0, y = 0;
            var points = polygon[0];

            for (int i = 0, len = points.Count, j = len - 1; i < len; j = i++)
            {
                var a = points[i];
                var b = points[j];
                double f = a.X * b.Y - b.X * a.Y;
                x += (a.X + b.X) * f;
                y += (a.Y + b.Y) * f;
                area += f * 3;
            }

            if (area == 0) return new Cell(points[0].X, points[0].Y, 0, polygon);
            return new Cell(x / area, y / area, 0, polygon);
        }

        private static MapPoint GetPole(List<List<MapPoint>> polygon, double precision)
        {
            // 1. FIX: Calculate BBox over ALL rings, not just polygon[0]!
            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;

            foreach (var ring in polygon)
            {
                foreach (var p in ring)
                {
                    if (p.X < minX) minX = p.X;
                    if (p.X > maxX) maxX = p.X;
                    if (p.Y < minY) minY = p.Y;
                    if (p.Y > maxY) maxY = p.Y;
                }
            }

            double width = maxX - minX;
            double height = maxY - minY;
            double cellSize = Math.Min(width, height);
            double h = cellSize / 2.0;

            if (cellSize == 0) return new MapPoint(minX, minY);

            var cellQueue = new CellQueue();

            var bestCell = GetCentroidCell(polygon);
            var bboxCell = new Cell(minX + width / 2, minY + height / 2, 0, polygon);
            if (bboxCell.D > bestCell.D) bestCell = bboxCell;

            for (double x = minX; x < maxX; x += cellSize)
            {
                for (double y = minY; y < maxY; y += cellSize)
                {
                    var cell = new Cell(x + h, y + h, h, polygon);
                    cellQueue.Push(cell);
                }
            }

            while (cellQueue.Count > 0)
            {
                var cell = cellQueue.Pop();

                if (cell.D > bestCell.D) bestCell = cell;
                if (cell.Max - bestCell.D <= precision) continue;

                h = cell.H / 2;
                cellQueue.Push(new Cell(cell.X - h, cell.Y - h, h, polygon));
                cellQueue.Push(new Cell(cell.X + h, cell.Y - h, h, polygon));
                cellQueue.Push(new Cell(cell.X - h, cell.Y + h, h, polygon));
                cellQueue.Push(new Cell(cell.X + h, cell.Y + h, h, polygon));
            }

            return new MapPoint(bestCell.X, bestCell.Y);
        }

        private static double PointToPolygonDist(double x, double y, List<List<MapPoint>> polygon)
        {
            bool inside = false;
            double minDistSq = double.MaxValue;

            for (int i = 0; i < polygon.Count; i++)
            {
                var ring = polygon[i];
                for (int j = 0, len = ring.Count, jPrev = len - 1; j < len; jPrev = j++)
                {
                    var a = ring[j];
                    var b = ring[jPrev];

                    if ((a.Y > y != b.Y > y) && (x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y) + a.X))
                        inside = !inside;

                    minDistSq = Math.Min(minDistSq, GetSegDistSq(x, y, a, b));
                }
            }
            return (inside ? 1 : -1) * Math.Sqrt(minDistSq);
        }

        private static double GetSegDistSq(double px, double py, MapPoint a, MapPoint b)
        {
            double x = a.X, y = a.Y, dx = b.X - x, dy = b.Y - y;

            if (dx != 0 || dy != 0)
            {
                double t = ((px - x) * dx + (py - y) * dy) / (dx * dx + dy * dy);
                if (t > 1) { x = b.X; y = b.Y; }
                else if (t > 0) { x += dx * t; y += dy * t; }
            }

            dx = px - x; dy = py - y;
            return dx * dx + dy * dy;
        }
    }
}