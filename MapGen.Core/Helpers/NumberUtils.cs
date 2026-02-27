using System;

namespace MapGen.Core.Helpers
{
    public static class NumberUtils
    {
        /// <summary>Equivalent to JS: rn(v, d)</summary>
        public static double RoundOld(double val, int precision = 0) =>
            Math.Round(val, precision, MidpointRounding.AwayFromZero);

        public static double Round(double val, int precision = 0)
        {
            if (precision == 0)
            {
                // JavaScript Math.round behavior: rounds .5 towards +Infinity
                return Math.Floor(val + 0.5);
            }

            double m = Math.Pow(10, precision);
            return Math.Floor(val * m + 0.5) / m;
        }

        /// <summary>Equivalent to JS: minmax(value, min, max)</summary>
        public static double MinMax(double value, double min, double max) =>
            Math.Clamp(value, min, max);

        /// <summary>Equivalent to JS: lim(v)</summary>
        public static double Lim(double val) => Math.Clamp(val, 0, 100);
        public static float Lim(float val) => Math.Clamp(val, 0f, 100f);
        public static int Lim(int val) => Math.Clamp(val, 0, 100);
        public static byte Lim(byte val) => Math.Clamp(val, (byte)0, (byte)100);

        /// <summary>Equivalent to JS: normalize(val, min, max)</summary>
        public static double Normalize(double val, double min, double max)
        {
            if (Math.Abs(max - min) < double.Epsilon) return 0;
            return MinMax((val - min) / (max - min), 0, 1);
        }

        /// <summary>Equivalent to JS: lerp(a, b, t)</summary>
        public static double Lerp(double a, double b, double t) =>
            a + (b - a) * t;
    }
}
