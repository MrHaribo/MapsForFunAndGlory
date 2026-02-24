using MapGen.Core;
using MapGen.Core.Helpers;
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
            };

            var rng = new AleaRandom(options.Seed);
            var generator = new MapGenerator();
            generator.Generate(options, rng);

            rng = new AleaRandom(options.Seed);
            HeightmapGenerator.Generate(generator.Data, HeightmapTemplates.Continents, rng);

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
            if (_map == null || _map.Cells == null) return;

            // 1. Calculate Scaling to fit MapData into current Canvas
            float scaleX = (float)canvas.LocalClipBounds.Width / _map.Width;
            float scaleY = (float)canvas.LocalClipBounds.Height / _map.Height;
            float finalScale = Math.Min(scaleX, scaleY); // Maintain aspect ratio

            canvas.Save();
            canvas.Scale(finalScale);

            using var fillPaint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };

            for (int i = 0; i < _map.Cells.Length; i++)
            {
                var cell = _map.Cells[i];
                if (cell.V == null || cell.V.Count < 3) continue;

                // Determine Color
                byte h = cell.H;
                if (h < 20)
                {
                    byte blueDepth = (byte)(100 + (h * 5));
                    fillPaint.Color = new SKColor(30, 60, blueDepth);
                }
                else
                {
                    byte landBrightness = (byte)Math.Clamp((h - 20) * 3 + 50, 0, 255);
                    fillPaint.Color = new SKColor(landBrightness, landBrightness, landBrightness);
                }

                // Draw Path
                using var path = new SKPath();
                var v0 = _map.Vertices[cell.V[0]].P;
                path.MoveTo((float)v0.X, (float)v0.Y);

                for (int j = 1; j < cell.V.Count; j++)
                {
                    var v = _map.Vertices[cell.V[j]].P;
                    path.LineTo((float)v.X, (float)v.Y);
                }
                path.Close();

                canvas.DrawPath(path, fillPaint);
            }

            canvas.Restore(); // Reset transform for other UI elements
        }
    }
}
