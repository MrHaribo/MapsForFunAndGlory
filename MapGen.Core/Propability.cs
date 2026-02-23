using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MapGen.Core
{
    public static class Probability
    {
        // Return a random integer in a range [min, max] inclusive
        // JS: Math.floor(Math.random() * (max - min + 1)) + min
        public static int Rand(IRandom rng, int min, int max)
        {
            // Using your interface's int range method
            // Since Azgaar's rand is inclusive, we ensure the range matches
            return rng.Next(min, max);
        }

        public static int Rand(IRandom rng, int max) => Rand(rng, 0, max);

        // Probability shorthand
        // JS: Math.random() < probability
        public static bool P(IRandom rng, double probability)
        {
            if (probability >= 1) return true;
            if (probability <= 0) return false;
            return rng.Next() < probability;
        }

        // Probability shorthand for floats
        // JS: ~~float + +P(float % 1)
        public static int Pint(IRandom rng, double value)
        {
            return (int)value + (P(rng, value % 1) ? 1 : 0);
        }

        // Return random value from the array
        public static T Ra<T>(IRandom rng, T[] array)
        {
            int index = rng.Next(0, array.Length - 1);
            return array[index];
        }

        // Return random key from weighted dictionary
        public static string Rw(IRandom rng, Dictionary<string, int> obj)
        {
            var list = new List<string>();
            foreach (var kvp in obj)
            {
                for (int i = 0; i < kvp.Value; i++)
                {
                    list.Add(kvp.Key);
                }
            }
            return Ra(rng, list.ToArray());
        }

        // Biased random number
        // Matches JS: Math.round(min + (max - min) * Math.pow(Math.random(), ex))
        public static int Biased(IRandom rng, int min, int max, double ex)
        {
            double value = min + (max - min) * Math.Pow(rng.Next(), ex);
            // Use AwayFromZero to match JavaScript's Math.round()
            return (int)Math.Round(value, MidpointRounding.AwayFromZero);
        }

        public static double Gauss(IRandom rng, double expected = 100, double deviation = 30, double min = 0, double max = 300, int round = 0)
        {
            double u1 = 1.0 - rng.Next();
            double u2 = 1.0 - rng.Next();
            double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
            double randNormal = expected + deviation * randStdNormal;

            double clamped = Math.Clamp(randNormal, min, max);
            // Also use AwayFromZero here for consistency with JS
            return Math.Round(clamped, round, MidpointRounding.AwayFromZero);
        }

        public static int GetNumberInRange(IRandom rng, string r)
        {
            if (string.IsNullOrEmpty(r)) return 0;

            // 1. Check if it's a simple number (like "95" or "10.5")
            if (double.TryParse(r, NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
            {
                // JS uses: ~~r + +P(r - ~~r)
                // We must only call P() if there is a fractional part
                double fractionalPart = val - Math.Truncate(val);

                // CRITICAL: JS only calls Math.random() if the second part of the sum is evaluated
                // In C#, ensure we don't consume an RNG value if fractionalPart is 0
                if (fractionalPart == 0)
                {
                    return (int)val;
                }

                return (int)val + (P(rng, fractionalPart) ? 1 : 0);
            }

            // 2. Handle Range logic ("90-100")
            int sign = r[0] == '-' ? -1 : 1;
            string tempR = char.IsDigit(r[0]) ? r : r.Substring(1);

            if (tempR.Contains("-"))
            {
                var parts = tempR.Split('-');
                if (double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double rangeMin) &&
                    double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double rangeMax))
                {
                    // JS: rand(range[0] * sign, +range[1])
                    // This calls Math.random() inside Rand()
                    return Rand(rng, (int)(rangeMin * sign), (int)rangeMax);
                }
            }

            return 0;
        }

        public static string GenerateSeed(IRandom rng)
        {
            return Math.Floor(rng.Next() * 1e9).ToString(CultureInfo.InvariantCulture);
        }
    }
}
