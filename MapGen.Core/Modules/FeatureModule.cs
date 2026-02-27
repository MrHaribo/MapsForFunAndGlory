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
            var cells = pack.Cells;
            var vertices = pack.Vertices;
            int packCellsNumber = cells.Length;
            if (packCellsNumber == 0) return;

            // Use local arrays to match JS scope, then sync to MapCell properties at the end
            sbyte[] distanceField = new sbyte[packCellsNumber]; // pack.cells.t
            ushort[] featureIds = new ushort[packCellsNumber];  // pack.cells.f
            int[] haven = new int[packCellsNumber];             // haven: opposite water cell
            byte[] harbor = new byte[packCellsNumber];          // harbor: count of water neighbors
            var features = new List<MapFeature> { null };

            // Helper: isLand check matching JS logic
            bool IsLand(int id) => cells[id].H >= MapConstants.LAND_THRESHOLD;
            bool IsWater(int id) => !IsLand(id);

            List<int> queue = new List<int> { 0 };

            for (ushort featureId = 1; queue.Count > 0 && queue[0] != -1; featureId++)
            {
                int firstCell = queue[0];
                featureIds[firstCell] = featureId;

                bool land = IsLand(firstCell);
                bool border = cells[firstCell].B == 1;
                int totalCells = 1;

                // Flood fill queue (JS uses the same array for outer and inner loops)
                var floodQueue = new Stack<int>();
                floodQueue.Push(firstCell);

                while (floodQueue.Count > 0)
                {
                    int cellId = floodQueue.Pop();
                    if (cells[cellId].B == 1) border = true;

                    foreach (int neighborId in cells[cellId].C)
                    {
                        bool isNeibLand = IsLand(neighborId);

                        if (land && !isNeibLand)
                        {
                            distanceField[cellId] = MapConstants.LAND_COAST;
                            distanceField[neighborId] = MapConstants.WATER_COAST;
                            if (haven[cellId] == 0) DefineHaven(cellId);
                        }
                        else if (land && isNeibLand)
                        {
                            if (distanceField[neighborId] == MapConstants.UNMARKED && distanceField[cellId] == MapConstants.LAND_COAST)
                                distanceField[neighborId] = MapConstants.LANDLOCKED;
                            else if (distanceField[cellId] == MapConstants.UNMARKED && distanceField[neighborId] == MapConstants.LAND_COAST)
                                distanceField[cellId] = MapConstants.LANDLOCKED;
                        }

                        if (featureIds[neighborId] == 0 && land == isNeibLand)
                        {
                            floodQueue.Push(neighborId);
                            featureIds[neighborId] = featureId;
                            totalCells++;
                        }
                    }
                }

                features.Add(AddFeature(firstCell, land, border, featureId, totalCells));

                // Find next unmarked cell
                int nextIndex = -1;
                for (int i = 0; i < packCellsNumber; i++)
                    if (featureIds[i] == 0) { nextIndex = i; break; }

                queue.Clear();
                if (nextIndex != -1) queue.Add(nextIndex);
            }

            // Final Sync to MapCell and MapPack
            for (int i = 0; i < packCellsNumber; i++)
            {
                cells[i].FeatureId = featureIds[i];
                cells[i].Distance = distanceField[i];
                cells[i].Haven = haven[i];
                cells[i].Harbor = harbor[i];
            }

            // Secondary Distance Markup (Generalized helpers)
            MarkupDistance(cells, MapConstants.DEEPER_LAND, 1, 127);
            MarkupDistance(cells, MapConstants.DEEP_WATER, -1, -110);

            pack.Features = features;

            // --- Local Functions ---

            void DefineHaven(int cellId)
            {
                var waterCells = cells[cellId].C.Where(IsWater).ToList();
                if (waterCells.Count == 0) return;

                var p1 = pack.Points[cellId];
                int closest = waterCells[0];
                double minDist = double.MaxValue;

                foreach (var wId in waterCells)
                {
                    var p2 = pack.Points[wId];
                    double d2 = Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2);
                    if (d2 < minDist) { minDist = d2; closest = wId; }
                }

                haven[cellId] = closest;
                harbor[cellId] = (byte)waterCells.Count;
            }

            MapFeature AddFeature(int firstCell, bool land, bool border, int featureId, int totalCells)
            {
                var typeStr = land ? "island" : border ? "ocean" : "lake";
                var typeEnum = land ? FeatureType.Island : border ? FeatureType.Ocean : FeatureType.Lake;

                var (startCell, featureVertices) = GetCellsData(typeStr, firstCell);
                
                // Calculate Area

                // JS: const points = clipPoly(featureVertices.map(vertex => vertices.p[vertex]));
                var points = featureVertices.Select(v => vertices[v].P).ToList();
                var clippedPoints = PathUtils.ClipPolygon(points, pack.Width, pack.Height);

                // D3 polygonArea equivalent logic
                // IMPORTANT: Use the CLIPPED points for the area, but the ORIGINAL vertices for the feature metadata
                double area = PathUtils.CalculateAreaFromPoints(clippedPoints);
                double absArea = Math.Abs(NumberUtils.Round(area));

                var feature = new MapFeature
                {
                    Id = featureId,
                    Type = typeEnum,
                    IsLand = land,
                    IsBorder = border,
                    CellsCount = totalCells,
                    FirstCell = startCell,
                    Vertices = featureVertices,
                    Area = absArea
                };

                if (typeEnum == FeatureType.Lake)
                {
                    if (area > 0) feature.Vertices.Reverse();
                    feature.Shoreline = feature.Vertices
                        .SelectMany(v => vertices[v].C)
                        .Where(IsLand)
                        .Distinct()
                        .ToList();

                    feature.Height = LakeModule.GetHeight(pack, feature);
                }

                return feature;

                (int, List<int>) GetCellsData(string featureType, int fCell)
                {
                    if (featureType == "ocean") return (fCell, new List<int>());

                    // Bounds-safe predicates to match JS behavior
                    bool OfSameType(int cId) => cId >= 0 && cId < featureIds.Length && featureIds[cId] == featureId;
                    bool OfDifferentType(int cId) => cId < 0 || cId >= featureIds.Length || featureIds[cId] != featureId;

                    int startCellInternal = FindOnBorderCell(fCell);
                    var verticesInternal = GetFeatureVertices(startCellInternal);
                    return (startCellInternal, verticesInternal);

                    int FindOnBorderCell(int currentCell)
                    {
                        bool IsOnBorder(int cId) => cells[cId].B == 1 || cells[cId].C.Any(OfDifferentType);
                        if (IsOnBorder(currentCell)) return currentCell;

                        for (int i = 0; i < packCellsNumber; i++)
                            if (OfSameType(i) && IsOnBorder(i)) return i;

                        throw new Exception($"Markup: firstCell {currentCell} is not on the feature or map border");
                    }

                    List<int> GetFeatureVertices(int sCell)
                    {
                        // Fix: Use Where + DefaultIfEmpty to safely handle the -1 default
                        int startingVertex = cells[sCell].V
                            .Where(v => vertices[v].C.Any(OfDifferentType))
                            .DefaultIfEmpty(-1)
                            .First();

                        if (startingVertex == -1)
                            throw new Exception($"Markup: startingVertex for cell {sCell} is not found");

                        // Assuming PathUtils.ConnectVertices signature matches: 
                        // (MapPack, int, Func<int, bool>, Func<int, bool>, bool)
                        return PathUtils.ConnectVertices(pack, startingVertex, OfSameType, null, false);
                    }
                }
            }
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
