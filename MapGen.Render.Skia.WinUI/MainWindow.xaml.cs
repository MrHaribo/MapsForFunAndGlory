using MapGen.Core;
using Microsoft.UI.Xaml;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using System;

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

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
            var options = new GenerationOptions
            {
                Seed = "42",
                //Seed = "azgaar",
                Width = 1920,
                Height = 1080,
                PointsCount = 2000,
                Jitter = 0.8
            };

            var generator = new MapGenerator();
            generator.Generate(options);
            _map = generator.Data;
        }

        private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            SKCanvas canvas = e.Surface.Canvas;

            canvas.Clear(SKColors.Transparent);

            DrawVoronoi(canvas);
        }

        private void DrawVoronoi(SKCanvas canvas)
        {
            if (_map == null || _map.H == null) return;

            using var strokePaint = new SKPaint
            {
                Color = new SKColor(0, 0, 0, 30), // Very faint edges
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 0.5f,
                IsAntialias = true
            };

            for (int i = 0; i < _map.PointsCount; i++)
            {
                var vertexIndices = _map.Cells.V[i];
                if (vertexIndices == null || vertexIndices.Count < 3) continue;

                // Determine Color based on Height
                byte h = _map.H[i];
                SKColor fillColor;

                if (h < 20)
                {
                    // Ocean: Dark blue to light blue
                    // Scale 0-19 to a blue range
                    byte blueDepth = (byte)(100 + (h * 5));
                    fillColor = new SKColor(30, 60, blueDepth);
                }
                else
                {
                    // Land: Grayscale (or you could do Green/Brown)
                    // Scale 20-100 to 50-250 for better visibility
                    byte landBrightness = (byte)Math.Clamp((h - 20) * 3 + 50, 0, 255);
                    fillColor = new SKColor(landBrightness, landBrightness, landBrightness);
                }

                using var fillPaint = new SKPaint
                {
                    Color = fillColor,
                    Style = SKPaintStyle.Fill,
                    IsAntialias = true
                };

                using var path = new SKPath();
                var v0 = _map.Vertices.P[vertexIndices[0]];
                path.MoveTo((float)v0.X, (float)v0.Y);

                for (int j = 1; j < vertexIndices.Count; j++)
                {
                    var v = _map.Vertices.P[vertexIndices[j]];
                    path.LineTo((float)v.X, (float)v.Y);
                }
                path.Close();

                canvas.DrawPath(path, fillPaint);
                // canvas.DrawPath(path, strokePaint); // Optional: toggle for grid visibility
            }
        }
    }
}
