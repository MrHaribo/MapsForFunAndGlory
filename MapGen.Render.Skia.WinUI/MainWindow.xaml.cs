using MapGen.Core;
using Microsoft.UI.Xaml;
using SkiaSharp;
using SkiaSharp.Views.Windows;

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
                Seed = "azgaar",
                Width = 1920,
                Height = 1080,
                PointsCount = 9975,
                FixedSpacing = 14.4,
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
            if (_map == null) return;

            // Paints for the different elements
            using var cellStroke = new SKPaint
            {
                Color = new SKColor(100, 100, 100, 80), // Semi-transparent gray
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1,
                IsAntialias = true
            };

            using var borderStroke = new SKPaint
            {
                Color = SKColors.Red.WithAlpha(120),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.5f,
                IsAntialias = true
            };

            using var sitePaint = new SKPaint
            {
                Color = SKColors.SteelBlue,
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };

            // Iterate through only the playable cells (PointsCount)
            for (int i = 0; i < _map.PointsCount; i++)
            {
                var vertexIndices = _map.Cells.V[i];

                // Safety check: Azgaar's cells usually have 3-8 vertices
                if (vertexIndices == null || vertexIndices.Count < 3) continue;

                using var cellPath = new SKPath();

                // Move to the first vertex of the cell
                var firstVertex = _map.Vertices.P[vertexIndices[0]];
                cellPath.MoveTo((float)firstVertex.X, (float)firstVertex.Y);

                // Draw lines to the remaining vertices
                for (int j = 1; j < vertexIndices.Count; j++)
                {
                    var vertex = _map.Vertices.P[vertexIndices[j]];
                    cellPath.LineTo((float)vertex.X, (float)vertex.Y);
                }

                cellPath.Close();

                // Use a different color if the cell is on the map boundary
                bool isBorder = _map.Cells.B[i] == 1;
                canvas.DrawPath(cellPath, isBorder ? borderStroke : cellStroke);

                // Optional: Draw a tiny dot at the cell's center (site)
                // Helps visualize the "jitter" we worked so hard to sync
                canvas.DrawCircle((float)_map.X[i], (float)_map.Y[i], 1.0f, sitePaint);
            }
        }
    }
}
