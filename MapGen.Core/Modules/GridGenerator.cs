using System;
using System.Collections.Generic;
using static MapGen.Core.Helpers.NumberUtils;

namespace MapGen.Core.Modules
{
    public static class GridGenerator
    {


        public static void PlacePoints(MapData data, GenerationOptions options, IRandom rng)
        {
            data.Spacing = Round(Math.Sqrt((options.Width * options.Height) / (double)options.PointsCount), 2);

            double radius = data.Spacing / 2.0;
            double jittering = radius * 0.9;
            double doubleJittering = jittering * 2.0;

            data.CellsCountX = (int)Math.Floor((options.Width + 0.5 * data.Spacing - 1e-10) / data.Spacing);
            data.CellsCountY = (int)Math.Floor((options.Height + 0.5 * data.Spacing - 1e-10) / data.Spacing);
            data.CellsCount = data.CellsCountX * data.CellsCountY;

            // Use the new struct array
            data.Points = new MapPoint[data.CellsCount];

            int index = 0;
            for (int j = 0; j < data.CellsCountY; j++)
            {
                double yBase = Round(radius + (j * data.Spacing), 2);
                for (int i = 0; i < data.CellsCountX; i++)
                {
                    double xBase = Round(radius + (i * data.Spacing), 2);

                    double xj = Math.Min(Round(xBase + (rng.Next() * doubleJittering - jittering), 2), (double)options.Width);
                    double yj = Math.Min(Round(yBase + (rng.Next() * doubleJittering - jittering), 2), (double)options.Height);

                    data.Points[index] = new MapPoint(xj, yj);
                    index++;
                }
            }
        }

        public static void PlaceBoundaryPoints(MapData data, int width, int height)
        {
            double s = data.Spacing;

            // JS: const offset = rn(-1 * spacing); 
            double offset = Round(-1 * s, 0);

            double bSpacing = s * 2;
            double w = width - offset * 2;
            double h = height - offset * 2;

            int numberX = (int)Math.Ceiling(w / bSpacing) - 1;
            int numberY = (int)Math.Ceiling(h / bSpacing) - 1;

            var boundaryPoints = new List<MapPoint>();

            // Top and Bottom edges
            for (double i = 0.5; i < numberX; i++)
            {
                double x = Math.Ceiling((w * i) / (double)numberX + offset);
                boundaryPoints.Add(new MapPoint(x, offset));      // Top
                boundaryPoints.Add(new MapPoint(x, h + offset));  // Bottom
            }

            // Left and Right edges
            for (double i = 0.5; i < numberY; i++)
            {
                double y = Math.Ceiling((h * i) / (double)numberY + offset);
                boundaryPoints.Add(new MapPoint(offset, y));      // Left
                boundaryPoints.Add(new MapPoint(w + offset, y));  // Right
            }

            data.BoundaryPoints = boundaryPoints.ToArray();
        }
    }
}

