//using MapGen.Core;
//using Newtonsoft.Json;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace MapGen.Tests
//{
//    public class HeightsRegressionData
//    {
//        public string Seed { get; set; }
//        public int Count { get; set; }
//        public byte[] Heights { get; set; }
//    }
//    public class HeightmapTests
//    {
//        [Fact]
//        public void HeightmapGenerator_HighIsland_MatchesJsOutput()
//        {
//            var json = File.ReadAllText("regression_heights_highisland.json");
//            var expected = JsonConvert.DeserializeObject<HeightsRegressionData>(json);

//            var options = new GenerationOptions
//            {
//                Seed = "azgaar", // Must match the dump seed
//                Width = 1920,
//                Height = 1080,
//                PointsCount = 9975,
//                Jitter = 0.8
//            };

//            var generator = new MapGenerator();
//            generator.Generate(options);

//            // Assert parity
//            for (int i = 0; i < expected.Heights.Length; i++)
//            {
//                // We use a small tolerance or direct equality 
//                // because these are bytes (0-100)
//                Assert.True(expected.Heights[i] == generator.Data.H[i],
//                    $"Height mismatch at index {i}. Expected {expected.Heights[i]}, Got {generator.Data.H[i]}");
//            }
//        }
//    }
//}
