using MapGen.Core;
using System;
using System.Collections.Generic;

public static class GridGenerator
{
    private static double Rn(double val, int precision = 2) =>
        Math.Round(val, precision, MidpointRounding.AwayFromZero);

    public static void PlacePoints(MapData data, GenerationOptions options, IRandom rng)
    {
        // Spacing is calculated once and stored
        data.Spacing = options.FixedSpacing ??
            Rn(Math.Sqrt((options.Width * options.Height) / (double)options.PointsCount), 2);

        double radius = data.Spacing / 2.0;
        double jittering = radius * 0.9;
        double doubleJittering = jittering * 2.0;

        int cellsX = (int)Math.Floor((options.Width + 0.5 * data.Spacing - 1e-10) / data.Spacing);
        int cellsY = (int)Math.Floor((options.Height + 0.5 * data.Spacing - 1e-10) / data.Spacing);

        int index = 0;
        for (int j = 0; j < cellsY; j++)
        {
            double yBase = Rn(radius + (j * data.Spacing), 2);
            for (int i = 0; i < cellsX; i++)
            {
                if (index >= data.X.Length) break;

                double xBase = Rn(radius + (i * data.Spacing), 2);

                // RNG consumption: X then Y
                double xj = Math.Min(Rn(xBase + (rng.Next() * doubleJittering - jittering), 2), (double)options.Width);
                double yj = Math.Min(Rn(yBase + (rng.Next() * doubleJittering - jittering), 2), (double)options.Height);

                data.X[index] = (float)xj;
                data.Y[index] = (float)yj;
                index++;
            }
        }
        data.PointsCount = index;
    }

    public static void PlaceBoundaryPoints(MapData data, int width, int height)
    {
        double s = data.Spacing;

        // JS: const offset = rn(-1 * spacing); 
        // Defaulting to 0 precision (integer round) matches your "Expected: -14"
        double offset = Rn(-1 * s, 0);

        double bSpacing = s * 2;
        double w = width - offset * 2;
        double h = height - offset * 2;

        int numberX = (int)Math.Ceiling(w / bSpacing) - 1;
        int numberY = (int)Math.Ceiling(h / bSpacing) - 1;

        var bX = new List<float>();
        var bY = new List<float>();

        for (double i = 0.5; i < numberX; i++)
        {
            // Use double math for the division to avoid truncation before Ceiling
            float x = (float)Math.Ceiling((w * i) / (double)numberX + offset);
            bX.Add(x); bY.Add((float)offset);
            bX.Add(x); bY.Add((float)(h + offset));
        }

        for (double i = 0.5; i < numberY; i++)
        {
            float y = (float)Math.Ceiling((h * i) / (double)numberY + offset);
            bX.Add((float)offset); bY.Add(y);
            bX.Add((float)(w + offset)); bY.Add(y);
        }

        data.BoundaryX = bX.ToArray();
        data.BoundaryY = bY.ToArray();
    }
}