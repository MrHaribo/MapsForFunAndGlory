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
            return GetRainbowHex(rng.Next()); // Consumes exactly 1 RNG call
        }

        public static string GetMixedColor(string color, IRandom rng, double mix = 0.2, double bright = 0.3)
        {
            // If provided color is not hex, generate random one
            string c = (!string.IsNullOrEmpty(color) && color.StartsWith("#")) ? color : GetRandomColor(rng);
            string randomColor = GetRandomColor(rng);

            var (r1, g1, b1) = HexToRgb(c);
            var (r2, g2, b2) = HexToRgb(randomColor);

            // 1. Emulate D3 Interpolate Stringification (Math.round in JS)
            int rMix = (int)Math.Floor(r1 * (1 - mix) + r2 * mix + 0.5);
            int gMix = (int)Math.Floor(g1 * (1 - mix) + g2 * mix + 0.5);
            int bMix = (int)Math.Floor(b1 * (1 - mix) + b2 * mix + 0.5);

            // 2. Emulate D3 .brighter(bright)
            double k = Math.Pow(1.0 / 0.7, bright);
            double rBright = rMix * k;
            double gBright = gMix * k;
            double bBright = bMix * k;

            // 3. Emulate D3 .hex() (Math.round in JS)
            int rFinal = (int)Math.Clamp(Math.Floor(rBright + 0.5), 0, 255);
            int gFinal = (int)Math.Clamp(Math.Floor(gBright + 0.5), 0, 255);
            int bFinal = (int)Math.Clamp(Math.Floor(bBright + 0.5), 0, 255);

            return $"#{rFinal:x2}{gFinal:x2}{bFinal:x2}";
        }

        private static string GetRainbowHex(double t)
        {
            if (t < 0 || t > 1) t -= Math.Floor(t);
            double ts = Math.Abs(t - 0.5);

            double h = 360 * t - 100;
            double s = 1.5 - 1.5 * ts;
            double l = 0.8 - 0.9 * ts; // FIXED: D3 uses 0.9!

            // Cubehelix magic constants
            double A = -0.14861;
            double B = 1.78277;
            double C = -0.29227;
            double D = -0.90649;
            double E = 1.97294;

            double hRad = (h + 120) * (Math.PI / 180.0);
            double a = s * l * (1.0 - l);

            double cosh = Math.Cos(hRad);
            double sinh = Math.Sin(hRad);

            // D3 d3-color Cubehelix to RGB Conversion
            double r = 255 * (l + a * (A * cosh + B * sinh));
            double g = 255 * (l + a * (C * cosh + D * sinh));
            double b = 255 * (l + a * (E * cosh)); // FIXED: Back to E * cosh

            // JS Math.round logic
            int rFinal = (int)Math.Clamp(Math.Floor(r + 0.5), 0, 255);
            int gFinal = (int)Math.Clamp(Math.Floor(g + 0.5), 0, 255);
            int bFinal = (int)Math.Clamp(Math.Floor(b + 0.5), 0, 255);

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