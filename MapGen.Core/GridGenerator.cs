using MapGen.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

public static class GridGenerator
{
    private static double Round(double val, int precision = 2) =>
        Math.Round(val, precision, MidpointRounding.AwayFromZero);

    public static void PlacePoints(MapData data, GenerationOptions options, IRandom rng)
    {
        // Spacing is calculated once and stored
        data.Spacing = Round(Math.Sqrt((options.Width * options.Height) / (double)options.PointsCount), 2);

        double radius = data.Spacing / 2.0;
        double jittering = radius * 0.9;
        double doubleJittering = jittering * 2.0;

        data.CellsCountX = (int)Math.Floor((options.Width + 0.5 * data.Spacing - 1e-10) / data.Spacing);
        data.CellsCountY = (int)Math.Floor((options.Height + 0.5 * data.Spacing - 1e-10) / data.Spacing);
        data.CellsCount = data.CellsCountX * data.CellsCountY;

        var dataX = new List<double>();
        var dataY = new List<double>();

        for (int j = 0; j < data.CellsCountY; j++)
        {
            double yBase = Round(radius + (j * data.Spacing), 2);
            for (int i = 0; i < data.CellsCountX; i++)
            {
                double xBase = Round(radius + (i * data.Spacing), 2);

                // RNG consumption: X then Y
                double xj = Math.Min(Round(xBase + (rng.Next() * doubleJittering - jittering), 2), (double)options.Width);
                double yj = Math.Min(Round(yBase + (rng.Next() * doubleJittering - jittering), 2), (double)options.Height);

                dataX.Add(xj);
                dataY.Add(yj);
            }
        }

        data.X = dataX.ToArray();
        data.Y = dataY.ToArray();
    }

    public static void PlaceBoundaryPoints(MapData data, int width, int height)
    {
        double s = data.Spacing;

        // JS: const offset = rn(-1 * spacing); 
        // Defaulting to 0 precision (integer round) matches your "Expected: -14"
        double offset = Round(-1 * s, 0);

        double bSpacing = s * 2;
        double w = width - offset * 2;
        double h = height - offset * 2;

        int numberX = (int)Math.Ceiling(w / bSpacing) - 1;
        int numberY = (int)Math.Ceiling(h / bSpacing) - 1;

        var bX = new List<double>();
        var bY = new List<double>();

        for (double i = 0.5; i < numberX; i++)
        {
            // Use double math for the division to avoid truncation before Ceiling
            double x = Math.Ceiling((w * i) / (double)numberX + offset);
            bX.Add(x); bY.Add(offset);
            bX.Add(x); bY.Add(h + offset);
        }

        for (double i = 0.5; i < numberY; i++)
        {
            double y = Math.Ceiling((h * i) / (double)numberY + offset);
            bX.Add(offset); bY.Add(y);
            bX.Add(w + offset); bY.Add(y);
        }

        data.BoundaryX = bX.ToArray();
        data.BoundaryY = bY.ToArray();
    }
}