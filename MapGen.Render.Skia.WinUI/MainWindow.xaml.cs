using MapGen.Core;
using MapGen.Core.Helpers;
using MapGen.Core.Modules;
using Microsoft.UI.Xaml;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using System;
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
                Seed = "42",
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

            // --- Toggle Layers Here ---
            RenderHeightmap(canvas);
            //RenderTemperature(canvas);
            //RenderPrecipitation(canvas);
            RenderRivers(canvas);

            canvas.Restore();
        }

        private void RenderHeightmap(SKCanvas canvas)
        {
            using var fillPaint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };

            for (int i = 0; i < _map.Cells.Length; i++)
            {
                var cell = _map.Cells[i];
                if (cell.V == null || cell.V.Count < 3) continue;

                byte h = cell.H;
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

        private void RenderTemperature(SKCanvas canvas)
        {
            using var tempPaint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };

            for (int i = 0; i < _map.Cells.Length; i++)
            {
                var cell = _map.Cells[i];
                if (cell.V == null || cell.V.Count < 3) continue;

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
                if (cell.H < MapConstants.LAND_THRESHOLD || cell.Prec == 0) continue;

                var point = _map.Points[cell.Index];
                float radius = (float)(Math.Sqrt(cell.Prec) * 0.85);

                canvas.DrawCircle((float)point.X, (float)point.Y, radius, precPaint);
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

                if (polygonPoints.Count < 3) continue;

                // 3. Construct the SkiaSharp path from the domain points
                using var path = new SKPath();
                path.MoveTo((float)polygonPoints[0].X, (float)polygonPoints[0].Y);

                for (int i = 1; i < polygonPoints.Count; i++)
                {
                    path.LineTo((float)polygonPoints[i].X, (float)polygonPoints[i].Y);
                }

                path.Close();

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

        // Helper to keep the rendering loops clean
        private SKPath CreateCellPath(MapCell cell)
        {
            var path = new SKPath();
            var v0 = _map.Vertices[cell.V[0]].P;
            path.MoveTo((float)v0.X, (float)v0.Y);

            for (int j = 1; j < cell.V.Count; j++)
            {
                var v = _map.Vertices[cell.V[j]].P;
                path.LineTo((float)v.X, (float)v.Y);
            }
            path.Close();
            return path;
        }
    }
}
