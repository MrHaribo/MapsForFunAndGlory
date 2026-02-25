using System;
using System.Collections.Generic;
using System.Globalization;

namespace MapGen.Core.Helpers
{
    public static class Probability
    {
        // --- Basic Range Extensions ---

        public static int Next(this IRandom rng, int min, int max)
            => (int)Math.Floor(rng.Next() * (max - min + 1)) + min;

        public static int Next(this IRandom rng, int max)
            => rng.Next(0, max);

        public static double Next(this IRandom rng, double min, double max)
        {
            var r = rng.Next();
            return Math.Floor(r * (max - min + 1)) + min;
        }

        // --- Azgaar Specific Logic ---

        public static bool P(this IRandom rng, double probability)
        {
            if (probability >= 1) return true;
            if (probability <= 0) return false;
            return rng.Next() < probability;
        }

        public static int Pint(this IRandom rng, double value)
            => (int)value + (rng.P(value % 1) ? 1 : 0);

        public static T Ra<T>(this IRandom rng, T[] array)
            => array[rng.Next(0, array.Length - 1)];

        public static string Rw(this IRandom rng, Dictionary<string, int> obj)
        {
            var list = new List<string>();
            foreach (var kvp in obj)
                for (int i = 0; i < kvp.Value; i++)
                    list.Add(kvp.Key);
            return rng.Ra(list.ToArray());
        }

        public static int Biased(this IRandom rng, int min, int max, double ex)
        {
            double value = min + (max - min) * Math.Pow(rng.Next(), ex);
            return (int)Math.Round(value, MidpointRounding.AwayFromZero);
        }

        // --- Static Helper Methods (Non-Extensions) ---

        // Legacy: Trigonometric Box-Muller transform
        //public static double Gauss(this IRandom rng, double expected = 100, double deviation = 30, double min = 0, double max = 300, int round = 0)
        //{
        //    double u1 = 1.0 - rng.Next();
        //    double u2 = 1.0 - rng.Next();
        //    double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        //    double randNormal = expected + deviation * randStdNormal;
        //    return Math.Round(Math.Clamp(randNormal, min, max), round, MidpointRounding.AwayFromZero);
        //}

        public static double Gauss(this IRandom rng, double expected = 100, double deviation = 30, double min = 0, double max = 300, int round = 0)
        {
            double x, y, r;
            // D3.js uses the Marsaglia Polar Method
            // It consumes numbers in pairs until they fall within the unit circle
            do
            {
                x = rng.Next() * 2 - 1;
                y = rng.Next() * 2 - 1;
                r = x * x + y * y;
            } while (r == 0 || r > 1); // D3: while (!r || r > 1)

            // D3 returns the 'y' component and calculates the 'z' value like this:
            double z = y * Math.Sqrt(-2.0 * Math.Log(r) / r);
            double res = expected + deviation * z;

            // Consistency check: Azgaar's 'rn' function rounds first, then we clamp
            // JS: rn(minmax(val, min, max), round)
            double rounded = Math.Round(res, round, MidpointRounding.AwayFromZero);
            return Math.Clamp(rounded, min, max);
        }

        public static int GetNumberInRange(this IRandom rng, string r)
        {
            if (string.IsNullOrEmpty(r)) return 0;

            // 1. Check if it's a simple number (e.g., "95", "10.5", "-5")
            if (double.TryParse(r, NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
            {
                double fractionalPart = val - Math.Truncate(val);
                if (fractionalPart == 0) return (int)val;
                return (int)val + (rng.P(fractionalPart) ? 1 : 0);
            }

            // 2. Handle Range logic ("10-20", "-10--5", "10--5")
            // Find the hyphen that separates the two numbers:
            // It's the hyphen that is NOT at the start and NOT preceded by another hyphen.
            int splitIdx = -1;
            for (int i = 1; i < r.Length; i++)
            {
                if (r[i] == '-' && r[i - 1] != '-')
                {
                    splitIdx = i;
                    break;
                }
            }

            if (splitIdx != -1)
            {
                string minPart = r.Substring(0, splitIdx);
                string maxPart = r.Substring(splitIdx + 1);

                if (double.TryParse(minPart, NumberStyles.Any, CultureInfo.InvariantCulture, out double rangeMin) &&
                    double.TryParse(maxPart, NumberStyles.Any, CultureInfo.InvariantCulture, out double rangeMax))
                {
                    // Note: C# rng.Next(int, int) now handles the Azgaar inclusive logic
                    return rng.Next((int)rangeMin, (int)rangeMax);
                }
            }

            return 0;
        }

        public static string GenerateSeed(this IRandom rng)
        {
            // JS: Math.floor(Math.random() * 1e9).toString()
            double value = Math.Floor(rng.Next() * 1e9);
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}