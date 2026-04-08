using MapGen.Core;
using MapGen.Core.Modules;
using SkiaSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapGen.Render.Skia.WinUI
{
    internal class MapGenRenderer
    {
        public static void RenderGridHeightmap(SKCanvas canvas, MapData map)
        {
            using var fillPaint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };

            for (int i = 0; i < map.Cells.Length; i++)
            {
                var cell = map.Cells[i];
                if (cell.Verticies == null || cell.Verticies.Count < 3) continue;

                byte h = cell.Height;
                if (h < MapConstants.LAND_THRESHOLD)
                {
                    byte blueDepth = (byte)Math.Clamp(100 + (h * 5), 0, 255);
                    fillPaint.Color = new SKColor(30, 60, blueDepth);
                }
                else
                {
                    byte landBrightness = (byte)Math.Clamp((h - 20) * 3 + 50, 0, 255);
                    fillPaint.Color = new SKColor(landBrightness, landBrightness, landBrightness);
                }

                using var path = CreateCellPath(cell, map.Vertices);
                canvas.DrawPath(path, fillPaint);
            }
        }

        public static void RenderPackHeightmap(SKCanvas canvas, MapPack pack)
        {
            // 1. Draw a base background for "Deep Ocean" 
            // Since Pack omits many ocean cells, we fill the canvas with the deepest water color first.
            canvas.Clear(new SKColor(30, 60, 100));

            using var fillPaint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };
            using var strokePaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                Color = SKColors.Black.WithAlpha(40),
                StrokeWidth = 0.5f,
                IsAntialias = true
            };

            foreach (var cell in pack.Cells)
            {
                // Safety check for vertices
                if (cell.Verticies == null || cell.Verticies.Count < 3) continue;

                // 2. Determine Color based on Height
                byte h = cell.Height;
                if (h < MapConstants.LAND_THRESHOLD)
                {
                    // Shallow water/Coastal
                    byte blueDepth = (byte)Math.Clamp(100 + (h * 5), 0, 255);
                    fillPaint.Color = new SKColor(30, 60, blueDepth);
                }
                else
                {
                    // Land
                    byte landBrightness = (byte)Math.Clamp((h - 20) * 3 + 50, 0, 255);
                    fillPaint.Color = new SKColor(landBrightness, landBrightness, landBrightness);
                }

                // 3. Create the Path using Pack-specific vertex data
                using var path = new SKPath();
                var firstV = pack.Vertices[cell.Verticies[0]];
                path.MoveTo((float)firstV.Point.X, (float)firstV.Point.Y);

                for (int i = 1; i < cell.Verticies.Count; i++)
                {
                    var v = pack.Vertices[cell.Verticies[i]];
                    path.LineTo((float)v.Point.X, (float)v.Point.Y);
                }
                path.Close();

                // 4. Draw the cell
                canvas.DrawPath(path, fillPaint);

                // Optional: Draw cell borders to see if they overlap or have gaps
                canvas.DrawPath(path, strokePaint);
            }
        }

        public static void RenderLandMask(SKCanvas canvas, MapData map)
        {
            // We use a solid color (it doesn't matter which, as long as it's not transparent)
            using var maskPaint = new SKPaint
            {
                Color = SKColors.Black,
                Style = SKPaintStyle.Fill,
                IsAntialias = false // No need for AA on a mask usually
            };

            // Draw every cell that is NOT water
            // Note: If you have a pre-calculated 'landPath' (concatenated polygons), 
            // it's much faster to DrawPath once than looping here.
            foreach (var cell in map.Cells)
            {
                if (cell.Height < 20) continue; // Skip water

                // Draw the polygon for this cell
                using var cellPath = new SKPath();
                var vertices = cell.Verticies; // Using your cell model's Vertex indices
                if (vertices == null || vertices.Count == 0) continue;

                // Assuming _map.Vertices contains MapPoint coordinates
                var startV = map.Vertices[vertices[0]];
                cellPath.MoveTo((float)startV.Point.X, (float)startV.Point.Y);

                for (int i = 1; i < vertices.Count; i++)
                {
                    var v = map.Vertices[vertices[i]];
                    cellPath.LineTo((float)v.Point.X, (float)v.Point.Y);
                }
                cellPath.Close();

                canvas.DrawPath(cellPath, maskPaint);
            }
        }

        public static void RenderBiomes(SKCanvas canvas, MapData map, MapPack pack)
        {
            if (pack?.Cells == null || map?.Cells == null) return;

            var biomeDefs = BiomModule.GetDefaultBiomes();
            using var fillPaint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };

            // Iterate through the PACK cells (where BiomeId lives)
            foreach (var packCell in pack.Cells)
            {
                // 1. Skip Water
                if (packCell.Height < MapConstants.LAND_THRESHOLD) continue;

                // 2. Map back to the GRID cell to get the physical geometry
                // packCell.GridId is the index of the corresponding cell in _map.Cells
                if (packCell.GridId < 0 || packCell.GridId >= map.Cells.Length) continue;
                var gridCell = map.Cells[packCell.GridId];

                // 3. Lookup Biome Color
                int biomeId = packCell.BiomeId;
                if (biomeId < 0 || biomeId >= biomeDefs.Count) continue;

                if (SKColor.TryParse(biomeDefs[biomeId].Color, out var color))
                {
                    // We use a slight transparency so the underlying heightmap shading 
                    // from RenderHeightmap creates a "3D" effect.
                    fillPaint.Color = color.WithAlpha(170);
                }

                // 4. Draw the geometry from the GRID cell
                using var path = CreateCellPath(gridCell, map.Vertices);
                canvas.DrawPath(path, fillPaint);
            }
        }

        public static void RenderTemperature(SKCanvas canvas, MapData map)
        {
            using var tempPaint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };

            for (int i = 0; i < map.Cells.Length; i++)
            {
                var cell = map.Cells[i];
                if (cell.Verticies == null || cell.Verticies.Count < 3) continue;

                // Map temperature to a 0-1 range for a gradient. 
                // We'll assume a range of -20°C (Green/Cool) to 40°C (Red/Hot)
                float t = (cell.Temp + 20) / 60f;
                t = Math.Clamp(t, 0, 1);

                // Simple Green (cool) to Red (hot) interpolation
                byte r = (byte)(t * 255);
                byte g = (byte)((1 - t) * 255);
                tempPaint.Color = new SKColor(r, g, 50, 180); // Semi-transparent

                using var path = CreateCellPath(cell, map.Vertices);
                canvas.DrawPath(path, tempPaint);
            }
        }

        public static void RenderPrecipitation(SKCanvas canvas, MapData map)
        {
            using var precPaint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                Color = new SKColor(100, 150, 255, 200),
                IsAntialias = true
            };

            for (int i = 0; i < map.Cells.Length; i++)
            {
                var cell = map.Cells[i];

                // Filter: Only render if it's land and has precipitation
                if (cell.Height < MapConstants.LAND_THRESHOLD || cell.Prec == 0) continue;

                float radius = (float)(Math.Sqrt(cell.Prec) * 0.85);

                canvas.DrawCircle((float)cell.Point.X, (float)cell.Point.Y, radius, precPaint);
            }
        }

        public static void RenderCultures(SKCanvas canvas, MapPack pack)
        {
            // We'll reuse the paint object for performance, just changing the color
            using var culturePaint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                IsAntialias = false // Antialiasing can cause thin seams between cells; false is often better for Voronoi fills
            };

            var cells = pack.Cells;
            var cultures = pack.Cultures;

            for (int i = 0; i < cells.Length; i++)
            {
                var cell = cells[i];

                // Skip cells with no assigned culture (Wildlands)
                if (cell.CultureId == 0) continue;
                if (cell.Verticies == null || cell.Verticies.Count < 3) continue;

                // Retrieve the culture color. 
                // Note: 'Color' is usually stored as a hex string or an object in the pack.
                // I'll assume your Culture object has an SKColor or can provide one.
                var culture = cultures[cell.CultureId];

                // Example: If culture.Color is a string like "#ff0000"
                culturePaint.Color = SKColor.Parse(culture.Color).WithAlpha(180);

                using var path = CreateCellPath(cell, pack.Vertices);
                canvas.DrawPath(path, culturePaint);
            }
        }

        public static void RenderRivers(SKCanvas canvas, MapPack pack)
        {
            if (pack?.Rivers == null) return;

            using var riverFill = new SKPaint
            {
                Color = new SKColor(49, 116, 173),
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };

            foreach (var river in pack.Rivers)
            {
                // 1. Get the meandered points (X, Y, Flux) from your module
                // We pass the pack to resolve Flux and the river's list of Cell IDs
                var meanderedData = RiverModule.AddMeandering(pack, river.Cells);

                // 2. Generate the polygon coordinates using our domain logic
                // This returns a List<MapPoint> forming a closed loop
                var polygonPoints = RiverModule.GetRiverPolygon(meanderedData, river.WidthFactor, river.SourceWidth);

                using var path = CreateRiverPath(polygonPoints);

                // 4. Render as a filled shape
                canvas.DrawPath(path, riverFill);
            }
        }

        public static void RenderRiversSimple(SKCanvas canvas, MapData map, MapPack pack)
        {
            if (pack?.Rivers == null || map?.Points == null) return;

            // Adjust this value to make rivers thicker or thinner globally
            // Start with 2.0f or 3.0f and adjust to your preference.
            float riverScale = 5.5f;
            float minVisibleWidth = 2.5f;

            using var paint = new SKPaint
            {
                Color = new SKColor(49, 116, 173),
                Style = SKPaintStyle.Stroke,
                StrokeJoin = SKStrokeJoin.Round,
                StrokeCap = SKStrokeCap.Round,
                IsAntialias = true
            };

            foreach (var river in pack.Rivers)
            {
                if (river.Cells == null || river.Cells.Count < 2) continue;

                for (int i = 1; i < river.Cells.Count; i++)
                {
                    var cell = pack.Cells[river.Cells[i]];
                    var prevCell = pack.Cells[river.Cells[i - 1]];

                    var p1X = (float)map.Points[prevCell.GridId].X;
                    var p1Y = (float)map.Points[prevCell.GridId].Y;
                    var p2X = (float)map.Points[cell.GridId].X;
                    var p2Y = (float)map.Points[cell.GridId].Y;

                    // Interpolate base width
                    float t = (float)i / (river.Cells.Count - 1);
                    float baseWidth = (float)(river.SourceWidth + (river.Width - river.SourceWidth) * t);

                    // Apply scaling and ensure it doesn't disappear
                    paint.StrokeWidth = Math.Max(minVisibleWidth, baseWidth * riverScale);

                    canvas.DrawLine(p1X, p1Y, p2X, p2Y, paint);
                }
            }
        }

        public static void RenderShoreline(SKCanvas canvas, MapPack pack)
        {
            if (pack?.Features == null) return;

            using var linePaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 3f,
                IsAntialias = true,
                StrokeJoin = SKStrokeJoin.Round
            };

            foreach (var feature in pack.Features)
            {
                var vertices = feature.ShorelineVertices;
                if (vertices == null || vertices.Count < 3) continue;

                // Draw the segments with the traffic light gradient
                for (int i = 0; i < vertices.Count - 1; i++)
                {
                    var p1 = pack.Vertices[vertices[i]].Point;
                    var p2 = pack.Vertices[vertices[i + 1]].Point;

                    float progress = (float)i / vertices.Count;
                    linePaint.Color = GetTrafficLightColor(progress);

                    canvas.DrawLine((float)p1.X, (float)p1.Y, (float)p2.X, (float)p2.Y, linePaint);
                }

                // --- THE CLOSING SEGMENT ---
                // Draw the final link from the end of the list back to the start
                var pStart = pack.Vertices[vertices[0]].Point;
                var pEnd = pack.Vertices[vertices[vertices.Count - 1]].Point;

                // We use a distinct color (White) for the closing link to verify closure
                linePaint.Color = SKColors.White;
                linePaint.StrokeWidth = 2f; // Slightly thinner to see the "snap"
                canvas.DrawLine((float)pEnd.X, (float)pEnd.Y, (float)pStart.X, (float)pStart.Y, linePaint);

                // Draw a Bright White Circle at the start
                using var startPaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill };
                canvas.DrawCircle((float)pStart.X, (float)pStart.Y, 5f, startPaint);

                // Draw a small Red dot at the very end of the list
                // If the red dot and white circle are in the same place, the data is perfect
                startPaint.Color = SKColors.Red;
                canvas.DrawCircle((float)pEnd.X, (float)pEnd.Y, 3f, startPaint);
            }
        }

        public static SKColor GetTrafficLightColor(float progress)
        {
            if (progress < 0.5f) // Green to Yellow
                return new SKColor((byte)(progress * 2 * 255), 255, 0);
            else // Yellow to Red
                return new SKColor(255, (byte)((1 - (progress - 0.5f) * 2) * 255), 0);
        }

        // Helper to keep the rendering loops clean
        public static SKPath CreateCellPath(MapCell cell, MapVertex[] vertices)
        {
            var path = new SKPath();
            var v0 = vertices[cell.Verticies[0]].Point;
            path.MoveTo((float)v0.X, (float)v0.Y);

            for (int j = 1; j < cell.Verticies.Count; j++)
            {
                var v = vertices[cell.Verticies[j]].Point;
                path.LineTo((float)v.X, (float)v.Y);
            }
            path.Close();
            return path;
        }

        public static SKPath CreateRiverPath(List<MapPoint> polygonPoints)
        {
            var path = new SKPath();
            if (polygonPoints.Count > 0)
            {
                path.MoveTo((float)polygonPoints[0].X, (float)polygonPoints[0].Y);

                for (int i = 1; i < polygonPoints.Count - 1; i++)
                {
                    var current = polygonPoints[i];
                    var next = polygonPoints[i + 1];

                    // Calculate midpoint between current and next
                    float midX = (float)(current.X + next.X) / 2;
                    float midY = (float)(current.Y + next.Y) / 2;

                    // Use current point as control point, and midpoint as the end
                    path.QuadTo((float)current.X, (float)current.Y, midX, midY);
                }

                path.LineTo((float)polygonPoints[^1].X, (float)polygonPoints[^1].Y);
                path.Close();
            }

            return path;
        }
    }
}
