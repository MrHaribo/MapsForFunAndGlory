using MapGen.Core;
using MapGen.Core.Helpers;
using MapGen.Core.Modules;
using Moq;
using Newtonsoft.Json;

namespace MapGen.Tests
{

    public class NameTests
    {
        [Fact]
        public void AssertNamesAgainstJsRegressionFile()
        {
            // Load the "Gold Standard"
            var json = File.ReadAllText("data/regression_names.json");
            var expected = JsonConvert.DeserializeObject<RegressionJson>(json);

            // Initialize deterministic RNG
            IRandom rng = new AleaRandom("42");

            // --- 0. RNG SYNC CHECK ---
            // If this fails, the Alea implementation itself is different
            for (int i = 0; i < 3; i++)
            {
                double actualRng = rng.Next();
                // Using a small delta for floating point precision, though Alea should be exact
                Assert.Equal(expected.RngCheck[i], actualRng, precision: 15);
            }

            // --- 1. getBase ---
            var baseConfigs = new[] { new[] { 0, 5, 10 }, new[] { 1, 5, 10 }, new[] { 18, 4, 8 } };
            int getBasePointer = 0;
            foreach (var p in baseConfigs)
            {
                for (int i = 0; i < 5; i++)
                {
                    string actual = NameModule.GetBase(rng, p[0], p[1], p[2], "");
                    Assert.Equal(expected.GetBase[getBasePointer++], actual);
                }
            }

            // --- 2. getBaseShort ---
            var shortIds = new[] { 0, 1, 12, 18 };
            int getShortPointer = 0;
            foreach (int id in shortIds)
            {
                for (int i = 0; i < 3; i++)
                {
                    string actual = NameModule.GetBaseShort(rng, id);
                    Assert.Equal(expected.GetBaseShort[getShortPointer++], actual);
                }
            }

            // --- 3. getState ---
            var stateInputs = new[] { ("Berlin", 0), ("Paris", 2), ("Kyoto", 12), ("Cairo", 18) };
            int getStatePointer = 0;
            foreach (var input in stateInputs)
            {
                for (int i = 0; i < 3; i++)
                {
                    string actual = NameModule.GetStateName(rng, input.Item1, baseIndex: input.Item2);
                    Assert.Equal(expected.GetState[getStatePointer++], actual);
                }
            }

            // --- 4. getMapName ---
            for (int i = 0; i < 5; i++)
            {
                string actual = NameModule.GetMapName(rng);
                Assert.Equal(expected.GetMapName[i], actual);
            }
        }

        public class RegressionJson
        {
            public string Seed { get; set; }
            public List<double> RngCheck { get; set; } // Added this
            public List<string> GetBase { get; set; }
            public List<string> GetBaseShort { get; set; }
            public List<string> GetState { get; set; }
            public List<string> GetMapName { get; set; }
        }

        //[Fact]
        //public void ChainTest()
        //{
        //    // Use a fixed seed for the first test to ensure reproducibility
        //    IRandom rng = new AleaRandom("42");

        //    Console.WriteLine("=== NAME MODULE COMPREHENSIVE TEST ===");
        //    Console.WriteLine();

        //    // 1. Test Base IDs (Testing Lazy Loading of multiple different chains)
        //    // We'll test German (0), English (1), and French (2)
        //    int[] testBases = { 0, 1, 2 };
        //    string[] baseNames = { "German", "English", "French" };

        //    for (int i = 0; i < testBases.Length; i++)
        //    {
        //        int id = testBases[i];
        //        Console.WriteLine($"--- Base: {baseNames[i]} (ID: {id}) ---");

        //        // Standard Base Generation
        //        var names = Enumerable.Range(0, 5).Select(_ => NameModule.GetBase(rng, id, 5, 10, "")).ToList();
        //        Console.WriteLine($"Standard (5-10 chars): {string.Join(", ", names)}");

        //        // Short Base (Used for Culture Templates)
        //        var shorts = Enumerable.Range(0, 5).Select(_ => NameModule.GetBaseShort(rng, id)).ToList();
        //        Console.WriteLine($"Short Version:         {string.Join(", ", shorts)}");
        //        Console.WriteLine();
        //    }

        //    // 2. Test Random Culture Selection Logic
        //    Console.WriteLine("--- Random Culture Selection Simulation ---");
        //    for (int i = 0; i < 3; i++)
        //    {
        //        int randomId = NameModule.GetRandomBaseId(rng);
        //        string cultureName = NameModule.GetCultureShort(rng, randomId);
        //        Console.WriteLine($"Picked Base {randomId} -> Generated Culture Name: {cultureName}");
        //    }
        //    Console.WriteLine();

        //    // 3. Test English Culture Template (Fixed Parameters)
        //    // Simulating: new CultureTemplate { Name = NameModule.GetBase(rng, 1, 5, 9, ""), BaseNameId = 1 }
        //    Console.WriteLine("--- English Culture Template Simulation (Base 1, 5-9 chars) ---");
        //    var englishNames = Enumerable.Range(0, 5).Select(_ => NameModule.GetBase(rng, 1, 5, 9, "")).ToList();
        //    Console.WriteLine($"Results: {string.Join(", ", englishNames)}");
        //    Console.WriteLine();

        //    // 4. Test State Name Logic (Suffixes and Transformations)
        //    Console.WriteLine("--- State Name Generation (Transforming Base Names) ---");
        //    // We'll take a generated name and turn it into a State
        //    string root = NameModule.GetBase(rng, 0, 5, 8, ""); // German root
        //    string stateLand = NameModule.GetStateName(rng, root, baseIndex: 0); // Should favor -land

        //    string root2 = NameModule.GetBase(rng, 18, 5, 8, ""); // Arabic root
        //    string stateAl = NameModule.GetStateName(rng, root2, baseIndex: 18); // Might add "Al"

        //    Console.WriteLine($"German Root: {root} -> State: {stateLand}");
        //    Console.WriteLine($"Arabic Root: {root2} -> State: {stateAl}");
        //    Console.WriteLine();

        //    // 5. Test Map Name
        //    Console.WriteLine("--- Map Name Generation ---");
        //    for (int i = 0; i < 3; i++)
        //    {
        //        Console.WriteLine($"Map Name {i + 1}: {NameModule.GetMapName(rng)}");
        //    }

        //    Console.WriteLine();
        //    Console.WriteLine("=== TEST COMPLETE ===");
        //}

    }
}