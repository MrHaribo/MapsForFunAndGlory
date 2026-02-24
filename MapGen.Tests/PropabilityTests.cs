using MapGen.Core;
using MapGen.Core.Helpers;
using MapGen.Core.Modules;
using Moq;

namespace MapGen.Tests
{
    public class ProbabilityTests
    {
        private readonly Mock<IRandom> _mockRng;

        public ProbabilityTests()
        {
            _mockRng = new Mock<IRandom>();
        }

        #region Basic Range Extensions (Next)

        [Fact]
        public void Next_IntRange_IsInclusiveOfMax()
        {
            // JS Logic: Math.floor(r * (max - min + 1)) + min
            // For range 5 to 10: 
            // - 0.0 results in 5
            // - 0.999 results in 10

            _mockRng.Setup(r => r.Next()).Returns(0.9999);
            Assert.Equal(10, _mockRng.Object.Next(5, 10));

            _mockRng.Setup(r => r.Next()).Returns(0.0);
            Assert.Equal(5, _mockRng.Object.Next(5, 10));
        }

        [Fact]
        public void Next_DoubleRange_MatchesLinearScaling()
        {
            // Formula: Next() * (max - min) + min
            _mockRng.Setup(r => r.Next()).Returns(0.5);
            Assert.Equal(15.0, _mockRng.Object.Next(10.0, 20.0));
        }

        #endregion

        #region Azgaar Specific logic (P, Pint, Ra, Rw, Biased)

        [Theory]
        [InlineData(0.5, 0.4, true)]  // 0.4 < 0.5
        [InlineData(0.5, 0.6, false)] // 0.6 >= 0.5
        public void P_ReturnsCorrectBoolean(double threshold, double roll, bool expected)
        {
            _mockRng.Setup(r => r.Next()).Returns(roll);
            Assert.Equal(expected, _mockRng.Object.P(threshold));
        }

        [Fact]
        public void Pint_ProbabilisticRounding_Works()
        {
            // Pint(2.3) -> 2 + (roll < 0.3 ? 1 : 0)
            _mockRng.Setup(r => r.Next()).Returns(0.2); // Success
            Assert.Equal(3, _mockRng.Object.Pint(2.3));

            _mockRng.Setup(r => r.Next()).Returns(0.4); // Fail
            Assert.Equal(2, _mockRng.Object.Pint(2.3));
        }

        [Fact]
        public void Ra_ReturnsCorrectElement()
        {
            var array = new[] { "A", "B", "C" };
            // Range 0 to 2. Roll 0.5 -> Floor(0.5 * 3) + 0 = index 1 ("B")
            _mockRng.Setup(r => r.Next()).Returns(0.5);
            Assert.Equal("B", _mockRng.Object.Ra(array));
        }

        [Fact]
        public void Biased_UsesPowerCurve_AndAwayFromZeroRounding()
        {
            // Math.round(10 + (100-10) * Math.pow(0.5, 2))
            // 10 + 90 * 0.25 = 10 + 22.5 = 32.5
            // Round(32.5, AwayFromZero) = 33
            _mockRng.Setup(r => r.Next()).Returns(0.5);
            Assert.Equal(33, _mockRng.Object.Biased(10, 100, 2.0));
        }

        #endregion

        #region Range String Parsers (GetNumberInRange, GetPointInRange)

        [Fact]
        public void GetNumberInRange_WholeNumber_ZeroRngConsumption()
        {
            // IMPORTANT: Passing "10" must not call rng.Next() to preserve sequence
            var result = Probability.GetNumberInRange(_mockRng.Object, "10");

            Assert.Equal(10, result);
            _mockRng.Verify(r => r.Next(), Times.Never);
        }

        [Fact]
        public void GetNumberInRange_RangeString_CalculatesCorrectValue()
        {
            // "5-10" calls Next(5, 10)
            // Roll 0.5: Floor(0.5 * 6) + 5 = 3 + 5 = 8
            _mockRng.Setup(r => r.Next()).Returns(0.5);
            var result = Probability.GetNumberInRange(_mockRng.Object, "5-10");

            Assert.Equal(8, result);
        }

        [Fact]
        public void GetNumberInRange_NegativeRange_Works()
        {
            // "-10--5" -> min: -10, max: -5. Range size: 6.
            // Roll 0.5: Floor(0.5 * 6) - 10 = 3 - 10 = -7
            _mockRng.Setup(r => r.Next()).Returns(0.5);
            var result = Probability.GetNumberInRange(_mockRng.Object, "-10--5");

            Assert.Equal(-7, result);
        }

        [Fact]
        public void GetNumberInRange_CrossZeroRange_Works()
        {
            // "10--5" -> min: 10, max: -5. Range size: 16 (from -5 to 10 inclusive)
            // Roll 0.5: Floor(0.5 * 16) - 5 = 8 - 5 = 3
            _mockRng.Setup(r => r.Next()).Returns(0.5);
            var result = Probability.GetNumberInRange(_mockRng.Object, "10--5");
            Assert.Equal(3, result);
        }

        [Fact]
        public void Rw_WeightedDictionary_SelectsCorrectKey()
        {
            var weights = new Dictionary<string, int> { { "Low", 1 }, { "High", 9 } };
            // Total weight 10. 
            // Roll 0.05 -> index 0 ("Low")
            // Roll 0.5  -> index 5 ("High")

            _mockRng.Setup(r => r.Next()).Returns(0.05);
            Assert.Equal("Low", _mockRng.Object.Rw(weights));

            _mockRng.Setup(r => r.Next()).Returns(0.5);
            Assert.Equal("High", _mockRng.Object.Rw(weights));
        }

        [Fact]
        public void Gauss_ClampsAndRoundsCorrectly()
        {
            // Mocking Gauss is tricky because it calls Next() twice.
            // u1 = 1 - 0.5 = 0.5, u2 = 1 - 0.8 = 0.2
            // This will produce a specific value on the curve.
            _mockRng.SetupSequence(r => r.Next())
                .Returns(0.5)
                .Returns(0.8);

            // We mostly want to test the clamping/rounding logic here
            var result = Probability.Gauss(_mockRng.Object, expected: 100, deviation: 10, min: 150, max: 200);

            // Since expected is 100 but min is 150, the result MUST be 150
            Assert.Equal(150, result);
        }

        [Fact]
        public void GenerateSeed_ProducesNineDigitString()
        {
            _mockRng.Setup(r => r.Next()).Returns(0.123456789);
            var seed = Probability.GenerateSeed(_mockRng.Object);

            // 0.123456789 * 1e9 = 123456789
            Assert.Equal("123456789", seed);
        }

        [Fact]
        public void GetPointInRange_ParsesPercentageCorrectly()
        {
            // "10-20" on length 1000 => 100 to 200
            // Formula: 0.5 * (200 - 100) + 100 = 150
            _mockRng.Setup(r => r.Next()).Returns(0.5);
            var result = HeightmapGenerator.GetPointInRange("10-20", 1000, _mockRng.Object);

            Assert.Equal(150.0, result);
        }

        #endregion
    }
}