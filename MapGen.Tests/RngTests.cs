//using MapGen.Core;
//using System.Diagnostics;

//namespace MapGen.Tests
//{
//    public class RngTests
//    {
//        [Fact]
//        public void SeedRandom_IntSeed_GeneratesExpectedSequence()
//        {
//            // Expected values from JS Alea("12345")
//            double[] expected =
//            {
//                0.27138191112317145 ,
//                0.19615925149992108 ,
//                0.6810678059700876  ,
//                0.9894359013997018  ,
//                0.34078020555898547 ,
//                0.984706997172907   ,
//                0.7196994491387159  ,
//                0.1688570436090231  ,
//                0.559025701135397   ,
//                0.43657660437747836
//            };

//            var rng = new SeedRandom(12345);

//            for (int i = 0; i < expected.Length; i++)
//            {
//                double actual = rng.NextDouble();
//                Assert.Equal(expected[i], actual, 12);
//            }
//        }

//        [Fact]
//        public void SeedRandom_StringSeed_GeneratesExpectedSequence()
//        {
//            // Expected values from JS Alea("12345")
//            double[] expected =
//            {
//                0.27138191112317145 ,
//                0.19615925149992108 ,
//                0.6810678059700876  ,
//                0.9894359013997018  ,
//                0.34078020555898547 ,
//                0.984706997172907   ,
//                0.7196994491387159  ,
//                0.1688570436090231  ,
//                0.559025701135397   ,
//                0.43657660437747836
//            };

//            var rng = new SeedRandom("12345");

//            for (int i = 0; i < expected.Length; i++)
//            {
//                double actual = rng.NextDouble();
//                Assert.Equal(expected[i], actual, 12);
//            }
//        }

//    }
//}