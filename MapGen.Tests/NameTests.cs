using MapGen.Core;
using MapGen.Core.Helpers;
using MapGen.Core.Modules;
using Moq;

namespace MapGen.Tests
{
    public class NameTests
    {

        [Fact]
        public void ChainTest()
        {
            // Use a fixed seed for the first test to ensure reproducibility
            IRandom rng = new AleaRandom("42");

            Console.WriteLine("=== NAME MODULE COMPREHENSIVE TEST ===");
            Console.WriteLine();

            // 1. Test Base IDs (Testing Lazy Loading of multiple different chains)
            // We'll test German (0), English (1), and French (2)
            int[] testBases = { 0, 1, 2 };
            string[] baseNames = { "German", "English", "French" };

            for (int i = 0; i < testBases.Length; i++)
            {
                int id = testBases[i];
                Console.WriteLine($"--- Base: {baseNames[i]} (ID: {id}) ---");

                // Standard Base Generation
                var names = Enumerable.Range(0, 5).Select(_ => NameModule.GetBase(rng, id, 5, 10, "")).ToList();
                Console.WriteLine($"Standard (5-10 chars): {string.Join(", ", names)}");

                // Short Base (Used for Culture Templates)
                var shorts = Enumerable.Range(0, 5).Select(_ => NameModule.GetBaseShort(rng, id)).ToList();
                Console.WriteLine($"Short Version:         {string.Join(", ", shorts)}");
                Console.WriteLine();
            }

            // 2. Test Random Culture Selection Logic
            Console.WriteLine("--- Random Culture Selection Simulation ---");
            for (int i = 0; i < 3; i++)
            {
                int randomId = NameModule.GetRandomBaseId(rng);
                string cultureName = NameModule.GetCultureShort(rng, randomId);
                Console.WriteLine($"Picked Base {randomId} -> Generated Culture Name: {cultureName}");
            }
            Console.WriteLine();

            // 3. Test English Culture Template (Fixed Parameters)
            // Simulating: new CultureTemplate { Name = NameModule.GetBase(rng, 1, 5, 9, ""), BaseNameId = 1 }
            Console.WriteLine("--- English Culture Template Simulation (Base 1, 5-9 chars) ---");
            var englishNames = Enumerable.Range(0, 5).Select(_ => NameModule.GetBase(rng, 1, 5, 9, "")).ToList();
            Console.WriteLine($"Results: {string.Join(", ", englishNames)}");
            Console.WriteLine();

            // 4. Test State Name Logic (Suffixes and Transformations)
            Console.WriteLine("--- State Name Generation (Transforming Base Names) ---");
            // We'll take a generated name and turn it into a State
            string root = NameModule.GetBase(rng, 0, 5, 8, ""); // German root
            string stateLand = NameModule.GetStateName(rng, root, baseIndex: 0); // Should favor -land

            string root2 = NameModule.GetBase(rng, 18, 5, 8, ""); // Arabic root
            string stateAl = NameModule.GetStateName(rng, root2, baseIndex: 18); // Might add "Al"

            Console.WriteLine($"German Root: {root} -> State: {stateLand}");
            Console.WriteLine($"Arabic Root: {root2} -> State: {stateAl}");
            Console.WriteLine();

            // 5. Test Map Name
            Console.WriteLine("--- Map Name Generation ---");
            for (int i = 0; i < 3; i++)
            {
                Console.WriteLine($"Map Name {i + 1}: {NameModule.GetMapName(rng)}");
            }

            Console.WriteLine();
            Console.WriteLine("=== TEST COMPLETE ===");
        }

    }
}