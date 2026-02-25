using MapGen.Core.Helpers;
using System;
using System.Linq;

namespace MapGen.Core.Modules
{
    public static class GlobeModule
    {
        public static void DefineMapSize(MapData data, IRandom rng)
        {
            var (size, lat, lon) = GetSizeAndLatitude(data, rng);

            data.MapSize = size;
            data.Latitude = lat;
            data.Longitude = lon;
        }

        private static (double size, double lat, double lon) GetSizeAndLatitude(MapData data, IRandom rng)
        {
            bool touchesBorder = data.Cells.Any(c => c.B == 1 && c.H >= MapConstants.LAND_THRESHOLD);
            double max = touchesBorder ? 80 : 100;

            // Shared Latitude logic: 40% or 60% base with gaussian shift
            double GetLat() => rng.Gauss(rng.P(0.5) ? 40 : 60, 20, 25, 75);

            // 1. Check for 100% size special cases if land doesn't touch the border
            if (!touchesBorder)
            {
                var result = data.Template switch
                {
                    HeightmapTemplate.Pangea => (100.0, 50.0, 50.0),
                    HeightmapTemplate.Shattered when rng.P(0.7) => (100.0, 50.0, 50.0),
                    HeightmapTemplate.Continents when rng.P(0.5) => (100.0, 50.0, 50.0),
                    HeightmapTemplate.Archipelago when rng.P(0.35) => (100.0, 50.0, 50.0),
                    HeightmapTemplate.HighIsland when rng.P(0.25) => (100.0, 50.0, 50.0),
                    HeightmapTemplate.LowIsland when rng.P(0.1) => (100.0, 50.0, 50.0),
                    _ => (0.0, 0.0, 0.0) // Sentinel for no-match
                };

                if (result.Item1 > 0) return result;
            }

            // 2. Standard template distributions
            return data.Template switch
            {
                HeightmapTemplate.Pangea => (rng.Gauss(70, 20, 30, max), GetLat(), 50),
                HeightmapTemplate.Volcano => (rng.Gauss(20, 20, 10, max), GetLat(), 50),
                HeightmapTemplate.Atoll => (rng.Gauss(3, 2, 1, 5), GetLat(), 50),
                _ => (rng.Gauss(30, 20, 15, max), GetLat(), 50) // Default for Continents, Archipelago, Islands
            };
        }

        public static void CalculateMapCoordinates(MapData data)
        {
            double sizeFraction = data.MapSize / 100.0;
            double latShift = data.Latitude / 100.0;
            double lonShift = data.Longitude / 100.0;

            // Total vertical span (100% = 180 degrees)
            double latT = Math.Round(sizeFraction * 180, 1);
            // Northern bound
            double latN = Math.Round(90 - (180 - latT) * latShift, 1);
            // Southern bound
            double latS = Math.Round(latN - latT, 1);

            // Calculate longitude span based on map aspect ratio
            double aspectRatio = (double)data.Width / data.Height;
            double lonT = Math.Round(Math.Min(aspectRatio * latT, 360), 1);
            // Eastern bound
            double lonE = Math.Round(180 - (360 - lonT) * lonShift, 1);
            // Western bound
            double lonW = Math.Round(lonE - lonT, 1);

            data.Coords = new MapCoordinates
            {
                LatT = latT,
                LatN = latN,
                LatS = latS,
                LonT = lonT,
                LonW = lonW,
                LonE = lonE
            };
        }
    }
}
