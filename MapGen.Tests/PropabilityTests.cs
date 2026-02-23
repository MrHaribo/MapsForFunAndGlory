using MapGen.Core;

namespace MapGen.Tests
{
    public class MockRng : IRandom
    {
        public double NextValue { get; set; } = 0.5;
        public int NextIntValue { get; set; } = 5;

        public double Next() => NextValue;
        public double Next(double min, double max) => min + (max - min) * NextValue;
        public int Next(int min, int max) => NextIntValue;
    }

    public class ProbabilityTests
    {
        private readonly MockRng _rng = new MockRng();

        [Fact]
        public void Rand_Inclusive_ReturnsValue()
        {
            _rng.NextIntValue = 10;
            var result = Probability.Rand(_rng, 0, 10);
            Assert.Equal(10, result);
        }

        [Theory]
        [InlineData(0.5, 0.6, true)]  // 0.5 < 0.6
        [InlineData(0.7, 0.6, false)] // 0.7 is not < 0.6
        public void P_Threshold_ReturnsCorrectBool(double rngValue, double threshold, bool expected)
        {
            _rng.NextValue = rngValue;
            Assert.Equal(expected, Probability.P(_rng, threshold));
        }

        [Fact]
        public void Pint_FractionalProbability_TriggersCorrectly()
        {
            // JS: ~~1.5 + +P(0.5)
            // If RNG is 0.4, P(0.5) is true (1). Result: 1 + 1 = 2
            _rng.NextValue = 0.4;
            Assert.Equal(2, Probability.Pint(_rng, 1.5));

            // If RNG is 0.6, P(0.5) is false (0). Result: 1 + 0 = 1
            _rng.NextValue = 0.6;
            Assert.Equal(1, Probability.Pint(_rng, 1.5));
        }

        [Fact]
        public void GetNumberInRange_StaticString_ParsesCorrectly()
        {
            // Simple "10" should return 10
            var result = Probability.GetNumberInRange(_rng, "10");
            Assert.Equal(10, result);
        }

        [Fact]
        public void GetNumberInRange_RangeString_CallsRngNext()
        {
            _rng.NextIntValue = 7;
            // "5-10" should trigger rng.Next(5, 10)
            var result = Probability.GetNumberInRange(_rng, "5-10");
            Assert.Equal(7, result);
        }

        [Fact]
        public void Biased_Exponents_CalculatesCorrectly()
        {
            _rng.NextValue = 0.5;
            // min: 0, max: 10, ex: 2 -> 0 + 10 * (0.5^2) = 2.5 -> Round to 3
            var result = Probability.Biased(_rng, 0, 10, 2);
            Assert.Equal(3, result);
        }

        [Fact]
        public void Gauss_Clamping_Works()
        {
            _rng.NextValue = 0.0001; // Results in high deviation
                                     // Force a value that would be 500, but max is 300
            var result = Probability.Gauss(_rng, expected: 100, deviation: 1000, min: 0, max: 300);
            Assert.True(result <= 300);
        }
    }
}
