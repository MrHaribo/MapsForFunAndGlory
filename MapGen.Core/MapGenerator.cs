using System;
using System.Collections.Generic;
using System.Text;

namespace MapGen.Core
{
    public class MapGenerator
    {
        private MapData _data;
        private IRandom _rng;
        private GenerationOptions _options;

        public MapData Data => _data;

        public void Generate(GenerationOptions options)
        {
            _options = options;

            // 1. Initialize RNG with seed
            _rng = new Alea(_options.Seed);

            // 2. Initialize Data Container
            _data = new MapData(_options.PointsCount, _options.Width, _options.Height);

            // 3. Place Points (Grid Generation)
            GridGenerator.PlacePoints(_data, _options, _rng);

            // 3. Step 2: Edge Boundary Points (automatically uses the same spacing)
            GridGenerator.PlaceBoundaryPoints(_data, _options.Width, _options.Height);

            // 5. Mesh Calculation
            VoronoiGenerator.CalculateVoronoi(_data);

            // 6. Heightmap Generation (Line 647)
            //HeightmapGenerator.Generate(_data, HeightmapTemplate.Test, _rng);

            // 7. Mark Features (Islands/Lakes) (Line 651)
            //MarkFeatures();

            // 8. Precipitation & Hydrology (Line 654)
            //CalculatePrecipitation();
            //CalculateRivers();

            // 9. Cultures & States (Line 665+)
            //PlaceCultures();
            //DefineStates();
        }
    }
}
