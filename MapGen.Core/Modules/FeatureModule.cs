using MapGen.Core.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
                bool isLand = data.Cells[startCell].Height >= MapConstants.LAND_THRESHOLD;
                bool isBorder = false;

                queue.Enqueue(startCell);
                data.Cells[startCell].FeatureId = currentFeatureId;

                while (queue.Count > 0)
                {
                    int cellId = queue.Dequeue();
                    var cell = data.Cells[cellId];

                    if (!isBorder && cell.Border == 1) isBorder = true;

                    foreach (int neighborId in cell.NeighborCells)
                    {
                        var neighbor = data.Cells[neighborId];
                        bool isNeighborLand = neighbor.Height >= MapConstants.LAND_THRESHOLD;

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
            ushort[] haven = new ushort[packCellsNumber];             // haven: opposite water cell
            byte[] harbor = new byte[packCellsNumber];          // harbor: count of water neighbors
            var features = new List<MapFeature>();

            // Helper: isLand check matching JS logic
            bool IsLand(int id) => cells[id].Height >= MapConstants.LAND_THRESHOLD;
            bool IsWater(int id) => !IsLand(id);

            List<int> queue = new List<int> { 0 };

            for (ushort featureId = 1; queue.Count > 0 && queue[0] != -1; featureId++)
            {
                int firstCell = queue[0];
                featureIds[firstCell] = featureId;

                bool land = IsLand(firstCell);
                bool border = cells[firstCell].Border == 1;
                int totalCells = 1;
                ushort detectedParentId = 0;

                // Flood fill queue (JS uses the same array for outer and inner loops)
                var floodQueue = new Stack<int>();
                floodQueue.Push(firstCell);

                while (floodQueue.Count > 0)
                {
                    int cellId = floodQueue.Pop();
                    if (cells[cellId].Border == 1) border = true;

                    foreach (int neighborId in cells[cellId].NeighborCells)
                    {
                        bool isNeibLand = IsLand(neighborId);
                        ushort neibFeatureId = featureIds[neighborId];

                        // Parent Detection Logic
                        // If the neighbor already has a feature ID and it's different from ours,
                        // that feature is a candidate for being our "Parent" (the container).
                        if (neibFeatureId != 0 && neibFeatureId != featureId)
                        {
                            // For a Lake, the parent is the first Land feature we touch.
                            // For an Island, the parent is the first Water feature we touch.
                            if (detectedParentId == 0) detectedParentId = neibFeatureId;
                        }

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

                var feature = AddFeature(firstCell, land, border, featureId, totalCells, detectedParentId);
                features.Add(feature);

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
                var waterCells = cells[cellId].NeighborCells.Where(IsWater).ToList();
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

                haven[cellId] = (ushort)closest;
                harbor[cellId] = (byte)waterCells.Count;
            }

            MapFeature AddFeature(int firstCell, bool land, bool border, int featureId, int totalCells, int parentId)
            {
                var featureType = land ? FeatureType.Island : border ? FeatureType.Ocean : FeatureType.Lake;

                var (startCell, featureVertices) = GetCellsData(featureType, firstCell);

                // Calculate Area


                // 1. Convert vertex indices to MapPoints
                var points = featureVertices.Select(v => vertices[v].Point).ToList();

                // 2. Use our new LineClip port
                var clippedPoints = LineClip.PolygonClip(points, pack.Width, pack.Height);

                // 3. Calculate Area (Signed)
                double area = PathUtils.CalculatePolygonArea(clippedPoints);
                double absArea = Math.Abs(NumberUtils.Round(area));

                var feature = new MapFeature
                {
                    Id = featureId,
                    ParentId = parentId,
                    Type = featureType,
                    IsLand = land,
                    IsBorder = border,
                    CellsCount = totalCells,
                    FirstCell = startCell,
                    Vertices = featureVertices,
                    Area = absArea
                };

                if (featureType == FeatureType.Island || featureType == FeatureType.Lake)
                {
                    bool inverseWindingOrder = area > 0;
                    if (featureType == FeatureType.Lake && inverseWindingOrder) feature.Vertices.Reverse();

                    feature.ShorelineVertices = new List<int>(feature.Vertices);

                    if (featureType == FeatureType.Island && inverseWindingOrder) feature.ShorelineVertices.Reverse();

                    bool targetIsLand = (featureType == FeatureType.Lake);
                    feature.ShorelineCells = feature.ShorelineVertices
                        .SelectMany(v => vertices[v].AdjacentCells)
                        .Where(cIdx => cIdx >= 0 && cIdx < pack.Cells.Length)
                        .Where(cIdx => {
                            bool isCellLand = cells[cIdx].Height >= MapConstants.LAND_THRESHOLD;
                            bool isPartOfThisFeature = featureIds[cIdx] == featureId;
                            return !isPartOfThisFeature && (isCellLand == targetIsLand);
                        })
                        .Distinct()
                        .ToList();

                    if (featureType == FeatureType.Lake)
                    {
                        feature.Height = LakeModule.GetHeight(pack, feature);
                    }

                    // ... after Shoreline calculation ...
                    OrderVertices(feature);

                    // Optional: Ensure the final sorted loop matches the intended winding
                    double sortedArea = PathUtils.CalculatePolygonArea(feature.ShorelineVertices.Select(v => vertices[v].Point).ToList());
                    if (featureType == FeatureType.Island && sortedArea > 0) feature.ShorelineVertices.Reverse();
                    if (featureType == FeatureType.Lake && sortedArea < 0) feature.ShorelineVertices.Reverse();
                }

                return feature;

                void OrderVertices(MapFeature feat)
                {
                    if (feat.ShorelineVertices == null || feat.ShorelineVertices.Count < 3) return;

                    var boundaryGraph = new Dictionary<int, List<int>>();
                    var vSet = new HashSet<int>(feat.ShorelineVertices);

                    foreach (int vIdx in vSet)
                    {
                        foreach (int neighborV in vertices[vIdx].NeighborVertices)
                        {
                            if (!vSet.Contains(neighborV)) continue;

                            // FIX: Filter out -1 (map edge) indices to prevent IndexOutOfRange
                            var sharedCells = vertices[vIdx].AdjacentCells
                                .Intersect(vertices[neighborV].AdjacentCells)
                                .Where(c => c >= 0 && c < featureIds.Length)
                                .ToList();

                            bool isBoundary = sharedCells.Any(c => featureIds[c] == feat.Id) &&
                                              sharedCells.Any(c => featureIds[c] != feat.Id);

                            if (isBoundary)
                            {
                                if (!boundaryGraph.ContainsKey(vIdx)) boundaryGraph[vIdx] = new List<int>();
                                if (!boundaryGraph[vIdx].Contains(neighborV)) boundaryGraph[vIdx].Add(neighborV);
                            }
                        }
                    }

                    if (boundaryGraph.Count == 0) return;

                    var sorted = new List<int>();
                    var visitedEdges = new HashSet<(int, int)>();

                    // Pick a starting vertex that has exactly 2 boundary edges (ideal) 
                    // or just the first one if it's a pinch point.
                    int startV = boundaryGraph.OrderBy(kvp => kvp.Value.Count).First().Key;
                    int currentV = startV;

                    // Safety limit: twice the number of vertices to allow for complex shapes
                    for (int i = 0; i < feat.ShorelineVertices.Count * 2; i++)
                    {
                        sorted.Add(currentV);

                        if (!boundaryGraph.TryGetValue(currentV, out var neighbors)) break;

                        int nextV = -1;

                        // 1. Can we close the loop? Only if we've actually moved (count > 2)
                        if (sorted.Count > 2 && neighbors.Contains(startV))
                        {
                            nextV = startV;
                        }
                        else
                        {
                            // 2. Otherwise, find an edge we haven't walked yet
                            foreach (var n in neighbors)
                            {
                                if (!visitedEdges.Contains((currentV, n)))
                                {
                                    nextV = n;
                                    break;
                                }
                            }
                        }

                        if (nextV == -1 || nextV == startV) break;

                        visitedEdges.Add((currentV, nextV));
                        visitedEdges.Add((nextV, currentV));
                        currentV = nextV;
                    }

                    feat.ShorelineVertices = sorted;
                }

                (int, List<int>) GetCellsData(FeatureType featureType, int fCell)
                {
                    if (featureType == FeatureType.Ocean) return (fCell, new List<int>());

                    // Bounds-safe predicates to match JS behavior
                    bool OfSameType(int cId) => cId >= 0 && cId < featureIds.Length && featureIds[cId] == featureId;
                    bool OfDifferentType(int cId) => cId < 0 || cId >= featureIds.Length || featureIds[cId] != featureId;

                    int startCellInternal = FindOnBorderCell(fCell);
                    var verticesInternal = GetFeatureVertices(startCellInternal);
                    return (startCellInternal, verticesInternal);

                    int FindOnBorderCell(int currentCell)
                    {
                        bool IsOnBorder(int cId) => cells[cId].Border == 1 || cells[cId].NeighborCells.Any(OfDifferentType);
                        if (IsOnBorder(currentCell)) return currentCell;

                        for (int i = 0; i < packCellsNumber; i++)
                            if (OfSameType(i) && IsOnBorder(i)) return i;

                        throw new Exception($"Markup: firstCell {currentCell} is not on the feature or map border");
                    }

                    List<int> GetFeatureVertices(int sCell)
                    {
                        // Fix: Use Where + DefaultIfEmpty to safely handle the -1 default
                        int startingVertex = cells[sCell].Verticies
                            .Where(v => vertices[v].AdjacentCells.Any(OfDifferentType))
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

                    foreach (int n in cells[i].NeighborCells)
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

        public static void DefineGroups(MapPack pack)
        {
            int gridCellsNumber = pack.Cells.Length;

            // Thresholds based on Total Cells and Divisor constants
            int oceanMin = gridCellsNumber / MapConstants.OCEAN_MIN_SIZE_DIVISOR;
            int seaMin = gridCellsNumber / MapConstants.SEA_MIN_SIZE_DIVISOR;
            int continentMin = gridCellsNumber / MapConstants.CONTINENT_MIN_SIZE_DIVISOR;
            int islandMin = gridCellsNumber / MapConstants.ISLAND_MIN_SIZE_DIVISOR;

            foreach (var feature in pack.Features)
            {
                // Skip nulls. FMG logic specifically skips "ocean" type for the main loop 
                // but defines it via a sub-call. To maintain parity, we classify everything.
                if (feature == null) continue;

                if (feature.Type == FeatureType.Ocean)
                {
                    feature.Group = DefineOceanGroup(feature, oceanMin, seaMin);
                }
                else if (feature.Type == FeatureType.Lake)
                {
                    // Note: FMG sets feature.height here via Lakes.getHeight. 
                    // We assume feature.Height is already populated.
                    feature.Group = DefineLakeGroup(feature);
                }
                else if (feature.IsLand) // This handles "island" type in JS
                {
                    feature.Group = DefineIslandGroup(feature, pack, continentMin, islandMin);
                }
            }
        }

        private static FeatureGroup DefineOceanGroup(MapFeature feature, int oceanMin, int seaMin)
        {
            if (feature.CellsCount > oceanMin) return FeatureGroup.Ocean;
            if (feature.CellsCount > seaMin) return FeatureGroup.Sea;
            return FeatureGroup.Gulf;
        }

        private static FeatureGroup DefineIslandGroup(MapFeature feature, MapPack pack, int continentMin, int islandMin)
        {
            // Parity: pack.features[pack.cells.f[feature.firstCell - 1]]
            // We check the feature ID of the cell index exactly 1 before the first cell of this feature.
            int prevCellIdx = feature.FirstCell - 1;
            if (prevCellIdx >= 0)
            {
                ushort prevFeatureId = pack.Cells[prevCellIdx].FeatureId;
                // MapPack.GetFeature(id) uses id - 1 internally
                var prevFeature = pack.GetFeature(prevFeatureId);

                if (prevFeature != null && prevFeature.Type == FeatureType.Lake)
                    return FeatureGroup.LakeIsland;
            }

            if (feature.CellsCount > continentMin) return FeatureGroup.Continent;
            if (feature.CellsCount > islandMin) return FeatureGroup.Island;
            return FeatureGroup.Isle;
        }

        private static FeatureGroup DefineLakeGroup(MapFeature feature)
        {
            // Temperature check
            if (feature.Temp < MapConstants.LAKE_FROZEN_TEMP) return FeatureGroup.Frozen;

            // Lava check: height > 60, small, and index-based randomness
            if (feature.Height > MapConstants.LAVA_LAKE_MIN_HEIGHT &&
                feature.CellsCount < MapConstants.LAVA_LAKE_MAX_CELLS &&
                feature.FirstCell % 10 == 0) return FeatureGroup.Lava;

            // Logic for lakes without Inlets and Outlets (Endorheic/Sinks)
            bool hasInlets = feature.Inlets != null && feature.Inlets.Count > 0;
            bool hasOutlet = feature.OutCell > 0; // Assuming OutCell 0 or -1 means no outlet

            if (!hasInlets && !hasOutlet)
            {
                if (feature.Evaporation > feature.Flux * 4) return FeatureGroup.Dry;

                if (feature.CellsCount < MapConstants.SINKHOLE_MAX_CELLS &&
                    feature.FirstCell % 10 == 0) return FeatureGroup.Sinkhole;
            }

            // Salt lake check
            if (!hasOutlet && feature.Evaporation > feature.Flux) return FeatureGroup.Salt;

            return FeatureGroup.Freshwater;
        }

        public static void RankCells(MapPack pack)
        {
            var cells = pack.Cells;

            // 1. Get Biome Data for lookup
            var biomes = BiomModule.GetDefaultBiomes();

            // 2. Calculate Global Statistics
            // FMG median excludes 0s: cells.fl.filter(f => f)
            var fluxValues = cells.Where(c => c.Flux > 0).Select(c => c.Flux).ToList();
            double meanFlux = fluxValues.Count > 0 ? NumberUtils.Median(fluxValues) : 0.0;

            // FMG Parity: maxFlux = d3.max(cells.fl) + d3.max(cells.conf)
            // We take the max of each array and sum them.
            double maxFlux = cells.Max(c => c.Flux) + cells.Max(c => c.Confluence);

            // Mean area for population scaling
            double meanArea = cells.Average(c => (double)c.Area);

            foreach (var cell in cells)
            {
                // No population in water (Height < 20)
                if (cell.Height < MapConstants.LAND_THRESHOLD) continue;

                // 3. Base Suitability from Biome Habitability
                // We use the ID to index directly into our fetched biome list
                double score = biomes[cell.BiomeId].Habitability;
                if (score <= 0) continue;

                // 4. River & Confluence value
                if (meanFlux > 0)
                {
                    double currentFlux = cell.Flux + cell.Confluence;
                    // Normalize clamps the value between 0 and 1 based on (val - mean) / (max - mean)
                    score += NumberUtils.Normalize(currentFlux, meanFlux, maxFlux) * MapConstants.SUITABILITY_RIVER_SCALE;
                }

                // 5. Elevation penalty
                // Standard FMG logic: score -= (height - 50) / 5
                score -= (cell.Height - MapConstants.ELEVATION_OPTIMUM) / MapConstants.SUITABILITY_DIVISOR;

                // 6. Coastal and Waterfront value
                if (cell.Distance == MapConstants.LAND_COAST)
                {
                    // Estuary bonus if a river segment exists in this coastal cell
                    if (cell.RiverId > 0) score += MapConstants.SCORE_ESTUARY;

                    // Check Haven (the water cell this land cell 'leans' toward)
                    var havenCell = pack.Cells[cell.Haven];
                    var feature = pack.GetFeature(havenCell.FeatureId);

                    if (feature.Type == FeatureType.Lake)
                    {
                        score += GetLakeScore(feature.Group);
                    }
                    else
                    {
                        score += MapConstants.SCORE_OCEAN_COAST;

                        // Harbor: 1 water neighbor indicates a deep bay/protected cove
                        if (cell.Harbor == 1) score += MapConstants.SCORE_SAFE_HARBOR;
                    }
                }

                // 7. Finalize Suitability and Population
                // FMG: cells.s[i] = score / 5
                cell.Suitability = (short)(score / MapConstants.SUITABILITY_DIVISOR);

                // FMG: cells.pop[i] = score > 0 ? (score * area) / meanArea : 0
                cell.Population = cell.Suitability > 0
                    ? (float)((cell.Suitability * (double)cell.Area) / meanArea)
                    : 0.0f;
            }
        }

        private static double GetLakeScore(FeatureGroup group) => group switch
        {
            FeatureGroup.Freshwater => MapConstants.SCORE_FRESHWATER,
            FeatureGroup.Salt => MapConstants.SCORE_SALT,
            FeatureGroup.Frozen => MapConstants.SCORE_FROZEN,
            FeatureGroup.Dry => MapConstants.SCORE_DRY,
            FeatureGroup.Sinkhole => MapConstants.SCORE_SINKHOLE,
            FeatureGroup.Lava => MapConstants.SCORE_LAVA,
            _ => 0.0
        };
    }
}
