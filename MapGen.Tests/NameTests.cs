using Castle.Components.DictionaryAdapter.Xml;
using MapGen.Core;
using MapGen.Core.Helpers;
using MapGen.Core.Modules;
using Newtonsoft.Json;

namespace MapGen.Tests
{
    public class RegressionChainDump
    {
        public int index { get; set; }
        public string name { get; set; }
        // Maps key (prev char) to list of syllables
        public Dictionary<string, List<string>> chain { get; set; }
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

    public class RegressionNameData 
    { 
        public int index { get; set; } 
        public string baseName { get; set; } 
        public List<string> names { get; set; }
    }

    public class NameTests
    {
        private readonly List<RegressionChainDump> _chainReferenceData;
        private readonly List<RegressionNameData> _nameReferenceData;

        public NameTests()
        {
            string chainData = File.ReadAllText("data/markov_chains_regression.json");
            _chainReferenceData = JsonConvert.DeserializeObject<List<RegressionChainDump>>(chainData);

            var nameData = File.ReadAllText("data/regression_name_generation.json");
            _nameReferenceData = JsonConvert.DeserializeObject<List<RegressionNameData>>(nameData);
        }


        [Theory]
        // Real-world bases by Azgaar
        [InlineData(0, "German")]
        [InlineData(1, "English")]
        [InlineData(2, "French")]
        [InlineData(3, "Italian")]
        [InlineData(4, "Castillian")]
        [InlineData(5, "Ruthenian")]
        [InlineData(6, "Nordic")]
        [InlineData(7, "Greek")]
        [InlineData(8, "Roman")]
        [InlineData(9, "Finnic")]
        [InlineData(10, "Korean")]
        [InlineData(11, "Chinese")]
        [InlineData(12, "Japanese")]
        [InlineData(13, "Portuguese")]
        [InlineData(14, "Nahuatl")]
        [InlineData(15, "Hungarian")]
        [InlineData(16, "Turkish")]
        [InlineData(17, "Berber")]
        [InlineData(18, "Arabic")]
        [InlineData(19, "Inuit")]
        [InlineData(20, "Basque")]
        [InlineData(21, "Nigerian")]
        [InlineData(22, "Celtic")]
        [InlineData(23, "Mesopotamian")]
        [InlineData(24, "Iranian")]
        [InlineData(25, "Hawaiian")]
        [InlineData(26, "Karnataka")]
        [InlineData(27, "Quechua")]
        [InlineData(28, "Swahili")]
        [InlineData(29, "Vietnamese")]
        [InlineData(30, "Cantonese")]
        [InlineData(31, "Mongolian")]
        // Fantasy bases by Dopu
        [InlineData(32, "Human Generic")]
        [InlineData(33, "Elven")]
        [InlineData(34, "Dark Elven")]
        [InlineData(35, "Dwarven")]
        [InlineData(36, "Goblin")]
        [InlineData(37, "Orc")]
        [InlineData(38, "Giant")]
        [InlineData(39, "Draconic")]
        [InlineData(40, "Arachnid")]
        [InlineData(41, "Serpents")]
        // Additional by Avengium
        [InlineData(42, "Levantine")]
        public void GenerateName_MatchesJSReference(int baseIndex, string expectedBaseName)
        {
            // 1. Load data
            var rng = new AleaRandom("42");
            var reference = _nameReferenceData[baseIndex];

            // 2. Setup the NameBase (ensure it has the same chain logic we just fixed)
            //var nameBase = NameModule.GetNameBases()[baseIndex];

            Assert.Equal(baseIndex, reference.index);
            Assert.Equal(expectedBaseName, reference.baseName);
            Assert.Equal(100, reference.names.Count);

            for (int i = 0; i < 100; i++) 
            {
                string actualName = NameModule.GetBase(rng, baseIndex);
                Assert.Equal(actualName, reference.names[i]);
            }
        }

        [Theory]
        // Real-world bases by Azgaar
        [InlineData(0, "German")]
        [InlineData(1, "English")]
        [InlineData(2, "French")]
        [InlineData(3, "Italian")]
        [InlineData(4, "Castillian")]
        [InlineData(5, "Ruthenian")]
        [InlineData(6, "Nordic")]
        [InlineData(7, "Greek")]
        [InlineData(8, "Roman")]
        [InlineData(9, "Finnic")]
        [InlineData(10, "Korean")]
        [InlineData(11, "Chinese")]
        [InlineData(12, "Japanese")]
        [InlineData(13, "Portuguese")]
        [InlineData(14, "Nahuatl")]
        [InlineData(15, "Hungarian")]
        [InlineData(16, "Turkish")]
        [InlineData(17, "Berber")]
        [InlineData(18, "Arabic")]
        [InlineData(19, "Inuit")]
        [InlineData(20, "Basque")]
        [InlineData(21, "Nigerian")]
        [InlineData(22, "Celtic")]
        [InlineData(23, "Mesopotamian")]
        [InlineData(24, "Iranian")]
        [InlineData(25, "Hawaiian")]
        [InlineData(26, "Karnataka")]
        [InlineData(27, "Quechua")]
        [InlineData(28, "Swahili")]
        [InlineData(29, "Vietnamese")]
        [InlineData(30, "Cantonese")]
        [InlineData(31, "Mongolian")]
        // Fantasy bases by Dopu
        [InlineData(32, "Human Generic")]
        [InlineData(33, "Elven")]
        [InlineData(34, "Dark Elven")]
        [InlineData(35, "Dwarven")]
        [InlineData(36, "Goblin")]
        [InlineData(37, "Orc")]
        [InlineData(38, "Giant")]
        [InlineData(39, "Draconic")]
        [InlineData(40, "Arachnid")]
        [InlineData(41, "Serpents")]
        // Additional by Avengium
        [InlineData(42, "Levantine")]
        public void CalculateChain_OutputMatchesJSReference(int baseIndex, string expectedBaseName)
        {
            // 1. Arrange: Find the reference data for this index
            var reference = _chainReferenceData.FirstOrDefault(d => d.index == baseIndex);
            Assert.NotNull(reference);

            var nameBase = NameModule.GetNameBases()[baseIndex];

            // 2. Act: Run your C# implementation
            var actualChain = NameModule.CalculateChain(nameBase.BaseContent);

            // 3. Assert: 
            // First, check that the set of keys (previous characters) is identical
            var expectedKeys = reference.chain.Keys.OrderBy(k => k).ToList();
            var actualKeys = actualChain.Keys.OrderBy(k => k).ToList();

            Assert.Equal(baseIndex, nameBase.Index);
            Assert.Equal(expectedBaseName, nameBase.Name);

            Assert.True(expectedKeys.SequenceEqual(actualKeys),
                $"Key mismatch for {reference.name}. " +
                $"JS has {expectedKeys.Count} keys, CS has {actualKeys.Count} keys.");

            // Second, check the syllable lists for every key
            foreach (var key in expectedKeys)
            {
                var expectedSyllables = reference.chain[key];
                var actualSyllables = actualChain[key];

                Assert.True(expectedSyllables.SequenceEqual(actualSyllables),
                    $"Syllable mismatch for {reference.name} at key '{key}'.\n" +
                    $"Expected: {string.Join(",", expectedSyllables)}\n" +
                    $"Actual:   {string.Join(",", actualSyllables)}");
            }
        }

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
    }
}