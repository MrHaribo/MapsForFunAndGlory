using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MapGen.Core.Helpers
{
    public static class ColorUtils
    {
        private static readonly string[] C_12 = {
            "#dababf", "#fb8072", "#80b1d3", "#fdb462", "#b3de69", "#fccde5",
            "#c6b9c1", "#bc80bd", "#ccebc5", "#ffed6f", "#8dd3c7", "#eb8de7"
        };

        // Regex to parse rgb() or rgba() strings, ignoring case
        private static readonly Regex RgbRegex = new Regex(
            @"^rgba?[\s+]?\([\s+]?(\d+)[\s+]?,[\s+]?(\d+)[\s+]?,[\s+]?(\d+)[\s+]?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

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

        public static string GetRandomColor(IRandom rng)
        {
            return GetRainbowHex(rng.Next());
        }

        public static string GetMixedColor(string color, IRandom rng, double mix = 0.2, double bright = 0.3)
        {
            // If provided color is not hex (e.g. hatching), generate random one
            string c = (!string.IsNullOrEmpty(color) && color.StartsWith("#")) ? color : GetRandomColor(rng);
            string randomColor = GetRandomColor(rng);

            var (r1, g1, b1) = HexToRgb(c);
            var (r2, g2, b2) = HexToRgb(randomColor);

            // 1. d3.interpolate(c, getRandomColor())(mix) -> Standard RGB linear interpolation
            double rMix = r1 * (1 - mix) + r2 * mix;
            double gMix = g1 * (1 - mix) + g2 * mix;
            double bMix = b1 * (1 - mix) + b2 * mix;

            // 2. .brighter(bright) -> D3 multiplies channels by (1 / 0.7) ^ k
            double k = Math.Pow(1.0 / 0.7, bright);
            rMix *= k;
            gMix *= k;
            bMix *= k;

            // Clamp values between 0 and 255
            int rFinal = (int)Math.Clamp(Math.Round(rMix), 0, 255);
            int gFinal = (int)Math.Clamp(Math.Round(gMix), 0, 255);
            int bFinal = (int)Math.Clamp(Math.Round(bMix), 0, 255);

            return $"#{rFinal:x2}{gFinal:x2}{bFinal:x2}";
        }

        public static string ToHex(string rgb)
        {
            if (string.IsNullOrEmpty(rgb)) return "";
            if (rgb.StartsWith("#")) return rgb;

            var match = RgbRegex.Match(rgb);
            if (match.Success && match.Groups.Count >= 4)
            {
                int r = int.Parse(match.Groups[1].Value);
                int g = int.Parse(match.Groups[2].Value);
                int b = int.Parse(match.Groups[3].Value);

                return $"#{r:x2}{g:x2}{b:x2}";
            }

            return "";
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

        private static (int r, int g, int b) HexToRgb(string hex)
        {
            hex = hex.TrimStart('#');

            // Handle shorthand hex (e.g. #abc to #aabbcc)
            if (hex.Length == 3)
            {
                hex = new string(new char[] { hex[0], hex[0], hex[1], hex[1], hex[2], hex[2] });
            }

            if (hex.Length >= 6)
            {
                return (
                    Convert.ToInt32(hex.Substring(0, 2), 16),
                    Convert.ToInt32(hex.Substring(2, 2), 16),
                    Convert.ToInt32(hex.Substring(4, 2), 16)
                );
            }

            return (0, 0, 0); // Fallback
        }
    }
}