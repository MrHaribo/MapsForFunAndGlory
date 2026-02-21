using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SharpVoronoiLib;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MapGen.Render.Skia.WinUI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private readonly Random _rng = new Random();

        private List<SKPoint> _samples = new();

        private Vector2 _size = new Vector2(1600, 800);
        private const float _sampleDistance = 5;

        private List<VoronoiSite> _sites;
        private List<VoronoiEdge> _edges;

        //private MapContext _mapContext;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
            //var options = new GenerationOptions
            //{
            //    Seed = 12345,
            //    MapWidth = 800,
            //    MapHeight = 600,
            //    PointCount = 2000
            //};

            //var gen = new MapGenerator();
            //_mapContext = gen.GenerateAsync(options).Result;


            //GenerateSamples();

            //GenerateVoronoi();
        }

        private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            SKCanvas canvas = e.Surface.Canvas;

            canvas.Clear(SKColors.Transparent);

            // Grid
            //DrawVoronoi(canvas, _mapContext.Grid);

            // Draw some text
            string text = "SkiaSharp on WinUI";
            var paint = new SKPaint
            {
                Color = SKColors.Gold,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2,
                TextAlign = SKTextAlign.Center,
                TextSize = 58
            };
            var bounds = new SKRect();
            paint.MeasureText(text, ref bounds);
            var coord = new SKPoint(e.Info.Width / 2, (e.Info.Height + bounds.Height) / 2); // Origin = Bottom Left
            canvas.DrawText(text, coord, paint);
        }

        //private void DrawVoronoi(SKCanvas canvas, GridData grid)
        //{
        //    using var path = new SKPath();

        //    for (int i = 0; i < grid.VoronoiCells.Count; i++)
        //    {
        //        var cell = grid.VoronoiCells[i];

        //        // Skip empty cells (can happen at boundaries)
        //        if (cell.VertexIndices.Count < 3)
        //            continue;

        //        // Get the site center
        //        var site = grid.Points[cell.Index];
        //        var cx = site.X;
        //        var cy = site.Y;

        //        // Convert vertex indices → actual vertex positions
        //        var verts = cell.VertexIndices
        //            .Select(idx => grid.VoronoiVertices[idx])
        //            .ToList();

        //        // Sort vertices by angle around the site
        //        var sorted = verts
        //            .OrderBy(v => Math.Atan2(v.Y - cy, v.X - cx))
        //            .Select(v => new SKPoint(v.X, v.Y))
        //            .ToArray();

        //        // Build path
        //        path.Reset();
        //        path.MoveTo(sorted[0]);
        //        for (int p = 1; p < sorted.Length; p++)
        //            path.LineTo(sorted[p]);
        //        path.Close();

        //        // Fill polygon
        //        canvas.DrawPath(path, new SKPaint
        //        {
        //            Style = SKPaintStyle.Fill,
        //            Color = GetRandomColor(),
        //            IsAntialias = true
        //        });
        //    }
        //}


        //public void RenderVoronoi(SKCanvas canvas, GridData grid)
        //{
        //    using var paint = new SKPaint
        //    {
        //        Color = SKColors.Black,
        //        StrokeWidth = 1,
        //        IsStroke = true
        //    };

        //    // Draw edges
        //    foreach (var cell in grid.VoronoiCells)
        //    {
        //        var verts = cell.VertexIndices
        //            .Select(i => grid.VoronoiVertices[i])
        //            .ToList();

        //        for (int i = 0; i < verts.Count; i++)
        //        {
        //            var a = verts[i];
        //            var b = verts[(i + 1) % verts.Count];

        //            canvas.DrawLine(a.X, a.Y, b.X, b.Y, paint);
        //        }
        //    }

        //    // Draw points
        //    using var pointPaint = new SKPaint
        //    {
        //        Color = SKColors.Red,
        //        IsStroke = false
        //    };

        //    foreach (var p in grid.Points)
        //    {
        //        canvas.DrawCircle(p.X, p.Y, 2, pointPaint);
        //    }
        //}


        //private void GenerateSamples()
        //{

        //    var samples = UniformPoissonDiskSampler.SampleRectangle(Vector2.Zero, _size, _sampleDistance).Select(x => new SKPoint(x.X, x.Y));

        //    foreach (var sample in samples)
        //    {
        //        _samples.Add(sample);
        //    }
        //}

        //private void GenerateVoronoi()
        //{
        //    var sw = Stopwatch.StartNew();

        //    _sites = _samples.Select(s => new VoronoiSite(s.X, s.Y)).ToList();
        //    _edges = VoronoiPlane.TessellateOnce(_sites, 0, 0, _size.X, _size.Y);

        //    Trace.WriteLine("SharpVoronoiLib " + sw.ElapsedMilliseconds);
        //    sw.Stop();
        //}

        //private void OnPaintSurface2(object sender, SKPaintSurfaceEventArgs e)
        //{
        //    SKCanvas canvas = e.Surface.Canvas;

        //    canvas.Clear(SKColors.Transparent);

        //    // Draw some text
        //    string text = "SkiaSharp on WinUI";
        //    var paint = new SKPaint
        //    {
        //        Color = SKColors.Gold,
        //        IsAntialias = true,
        //        Style = SKPaintStyle.Stroke,
        //        StrokeWidth = 2,
        //        TextAlign = SKTextAlign.Center,
        //        TextSize = 58
        //    };
        //    var bounds = new SKRect();
        //    paint.MeasureText(text, ref bounds);
        //    var coord = new SKPoint(e.Info.Width / 2, (e.Info.Height + bounds.Height) / 2); // Origin = Bottom Left
        //    canvas.DrawText(text, coord, paint);


        //    DrawVoronoi(canvas);




        //    DrawSamples(canvas);
        //}

        //private void DrawVoronoi(SKCanvas canvas)
        //{
        //    using SKPath path = new();

        //    var seen = new HashSet<int>();

        //    foreach (var site in _sites)
        //    {
        //        var color = GetRandomColor();

        //        var points = site.Points
        //            .OrderBy(p => Math.Atan2(p.Y - site.Y, p.X - site.X))
        //            .Select(v => new SKPoint((float)v.X, (float)v.Y))
        //            .ToArray();

        //        path.Reset();
        //        path.MoveTo(points[0]);
        //        for (int i = 1; i < points.Length; i++)
        //        {
        //            path.LineTo(points[i]);
        //        }
        //        path.Close();

        //        canvas.DrawPath(path, new SKPaint
        //        {
        //            Style = SKPaintStyle.Fill,
        //            Color = color,
        //            IsAntialias = true
        //        });
        //    }
        //}

        private void DrawSamples(SKCanvas canvas)
        {
            var points = _sites.Select(v => new SKPoint((float)v.X, (float)v.Y)).ToArray();
            canvas.DrawPoints(SKPointMode.Points, points, new SKPaint
            {
                Style = SKPaintStyle.Fill,
                Color = new SKColor(0, 0, 0),
                IsAntialias = true
            });
        }

        private SKColor GetRandomColor()
        {
            var rgb = new byte[3];
            _rng.NextBytes(rgb);
            return new SKColor(rgb[0], rgb[1], rgb[2]);
        }
    }
}
