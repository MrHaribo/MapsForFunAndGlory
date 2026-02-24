using MapGen.Core.Helpers;

namespace MapGen.Tests
{
    public class AleaTests
    {
        [Fact]
        public void Alea_SeedAzgaar_MatchesJsOutput()
        {
            var rng = new AleaRandom("azgaar");

            Assert.Equal(0.944474590709433, rng.Next(), 15);
            Assert.Equal(0.47310595540329814, rng.Next(), 15);
            Assert.Equal(0.4731390099041164, rng.Next(), 15);
        }

    }
}
