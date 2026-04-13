using MapGen.Core;
using MapGen.Core.Modules;
using System.Text.Json;

namespace MapGen.Tests
{
    public class StateRegressionData
    {
        public List<StateDump> states { get; set; }
        public int[] cells_state { get; set; }
        public List<List<string>> chronicle { get; set; }
    }

    public class StateDump
    {
        public int id { get; set; }
        public string name { get; set; }
        public string color { get; set; }
        public double expansionism { get; set; }
        public int capitalId { get; set; }
        public string type { get; set; }
        public int centerCell { get; set; }
        public int cultureId { get; set; }
        public double[] pole { get; set; }
        public List<int> neighbors { get; set; }
        public int area { get; set; }
        public int population { get; set; }
        public int burgsCount { get; set; }
        public string[] diplomacy { get; set; }
        public List<CampaignDump> campaigns { get; set; }
        public string form { get; set; }
        public string formName { get; set; }
        public string fullName { get; set; }
    }

    public class CampaignDump
    {
        public string name { get; set; }
        public int start { get; set; }
        public int end { get; set; }
    }

    public class StateTests
    {
        private static MapPack _pack;
        private static StateRegressionData _expectedData;
        private static Dictionary<int, StateDump> _expectedDict;

        private static bool _isInitialized = false;
        private static readonly object _initLock = new object();

        public StateTests()
        {
            // Lock ensures that if xUnit runs tests in parallel, we only generate the map once.
            lock (_initLock)
            {
                if (!_isInitialized)
                {
                    // 1. Load JS Regression Data once
                    string json = File.ReadAllText("data/regression_states.json");
                    _expectedData = JsonSerializer.Deserialize<StateRegressionData>(json);
                    _expectedDict = _expectedData.states.ToDictionary(s => s.id);

                    // 2. Generate the map pack once
                    _pack = GenerateTestMap();

                    _isInitialized = true;
                }
            }
        }

        private static MapPack GenerateTestMap()
        {
            var mapData = TestMapData.TestData;
            GridGenerator.Generate(mapData);
            VoronoiGenerator.CalculateVoronoi(mapData);
            HeightmapGenerator.Generate(mapData);
            FeatureModule.MarkupGrid(mapData);
            GlobeModule.DefineMapSize(mapData);
            GlobeModule.CalculateMapCoordinates(mapData);
            ClimateModule.CalculateTemperatures(mapData);
            ClimateModule.GeneratePrecipitation(mapData);

            var pack = PackModule.ReGraph(mapData);
            FeatureModule.MarkupPack(pack);
            RiverModule.Generate(pack);
            BiomModule.Define(pack);
            FeatureModule.DefineGroups(pack);
            FeatureModule.RankCells(pack);
            CultureModule.Generate(pack);
            CultureModule.ExpandCultures(pack);
            BurgModule.Generate(pack);

            // ACT: Generate States
            StateModule.Generate(pack);

            return pack;
        }

        [Fact]
        public void CreateStates_InitialProperties_MatchRegression()
        {
            foreach (var actualState in _pack.States)
            {
                Assert.True(_expectedDict.ContainsKey(actualState.Id), $"State ID {actualState.Id} not found in expected data.");
                var expectedState = _expectedDict[actualState.Id];

                // Base properties set strictly in CreateStates()
                Assert.Equal(expectedState.name, actualState.Name);
                Assert.Equal(expectedState.expansionism, actualState.Expansionism, 2);
                Assert.Equal(expectedState.capitalId, actualState.CapitalId);
                Assert.Equal(expectedState.centerCell, actualState.CenterCell);
                Assert.Equal(expectedState.cultureId, actualState.CultureId);

                // Type Mapping (CultureType Enum vs string)
                var actualStateType = actualState.Type == CultureType.Undefined ? null : actualState.Type.ToString();
                Assert.Equal(expectedState.type, actualStateType);
            }
        }

        [Fact]
        public void State_BasicProperties_MatchRegression()
        {
            foreach (var actualState in _pack.States)
            {
                Assert.True(_expectedDict.ContainsKey(actualState.Id), $"State ID {actualState.Id} not found in expected data.");
                var expectedState = _expectedDict[actualState.Id];

                Assert.Equal(expectedState.name, actualState.Name);
                Assert.Equal(expectedState.expansionism, actualState.Expansionism, 2);
                Assert.Equal(expectedState.capitalId, actualState.CapitalId);
                Assert.Equal(expectedState.centerCell, actualState.CenterCell);
                Assert.Equal(expectedState.cultureId, actualState.CultureId);
                Assert.Equal(expectedState.area, actualState.Area);
                Assert.Equal(expectedState.population, actualState.Population);
                Assert.Equal(expectedState.burgsCount, actualState.BurgsCount);
            }
        }


        [Fact]
        public void State_Colors_MatchRegression()
        {
            foreach (var actualState in _pack.States)
            {
                Assert.True(_expectedDict.ContainsKey(actualState.Id), $"State ID {actualState.Id} not found in expected data.");
                var expectedState = _expectedDict[actualState.Id];

                Assert.Equal(expectedState.color, actualState.Color);
            }
        }

        [Fact]
        public void Trace_ExpandStates_Divergence()
        {
            // Find the very first cell where the state assignment differs
            for (int i = 0; i < _pack.Cells.Length; i++)
            {
                int expectedState = _expectedData.cells_state[i];
                int actualState = _pack.Cells[i].StateId;

                if (expectedState != actualState)
                {
                    // Set a breakpoint here!
                    Assert.Fail($"DIVERGENCE FOUND at Cell {i}. JS says State {expectedState}, C# says State {actualState}.");
                }
            }
        }

        [Fact]
        public void StateCount_MatchesRegression()
        {
            Assert.Equal(_expectedData.states.Count, _pack.States.Count);
        }

        [Fact]
        public void GlobalCellExpansion_MatchesRegression()
        {
            Assert.Equal(_expectedData.cells_state.Length, _pack.Cells.Length);
            for (int i = 0; i < _pack.Cells.Length; i++)
            {
                Assert.Equal(_expectedData.cells_state[i], _pack.Cells[i].StateId);
            }
        }



        [Fact]
        public void StateFormsAndNames_MatchRegression()
        {
            foreach (var actualState in _pack.States)
            {
                var expectedState = _expectedDict[actualState.Id];

                var actualStateForm = actualState.Form == StateForm.Undefined ? null : actualState.Form.ToString();
                Assert.Equal(expectedState.formName, actualStateForm); // Matching your previous test logic
                Assert.Equal(expectedState.fullName, actualState.FullName);

                var actualStateType = actualState.Type == CultureType.Undefined ? null : actualState.Type.ToString();
                Assert.Equal(expectedState.type, actualStateType);
            }
        }

        [Fact]
        public void StateNeighbors_MatchRegression()
        {
            foreach (var actualState in _pack.States)
            {
                var expectedState = _expectedDict[actualState.Id];

                Assert.True(actualState.Neighbors.OrderBy(n => n).SequenceEqual(expectedState.neighbors.OrderBy(n => n)),
                    $"Neighbor mismatch for State {actualState.Name}");
            }
        }

        [Fact]
        public void StatePoles_MatchRegression()
        {
            foreach (var actualState in _pack.States)
            {
                var expectedState = _expectedDict[actualState.Id];

                Assert.Equal(expectedState.pole[0], actualState.Pole.X, 2);
                Assert.Equal(expectedState.pole[1], actualState.Pole.Y, 2);
            }
        }

        [Fact]
        public void StateDiplomacy_MatchesRegression()
        {
            foreach (var actualState in _pack.States)
            {
                var expectedState = _expectedDict[actualState.Id];

                if (expectedState.diplomacy != null && expectedState.diplomacy.Length > 0)
                {
                    Assert.Equal(expectedState.diplomacy.Length, actualState.Diplomacy.Length);
                    for (int d = 0; d < expectedState.diplomacy.Length; d++)
                    {
                        string expectedDip = expectedState.diplomacy[d] == "x" ? "None" : expectedState.diplomacy[d];
                        Assert.Equal(expectedDip, actualState.Diplomacy[d].ToString());
                    }
                }
            }
        }

        [Fact]
        public void StateCampaigns_MatchRegression()
        {
            foreach (var actualState in _pack.States)
            {
                var expectedState = _expectedDict[actualState.Id];

                if (expectedState.campaigns != null)
                {
                    Assert.Equal(expectedState.campaigns.Count, actualState.Campaigns.Count);
                    for (int c = 0; c < expectedState.campaigns.Count; c++)
                    {
                        Assert.Equal(expectedState.campaigns[c].name, actualState.Campaigns[c].Name);
                        Assert.Equal(expectedState.campaigns[c].start, actualState.Campaigns[c].Start);
                        Assert.Equal(expectedState.campaigns[c].end, actualState.Campaigns[c].End);
                    }
                }
            }
        }

        [Fact]
        public void GlobalDiplomacyChronicle_MatchesRegression()
        {
            Assert.Equal(_expectedData.chronicle.Count, _pack.DiplomacyChronicle.Count);
            for (int i = 0; i < _expectedData.chronicle.Count; i++)
            {
                Assert.True(_expectedData.chronicle[i].SequenceEqual(_pack.DiplomacyChronicle[i]),
                    $"Chronicle mismatch at index {i}");
            }
        }
    }
}