using System;
using System.Collections.Generic;
using System.Linq;

namespace MapGen.Core.Helpers
{
    public static class ColorUtil
    {
        private static readonly string[] C_12 = {
        "#dababf", "#fb8072", "#80b1d3", "#fdb462", "#b3de69", "#fccde5",
        "#c6b9c1", "#bc80bd", "#ccebc5", "#ffed6f", "#8dd3c7", "#eb8de7"
    };

        public static List<string> GetColors(int number, IRandom rng)
        {
            var colors = new List<string>();
            for (int i = 0; i < number; i++)
            {
                if (i < 12)
                {
                    colors.Add(C_12[i]);
                }
                else
                {
                    // d3.scaleSequential(d3.interpolateRainbow)((i - 12) / (number - 12))
                    double t = (double)(i - 12) / (number - 12);
                    colors.Add(GetRainbowHex(t));
                }
            }

            // Use our new extension to maintain RNG parity
            return rng.Shuffle(colors);
        }

        private static string GetRainbowHex(double t)
        {
            t = Math.Clamp(t, 0, 1);
            // Standard D3 Rainbow interpolation formula
            double r = Math.Round(255 * (0.5 + 0.5 * Math.Sin(Math.PI * (2 * t + 0.5))));
            double g = Math.Round(255 * (0.5 + 0.5 * Math.Sin(Math.PI * (2 * t + 0.5 + 2.0 / 3.0))));
            double b = Math.Round(255 * (0.5 + 0.5 * Math.Sin(Math.PI * (2 * t + 0.5 + 4.0 / 3.0))));

            return $"#{((int)r):x2}{((int)g):x2}{((int)b):x2}";
        }
    }
}