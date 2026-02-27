using MapGen.Core.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MapGen.Core.Modules
{
    public static class FeatureModule
    {
        public static void MarkupGrid(MapData data)
        {
            data.Rng.Init(data.Seed);

            int cellsCount = data.Cells.Length;
            data.Features = new List<MapFeature> { null }; // Index 0 is empty/padding like JS

            // Initialize all cells to unmarked state
            // Since FeatureId is ushort (0) and Distance is sbyte (0), 
            // and MapConstants.UNMARKED is 0, this is often the default,
            // but explicit reset ensures safety during re-generation.
            for (int i = 0; i < cellsCount; i++)
            {
                data.Cells[i].FeatureId = 0;
                data.Cells[i].Distance = MapConstants.UNMARKED;
            }

            Queue<int> queue = new Queue<int>();
            ushort nextFeatureId = 1;

            // 1. Identify Features using Flood Fill
            for (int startCell = 0; startCell < cellsCount; startCell++)
            {
                if (data.Cells[startCell].FeatureId != MapConstants.UNMARKED) continue;

                ushort currentFeatureId = nextFeatureId++;
                bool isLand = data.Cells[startCell].H >= MapConstants.LAND_THRESHOLD;
                bool isBorder = false;

                queue.Enqueue(startCell);
                data.Cells[startCell].FeatureId = currentFeatureId;

                while (queue.Count > 0)
                {
                    int cellId = queue.Dequeue();
                    var cell = data.Cells[cellId];

                    if (!isBorder && cell.B == 1) isBorder = true;

                    foreach (int neighborId in cell.C)
                    {
                        var neighbor = data.Cells[neighborId];
                        bool isNeighborLand = neighbor.H >= MapConstants.LAND_THRESHOLD;

                        if (isLand == isNeighborLand && neighbor.FeatureId == MapConstants.UNMARKED)
                        {
                            neighbor.FeatureId = currentFeatureId;
                            queue.Enqueue(neighborId);
                        }
                        else if (isLand && !isNeighborLand)
                        {
                            // Boundary detected: Mark initial coast distance directly on cells
                            cell.Distance = MapConstants.LAND_COAST;
                            neighbor.Distance = MapConstants.WATER_COAST;
                        }
                    }
                }

                var type = isLand ? FeatureType.Island : (isBorder ? FeatureType.Ocean : FeatureType.Lake);
                data.Features.Add(new MapFeature { Id = currentFeatureId, IsLand = isLand, IsBorder = isBorder, Type = type });
            }

            // 2. Markup Deep Water (Distance Field Propagation)
            // We now use the unified helper that works directly on MapCell[]
            MarkupDistance(data.Cells, MapConstants.DEEP_WATER, -1, -10);
        }

        public static void MarkupPack(MapPack pack)
        {
            int cellCount = pack.Cells.Length;
            if (cellCount == 0) return;

            ushort[] featureIds = new ushort[cellCount];
            var features = new List<MapFeature> { null }; // Feature 0 is null/dummy
            var queue = new Stack<int>();
            queue.Push(0);

            for (ushort featureId = 1; queue.Count > 0; featureId++)
            {
                int firstCell = queue.Pop();
                featureIds[firstCell] = featureId;

                bool land = pack.Cells[firstCell].H >= MapConstants.LAND_THRESHOLD;
                bool border = pack.Cells[firstCell].B == 1;
                int totalCells = 0;

                var featureQueue = new Queue<int>();
                featureQueue.Enqueue(firstCell);

                // Temporary storage for this feature's growth
                while (featureQueue.Count > 0)
                {
                    int cellId = featureQueue.Dequeue();
                    totalCells++;
                    if (pack.Cells[cellId].B == 1) border = true;

                    foreach (int n in pack.Cells[cellId].C)
                    {
                        bool nLand = pack.Cells[n].H >= MapConstants.LAND_THRESHOLD;

                        // Mark coastal distances during flood fill
                        if (land && !nLand)
                        {
                            pack.Cells[cellId].Distance = MapConstants.LAND_COAST;
                            pack.Cells[n].Distance = MapConstants.WATER_COAST;
                            DefineHaven(pack, cellId);
                        }

                        if (featureIds[n] == 0 && land == nLand)
                        {
                            featureIds[n] = featureId;
                            featureQueue.Enqueue(n);
                        }
                    }
                }

                features.Add(CreateFeature(pack, featureId, firstCell, land, border, totalCells, featureIds));

                // Find next unmarked cell
                int nextUnmarked = -1;
                for (int j = 0; j < cellCount; j++)
                {
                    if (featureIds[j] == 0) { nextUnmarked = j; break; }
                }
                if (nextUnmarked != -1) queue.Push(nextUnmarked);
            }

            // Apply secondary distance markup (Deep water/Inland)
            MarkupDistance(pack.Cells, MapConstants.DEEPER_LAND, 1, 127);
            MarkupDistance(pack.Cells, MapConstants.DEEP_WATER, -1, -110);

            pack.Features = features;
            // Sync feature IDs back to cells
            for (int i = 0; i < cellCount; i++) pack.Cells[i].FeatureId = featureIds[i];
        }

        private static void DefineHaven(MapPack pack, int cellId)
        {
            var cell = pack.Cells[cellId];
            var waterNeighbors = cell.C.Where(n => pack.Cells[n].H < MapConstants.LAND_THRESHOLD).ToList();
            if (!waterNeighbors.Any()) return;

            var p1 = pack.Points[cellId];
            int closest = waterNeighbors[0];
            double minDist = double.MaxValue;

            foreach (var wId in waterNeighbors)
            {
                var p2 = pack.Points[wId];
                double d2 = Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2);
                if (d2 < minDist) { minDist = d2; closest = wId; }
            }

            cell.Haven = closest;
            cell.Harbor = (byte)waterNeighbors.Count;
        }

        private static MapFeature CreateFeature(MapPack pack, ushort id, int firstCell, bool land, bool border, int totalCells, ushort[] fIds)
        {
            var type = land ? FeatureType.Island : (border ? FeatureType.Ocean : FeatureType.Lake);

            // Find a cell on the edge of the feature to start vertex tracing
            int startCell = firstCell;
            foreach (var cellId in Enumerable.Range(0, pack.Cells.Length).Where(i => fIds[i] == id))
            {
                if (pack.Cells[cellId].B == 1 || pack.Cells[cellId].C.Any(n => fIds[n] != id))
                {
                    startCell = cellId;
                    break;
                }
            }

            // Trace vertices
            var featureVertices = new List<int>();
            if (type != FeatureType.Ocean)
            {
                int startVertex = pack.Cells[startCell].V.FirstOrDefault(v => pack.Vertices[v].C.Any(c => fIds[c] != id));
                featureVertices = PathUtils.ConnectVertices(pack, startVertex, c => fIds[c] == id);
            }

            return new MapFeature
            {
                Id = id,
                IsLand = land,
                IsBorder = border,
                Type = type,
                CellsCount = totalCells,
                FirstCell = startCell,
                Vertices = featureVertices,
                Area = 0 // PolygonArea logic can be added here
            };
        }

        public static void MarkupDistance(MapCell[] cells, sbyte start, sbyte increment, sbyte limit)
        {
            for (sbyte dist = start; dist != limit; dist += increment)
            {
                int marked = 0;
                sbyte prevDist = (sbyte)(dist - increment);

                for (int i = 0; i < cells.Length; i++)
                {
                    // Look for cells marked in the previous iteration
                    if (cells[i].Distance != prevDist) continue;

                    foreach (int n in cells[i].C)
                    {
                        // If neighbor is unmarked, assign the current distance step
                        if (cells[n].Distance == MapConstants.UNMARKED)
                        {
                            cells[n].Distance = dist;
                            marked++;
                        }
                    }
                }
                if (marked == 0) break;
            }
        }
    }
}
