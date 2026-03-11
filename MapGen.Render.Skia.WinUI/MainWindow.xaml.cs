using MapGen.Core;
using MapGen.Core.Helpers;
using MapGen.Core.Modules;
using Microsoft.UI.Xaml;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MapGen.Render.Skia.WinUI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private MapData _map;
        private MapPack _pack;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
            var mapOptions = new MapOptions
            {
                //Seed = "42",
                //Seed = "1114678237",
                Seed = new Random().Next().ToString(),
                Width = 1920,
                Height = 1080,
                PointsCount = 2000,
            };

            var rng = new AleaRandom(mapOptions.Seed);
            MapOptions.RandomizeOptions(mapOptions, rng);
            mapOptions.Template = HeightmapTemplate.Continents;

            var mapData = new MapData
            {
                Options = mapOptions,
                Rng = rng,
            };

            GridGenerator.Generate(mapData);
            VoronoiGenerator.CalculateVoronoi(mapData);
            HeightmapGenerator.Generate(mapData);
            FeatureModule.MarkupGrid(mapData);
            LakeModule.AddLakesInDeepDepressions(mapData);
            LakeModule.OpenNearSeaLakes(mapData);
            GlobeModule.DefineMapSize(mapData);
            GlobeModule.CalculateMapCoordinates(mapData);
            ClimateModule.CalculateTemperatures(mapData);
            ClimateModule.GeneratePrecipitation(mapData);

            _map = mapData;

            var pack = PackModule.ReGraph(mapData);
            FeatureModule.MarkupPack(pack);
            RiverModule.Generate(pack, mapData, allowErosion: true);
            BiomModule.Define(pack, mapData);

            _pack = pack;
        }

        private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            SKCanvas canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.Transparent);

            if (_map == null || _map.Cells == null) return;

            float scaleX = (float)canvas.LocalClipBounds.Width / _map.Width;
            float scaleY = (float)canvas.LocalClipBounds.Height / _map.Height;
            float finalScale = Math.Min(scaleX, scaleY);

            canvas.Save();
            canvas.Scale(finalScale);

            // 1. Render the base layers (Heightmap, etc.)
            //RenderHeightmap(canvas);
            RenderPackHeightmap(canvas);
            //RenderBiomes(canvas);
            //RenderPrecipitation(canvas);
            RenderShoreline(canvas);

            // 2. Start a new layer for the clipped rivers
            // We use a SaveLayer so the blending only affects the rivers and the mask
            canvas.SaveLayer();

            // 3. Draw the Land Mask (The "Destination" for the blend)
            RenderLandMask(canvas);

            // 4. Draw the Rivers using SrcIn blend mode
            // This tells Skia: "Only keep the River pixels that overlap with the Land Mask"
            using (var paint = new SKPaint { BlendMode = SKBlendMode.SrcIn })
            {
                canvas.SaveLayer(paint);
                RenderRivers(canvas);
                canvas.Restore();
            }

            // 5. Cleanup the layers
            canvas.Restore(); // Restore from the initial SaveLayer

            canvas.Restore(); // Restore the scale/transform
        }

        private void RenderHeightmap(SKCanvas canvas)
        {
            using var fillPaint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };

            for (int i = 0; i < _map.Cells.Length; i++)
            {
                var cell = _map.Cells[i];
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

                using var path = CreateCellPath(cell);
                canvas.DrawPath(path, fillPaint);
            }
        }

        private void RenderPackHeightmap(SKCanvas canvas)
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

            foreach (var cell in _pack.Cells)
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
                var firstV = _pack.Vertices[cell.Verticies[0]];
                path.MoveTo((float)firstV.Point.X, (float)firstV.Point.Y);

                for (int i = 1; i < cell.Verticies.Count; i++)
                {
                    var v = _pack.Vertices[cell.Verticies[i]];
                    path.LineTo((float)v.Point.X, (float)v.Point.Y);
                }
                path.Close();

                // 4. Draw the cell
                canvas.DrawPath(path, fillPaint);

                // Optional: Draw cell borders to see if they overlap or have gaps
                canvas.DrawPath(path, strokePaint);
            }
        }

        private void RenderLandMask(SKCanvas canvas)
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
            foreach (var cell in _map.Cells)
            {
                if (cell.Height < 20) continue; // Skip water

                // Draw the polygon for this cell
                using var cellPath = new SKPath();
                var vertices = cell.Verticies; // Using your cell model's Vertex indices
                if (vertices == null || vertices.Count == 0) continue;

                // Assuming _map.Vertices contains MapPoint coordinates
                var startV = _map.Vertices[vertices[0]];
                cellPath.MoveTo((float)startV.Point.X, (float)startV.Point.Y);

                for (int i = 1; i < vertices.Count; i++)
                {
                    var v = _map.Vertices[vertices[i]];
                    cellPath.LineTo((float)v.Point.X, (float)v.Point.Y);
                }
                cellPath.Close();

                canvas.DrawPath(cellPath, maskPaint);
            }
        }

        private void RenderBiomes(SKCanvas canvas)
        {
            if (_pack?.Cells == null || _map?.Cells == null) return;

            var biomeDefs = BiomModule.GetDefaultBiomes();
            using var fillPaint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };

            // Iterate through the PACK cells (where BiomeId lives)
            foreach (var packCell in _pack.Cells)
            {
                // 1. Skip Water
                if (packCell.Height < MapConstants.LAND_THRESHOLD) continue;

                // 2. Map back to the GRID cell to get the physical geometry
                // packCell.GridId is the index of the corresponding cell in _map.Cells
                if (packCell.GridId < 0 || packCell.GridId >= _map.Cells.Length) continue;
                var gridCell = _map.Cells[packCell.GridId];

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
                using var path = CreateCellPath(gridCell);
                canvas.DrawPath(path, fillPaint);
            }
        }

        private void RenderTemperature(SKCanvas canvas)
        {
            using var tempPaint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };

            for (int i = 0; i < _map.Cells.Length; i++)
            {
                var cell = _map.Cells[i];
                if (cell.Verticies == null || cell.Verticies.Count < 3) continue;

                // Map temperature to a 0-1 range for a gradient. 
                // We'll assume a range of -20°C (Green/Cool) to 40°C (Red/Hot)
                float t = (cell.Temp + 20) / 60f;
                t = Math.Clamp(t, 0, 1);

                // Simple Green (cool) to Red (hot) interpolation
                byte r = (byte)(t * 255);
                byte g = (byte)((1 - t) * 255);
                tempPaint.Color = new SKColor(r, g, 50, 180); // Semi-transparent

                using var path = CreateCellPath(cell);
                canvas.DrawPath(path, tempPaint);
            }
        }

        private void RenderPrecipitation(SKCanvas canvas)
        {
            using var precPaint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                Color = new SKColor(100, 150, 255, 200),
                IsAntialias = true
            };

            for (int i = 0; i < _map.Cells.Length; i++)
            {
                var cell = _map.Cells[i];

                // Filter: Only render if it's land and has precipitation
                if (cell.Height < MapConstants.LAND_THRESHOLD || cell.Prec == 0) continue;

                float radius = (float)(Math.Sqrt(cell.Prec) * 0.85);

                canvas.DrawCircle((float)cell.Point.X, (float)cell.Point.Y, radius, precPaint);
            }
        }

        private void RenderRivers(SKCanvas canvas)
        {
            if (_pack?.Rivers == null) return;

            using var riverFill = new SKPaint
            {
                Color = new SKColor(49, 116, 173),
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };

            foreach (var river in _pack.Rivers)
            {
                // 1. Get the meandered points (X, Y, Flux) from your module
                // We pass the pack to resolve Flux and the river's list of Cell IDs
                var meanderedData = RiverModule.AddMeandering(_pack, river.Cells);

                // 2. Generate the polygon coordinates using our domain logic
                // This returns a List<MapPoint> forming a closed loop
                var polygonPoints = RiverModule.GetRiverPolygon(meanderedData, river.WidthFactor, river.SourceWidth);

                using var path = CreateRiverPath(polygonPoints);

                // 4. Render as a filled shape
                canvas.DrawPath(path, riverFill);
            }
        }

        private void RenderRiversSimple(SKCanvas canvas)
        {
            if (_pack?.Rivers == null || _map?.Points == null) return;

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

            foreach (var river in _pack.Rivers)
            {
                if (river.Cells == null || river.Cells.Count < 2) continue;

                for (int i = 1; i < river.Cells.Count; i++)
                {
                    var cell = _pack.Cells[river.Cells[i]];
                    var prevCell = _pack.Cells[river.Cells[i - 1]];

                    var p1X = (float)_map.Points[prevCell.GridId].X;
                    var p1Y = (float)_map.Points[prevCell.GridId].Y;
                    var p2X = (float)_map.Points[cell.GridId].X;
                    var p2Y = (float)_map.Points[cell.GridId].Y;

                    // Interpolate base width
                    float t = (float)i / (river.Cells.Count - 1);
                    float baseWidth = (float)(river.SourceWidth + (river.Width - river.SourceWidth) * t);

                    // Apply scaling and ensure it doesn't disappear
                    paint.StrokeWidth = Math.Max(minVisibleWidth, baseWidth * riverScale);

                    canvas.DrawLine(p1X, p1Y, p2X, p2Y, paint);
                }
            }
        }

        private void RenderShoreline(SKCanvas canvas)
        {
            if (_pack?.Features == null) return;

            using var linePaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 3f,
                IsAntialias = true,
                StrokeJoin = SKStrokeJoin.Round
            };

            foreach (var feature in _pack.Features)
            {
                var vertices = feature.ShorelineVertices;
                if (vertices == null || vertices.Count < 3) continue;

                // Draw the segments with the traffic light gradient
                for (int i = 0; i < vertices.Count - 1; i++)
                {
                    var p1 = _pack.Vertices[vertices[i]].Point;
                    var p2 = _pack.Vertices[vertices[i + 1]].Point;

                    float progress = (float)i / vertices.Count;
                    linePaint.Color = GetTrafficLightColor(progress);

                    canvas.DrawLine((float)p1.X, (float)p1.Y, (float)p2.X, (float)p2.Y, linePaint);
                }

                // --- THE CLOSING SEGMENT ---
                // Draw the final link from the end of the list back to the start
                var pStart = _pack.Vertices[vertices[0]].Point;
                var pEnd = _pack.Vertices[vertices[vertices.Count - 1]].Point;

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

        private SKColor GetTrafficLightColor(float progress)
        {
            if (progress < 0.5f) // Green to Yellow
                return new SKColor((byte)(progress * 2 * 255), 255, 0);
            else // Yellow to Red
                return new SKColor(255, (byte)((1 - (progress - 0.5f) * 2) * 255), 0);
        }

        // Helper to keep the rendering loops clean
        private SKPath CreateCellPath(MapCell cell)
        {
            var path = new SKPath();
            var v0 = _map.Vertices[cell.Verticies[0]].Point;
            path.MoveTo((float)v0.X, (float)v0.Y);

            for (int j = 1; j < cell.Verticies.Count; j++)
            {
                var v = _map.Vertices[cell.Verticies[j]].Point;
                path.LineTo((float)v.X, (float)v.Y);
            }
            path.Close();
            return path;
        }

        private SKPath CreateRiverPath(List<MapPoint> polygonPoints)
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
