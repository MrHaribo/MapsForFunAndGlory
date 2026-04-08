using MapGen.Core;
using MapGen.Core.Helpers;
using MapGen.Core.Modules;
using Microsoft.UI.Xaml;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using System;
using System.Diagnostics;
using Windows.Graphics;
using CommunityToolkit.Mvvm.ComponentModel;

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
        private MapPack _detailPack;

        public MainWindowViewModel ViewModel { get; } = new();

        public MainWindow()
        {
            InitializeComponent();

            AppWindow.MoveAndResize(new RectInt32(100, 100, 2400, 1200));
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
            GenerateMap();
        }

        private void GenerateMap()
        {
            var seed = string.IsNullOrEmpty(ViewModel.Seed) ? new Random().Next().ToString() : ViewModel.Seed;

            var mapOptions = new MapOptions
            {
                Seed = seed,
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

            var sw = Stopwatch.StartNew();

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


            //var pack = PackModule.ReGraph(mapData);
            //FeatureModule.MarkupPack(pack);
            //RiverModule.Generate(pack, mapData, allowErosion: true);
            //BiomModule.Define(pack, mapData);


            Trace.WriteLine("Pack generated " + sw.ElapsedMilliseconds);
            sw.Restart();

            _map = mapData;

            var pack = PackModule.ReGraph(mapData);

            Trace.WriteLine("ReGraph " + sw.ElapsedMilliseconds);
            sw.Restart();

            FeatureModule.MarkupPack(pack);

            Trace.WriteLine("MarkupPack " + sw.ElapsedMilliseconds);
            sw.Restart();

            RiverModule.Generate(pack, mapData, allowErosion: true);

            Trace.WriteLine("RiverModule " + sw.ElapsedMilliseconds);
            sw.Restart();

            BiomModule.Define(pack, mapData);

            Trace.WriteLine("BiomModule " + sw.ElapsedMilliseconds);
            sw.Restart();

            FeatureModule.DefineGroups(pack);
            FeatureModule.RankCells(pack);
            CultureModule.Generate(pack, mapData, 9);
            CultureModule.ExpandCultures(pack);

            _pack = pack;

            Trace.WriteLine("CultureModule " + sw.ElapsedMilliseconds);
            sw.Restart();

            pack = PackModule.RefineRivers(pack, mapData);
            FeatureModule.MarkupPack(pack);
            RiverModule.Generate(pack, mapData, allowErosion: true);
            BiomModule.Define(pack, mapData);

            Trace.WriteLine("Pack Detail " + sw.ElapsedMilliseconds);
            sw.Restart();



            _detailPack = pack;
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

            if (ViewModel.ShowGridHeightmap)
                MapGenRenderer.RenderGridHeightmap(canvas, _map);

            if (ViewModel.ShowPackHeightmap)
                MapGenRenderer.RenderPackHeightmap(canvas, _pack);

            if (ViewModel.ShowDetailPackHeightmap)
                MapGenRenderer.RenderPackHeightmap(canvas, _detailPack);

            if (ViewModel.ShowBiomes)
                MapGenRenderer.RenderBiomes(canvas, _map, _pack);

            if (ViewModel.ShowPrecipitation)
                MapGenRenderer.RenderPrecipitation(canvas, _map);

            if (ViewModel.ShowTemperature)
                MapGenRenderer.RenderTemperature(canvas, _map);

            if (ViewModel.ShowShoreline)
                MapGenRenderer.RenderShoreline(canvas, _pack);

            if (ViewModel.ShowRivers)
                MapGenRenderer.RenderRivers(canvas, _pack);

            if (ViewModel.ShowCultures)
                MapGenRenderer.RenderCultures(canvas, _pack);


            //// 2. Start a new layer for the clipped rivers
            //// We use a SaveLayer so the blending only affects the rivers and the mask
            //canvas.SaveLayer();

            //// 3. Draw the Land Mask (The "Destination" for the blend)
            //MapGenRenderer.RenderLandMask(canvas, _map);

            //if (ViewModel.ShowRivers)
            //{
            //    // 4. Draw the Rivers using SrcIn blend mode
            //    // This tells Skia: "Only keep the River pixels that overlap with the Land Mask"
            //    using (var paint = new SKPaint { BlendMode = SKBlendMode.SrcIn })
            //    {
            //        canvas.SaveLayer(paint);
            //        MapGenRenderer.RenderRivers(canvas, _pack);
            //        canvas.Restore();
            //    }
            //}

            //// 5. Cleanup the layers
            //canvas.Restore(); // Restore from the initial SaveLayer

            //canvas.Restore(); // Restore the scale/transform
        }

        private void Show_Click(object sender, RoutedEventArgs e)
        {
            SkiaCanvas.Invalidate();
        }

        private void Generate_Click(object sender, RoutedEventArgs e)
        {
            GenerateMap();
            SkiaCanvas.Invalidate();
        }
    }
}
