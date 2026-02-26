namespace MapGen.Core.Helpers
{
    public class AleaRandom : IRandom
    {
        private double s0, s1, s2, c;

        public AleaRandom() => Init(string.Empty);
        public AleaRandom(string seed) => Init(seed);

        public void Init(string seed)
        {
            var mash = new Mash();
            // The state MUST persist across these three calls
            s0 = mash.Apply(" ");
            s1 = mash.Apply(" ");
            s2 = mash.Apply(" ");

            // The seed is then applied to the already-warmed-up mash state
            s0 -= mash.Apply(seed);
            if (s0 < 0) s0 += 1;

            s1 -= mash.Apply(seed);
            if (s1 < 0) s1 += 1;

            s2 -= mash.Apply(seed);
            if (s2 < 0) s2 += 1;

            c = 1;
        }

        // Standard NextDouble (0.0 to 1.0)
        public double Next()
        {
            double t = 2091639 * s0 + c * 2.3283064365386963e-10; // 2^-32
            s0 = s1;
            s1 = s2;
            c = (uint)t;
            s2 = t - c;
            return s2;
        }

        private class Mash
        {
            private uint n = 0xefc8249d;

            public double Apply(string data)
            {
                for (int i = 0; i < data.Length; i++)
                {
                    n += data[i];
                    double h = 0.02519603282416938 * n;
                    n = (uint)h;
                    h -= n;
                    h *= n;
                    n = (uint)h;
                    h -= n;
                    n += (uint)(h * 0x100000000); // 2^32
                }
                return n * 2.3283064365386963e-10;
            }
        }
    }
}