using MapGen.Core.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace MapGen.Core.Modules
{
    public static class StateModule
    {
        private static readonly string[] MonarchyForms = {
            "Duchy", "Grand Duchy", "Principality", "Kingdom", "Empire"
        };

        private static readonly string[] AdjForms = {
            "Empire", "Sultanate", "Khaganate", "Shogunate", "Caliphate", "Despotate",
            "Theocracy", "Oligarchy", "Union", "Confederation", "Trade Company",
            "League", "Tetrarchy", "Triumvirate", "Diarchy", "Horde", "Marches"
        };

        // Integer-weighted dictionary for the war types
        private static readonly Dictionary<string, int> WarsDict = new Dictionary<string, int>
        {
            { "War", 6 }, { "Conflict", 2 }, { "Campaign", 4 },
            { "Invasion", 2 }, { "Rebellion", 2 }, { "Conquest", 2 },
            { "Intervention", 1 }, { "Expedition", 1 }, { "Crusade", 1 }
        };

        private struct ExpansionNode
        {
            public int CellId;
            public double Cost;
            public int StateId;
            public byte NativeBiome;
        }

        public static void Generate(MapPack pack)
        {
            pack.States = CreateStates(pack);

            ExpandStates(pack);
            Normalize(pack);
            GetPoles(pack);           // Requires Polylabel/Centroid math
            FindNeighbors(pack);
            AssignColors(pack);
            GenerateCampaigns(pack);  // Requires Campaign model array on MapState
            GenerateDiplomacy(pack);  // Requires Diplomacy model array on MapState

            // --- Local Functions ---

            static List<MapState> CreateStates(MapPack pack)
            {
                var rng = pack.Rng;
                var burgs = pack.Burgs;
                var cultures = pack.Cultures;

                // State 0 is always "Neutrals"
                var states = new List<MapState>
                {
                    new MapState { Id = 0, Name = "Neutrals", Color = null }
                };

                double sizeVariety = MapConstants.STATE_SIZE_VARIETY;

                var each5th = Propability.Each(5);

                foreach (var burg in burgs)
                {
                    if (!burg.IsCapital) continue;

                    // JS: Math.random() is a double between 0 and 1. Use rng.NextDouble()
                    double expansionism = Math.Round(rng.Next() * sizeVariety + 1, 1);
                    //bool isEach5th = (each5thCounter++ % 5 == 0);

                    // JS: const basename = burg.name.length < 9 && each5th(burg.cell) ? ...
                    string basename = (burg.Name.Length < 9 && each5th(burg.Cell))
                        ? burg.Name
                        : NameModule.GetCultureShort(rng, cultures[burg.CultureId].BaseNameId);

                    string name = NameModule.GetStateName(rng, basename, cultures[burg.CultureId].BaseNameId);
                    CultureType type = cultures[burg.CultureId].Type;

                    var coa = new MapCoA { Type = type };

                    var state = new MapState
                    {
                        Id = burg.Id, // State ID in the list matches Capital Burg ID
                        Name = name,
                        Expansionism = expansionism,
                        CapitalId = burg.Id,
                        Type = type,
                        CenterCell = burg.Cell,
                        CultureId = burg.CultureId,
                        CoA = coa
                    };

                    burg.StateId = state.Id;
                    states.Add(state);
                }

                return states;
            }
        }

        #region Expansion

        private static void ExpandStates(MapPack pack)
        {
            var cells = pack.Cells;
            var states = pack.States;
            var cultures = pack.Cultures;
            var burgs = pack.Burgs;

            // C# 10+ PriorityQueue is perfect for Azgaar's FlatQueue
            var queue = new PriorityQueue<ExpansionNode, double>();
            var cost = new double[cells.Length];

            var bioms = BiomModule.GetDefaultBiomes();

            double globalGrowthRate = MapConstants.STATE_GLOBAL_GROWTH_RATE;
            double statesGrowthRate = MapConstants.STATE_GROWTH_RATE;
            double growthRate = (cells.Length / 2.0) * globalGrowthRate * statesGrowthRate;

            // Clear state from all cells initially
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i].StateId = 0;
                cost[i] = double.MaxValue;
            }

            var stateDict = states.ToDictionary(s => s.Id);

            // Queue initial capitals
            foreach (var state in states)
            {
                if (state.Id == 0) continue; // Skip Neutrals

                int capitalCell = burgs[state.CapitalId - 1].Cell; // Remember: Burg Id is 1-based, list is 0-based
                cells[capitalCell].StateId = state.Id;

                int cultureCenter = cultures[state.CultureId].CenterCell;
                byte nativeBiome = cells[cultureCenter].BiomeId;

                queue.Enqueue(new ExpansionNode { CellId = state.CenterCell, Cost = 0, StateId = state.Id, NativeBiome = nativeBiome }, 0);
                cost[state.CenterCell] = 1;
            }

            while (queue.TryDequeue(out ExpansionNode next, out double p))
            {
                int e = next.CellId;
                int s = next.StateId;
                byte b = next.NativeBiome;

                var state = stateDict[s];
                CultureType type = state.Type;
                int culture = state.CultureId;

                foreach (int neighbor in cells[e].NeighborCells)
                {
                    // Do not overwrite capital cells
                    if (cells[neighbor].StateId != 0 && cells[neighbor].BurgId > 0 && burgs[cells[neighbor].BurgId - 1].IsCapital)
                        continue;

                    double cultureCost = (culture == cells[neighbor].CultureId) ? -9 : 100;
                    double populationCost = cells[neighbor].Height < 20 ? 0 :
                        (cells[neighbor].Suitability > 0 ? Math.Max(20 - cells[neighbor].Suitability, 0) : 5000);

                    double biomeCost = GetBiomeCost(b, cells[neighbor].BiomeId, type, bioms);
                    double heightCost = GetHeightCost(cells[neighbor], pack, type);
                    double riverCost = GetRiverCost(cells[neighbor], type);
                    double typeCost = GetTypeCost(cells[neighbor], type);

                    double cellCost = Math.Max(cultureCost + populationCost + biomeCost + heightCost + riverCost + typeCost, 0);
                    double totalCost = p + 10 + (cellCost / state.Expansionism);

                    if (neighbor == 585)
                    {
                        Console.WriteLine($"[C#] Cell {e} eval by State {s} (from Cell {e})");
                        Console.WriteLine($"     Cul:{cultureCost}, Pop:{populationCost}, Bio:{biomeCost}, Hgt:{heightCost}, Riv:{riverCost}, Typ:{typeCost}");
                        Console.WriteLine($"     CellCost: {cellCost} / Exp: {state.Expansionism} = {cellCost / state.Expansionism}");
                        Console.WriteLine($"     TotalCost: {totalCost} (Previous path cost 'p': {p})");
                    }

                    if (totalCost > growthRate) continue;

                    if (totalCost < cost[neighbor])
                    {
                        if (cells[neighbor].Height >= 20) cells[neighbor].StateId = s;
                        cost[neighbor] = totalCost;
                        queue.Enqueue(new ExpansionNode { CellId = neighbor, Cost = totalCost, StateId = s, NativeBiome = b }, totalCost);
                    }
                }
            }

            // Reassign non-capital burgs to the state that captured their cell
            foreach (var burg in burgs)
            {
                burg.StateId = cells[burg.Cell].StateId;
            }
        }

        private static void Normalize(MapPack pack)
        {
            var cells = pack.Cells;
            var burgs = pack.Burgs;

            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i].Height < 20 || cells[i].BurgId > 0) continue;

                // Do not overwrite near capitals
                bool nearCapital = cells[i].NeighborCells.Any(c => cells[c].BurgId > 0 && burgs[cells[c].BurgId - 1].IsCapital);
                if (nearCapital) continue;

                var landNeighbors = cells[i].NeighborCells.Where(c => cells[c].Height >= 20).ToList();
                var adversaries = landNeighbors.Where(c => cells[c].StateId != cells[i].StateId).ToList();
                if (adversaries.Count < 2) continue;

                var buddies = landNeighbors.Where(c => cells[c].StateId == cells[i].StateId).ToList();
                if (buddies.Count > 2) continue;
                if (adversaries.Count <= buddies.Count) continue;

                cells[i].StateId = cells[adversaries[0]].StateId;
            }
        }

        private static void FindNeighbors(MapPack pack)
        {
            var cells = pack.Cells;
            var stateDict = pack.States.ToDictionary(s => s.Id);

            // Ensure all states start with an empty list
            foreach (var state in pack.States)
            {
                if (state.Neighbors == null)
                    state.Neighbors = new List<int>();
                else
                    state.Neighbors.Clear();
            }

            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i].Height < 20) continue;

                int s = cells[i].StateId;
                if (!stateDict.ContainsKey(s)) continue;

                foreach (int c in cells[i].NeighborCells)
                {
                    // If neighbor is land and belongs to a different state (including State 0)
                    if (cells[c].Height >= 20 && cells[c].StateId != s)
                    {
                        int neighborState = cells[c].StateId;

                        // Add the neighbor if we haven't recorded it yet
                        if (!stateDict[s].Neighbors.Contains(neighborState))
                        {
                            stateDict[s].Neighbors.Add(neighborState);
                        }
                    }
                }
            }
        }

        private static void AssignColors(MapPack pack)
        {
            var states = pack.States;
            var colors = new List<string> { "#66c2a5", "#fc8d62", "#8da0cb", "#e78ac3", "#a6d854", "#ffd92f" };
            var stateDict = states.ToDictionary(s => s.Id);

            // 1. Assign basic color using greedy coloring algorithm
            foreach (var state in states)
            {
                if (state.Id == 0) continue; // JS: if (!state.i ...) return;

                string assignedColor = colors.FirstOrDefault(color =>
                    state.Neighbors.All(neibId => !stateDict.ContainsKey(neibId) || stateDict[neibId].Color != color));

                if (string.IsNullOrEmpty(assignedColor))
                {
                    assignedColor = ColorUtils.GetRandomColor(pack.Rng);
                }

                state.Color = assignedColor;

                // JS: colors.push(colors.shift());
                // MUST move Index 0 to the back unconditionally!
                string firstColor = colors[0];
                colors.RemoveAt(0);
                colors.Add(firstColor);
            }

            // 2. Randomize each already used color a bit
            // JS iterates over 'colors' in its currently shifted state
            foreach (var c in colors.ToList())
            {
                var sameColored = states.Where(state => state.Id != 0 && state.Color == c).ToList();
                for (int index = 0; index < sameColored.Count; index++)
                {
                    if (index == 0) continue; // JS: if (!index) return;

                    sameColored[index].Color = ColorUtils.GetMixedColor(sameColored[index].Color, pack.Rng);
                }
            }
        }

        public static void GetPoles(MapPack pack)
        {
            var cells = pack.Cells;
            var states = pack.States;

            // JS: const poles = getPolesOfInaccessibility(pack, i => cells.state[i]);
            var poles = PathUtils.GetPolesOfInaccessibility(pack, i => cells[i].StateId);

            foreach (var state in states)
            {
                if (state.Id == 0) continue; // Skip Neutrals

                // JS: s.pole = poles[s.i] || s.center;
                if (poles.TryGetValue(state.Id, out var polePoint))
                {
                    // Convert MapPoint to double[] array formatted to 2 decimal places (JS rn() equivalent)
                    state.Pole = new MapPoint(Math.Round(polePoint.X, 2), Math.Round(polePoint.Y, 2));
                }
                else
                {
                    // Fallback to the State's center cell if no pole could be calculated
                    var centerPoint = cells[state.CenterCell].Point;
                    state.Pole = new MapPoint(Math.Round(centerPoint.X, 2), Math.Round(centerPoint.Y, 2));
                }
            }
        }

        // --- Helper Methods for ExpandStates ---

        private static double GetBiomeCost(byte nativeBiome, byte currentBiome, CultureType type, List<BiomeDefinition> bioms)
        {
            if (nativeBiome == currentBiome) return 10;

            double cost = currentBiome < bioms.Count ? bioms[currentBiome].MovementCost : 50;

            if (type == CultureType.Hunting) return cost * 2;
            if (type == CultureType.Nomadic && currentBiome > 4 && currentBiome < 10) return cost * 3;

            return cost;
        }

        private static double GetHeightCost(MapCell cell, MapPack pack, CultureType type)
        {
            byte h = cell.Height;
            bool isLake = cell.FeatureId > 0 && pack.GetFeature(cell.FeatureId).Type == FeatureType.Lake;

            if (type == CultureType.Lake && isLake) return 10;
            if (type == CultureType.Naval && h < 20) return 300;
            if (type == CultureType.Nomadic && h < 20) return 10000;
            if (h < 20) return 1000;
            if (type == CultureType.Highland && h < 62) return 1100;
            if (type == CultureType.Highland) return 0;
            if (h >= 67) return 2200;
            if (h >= 44) return 300;
            return 0;
        }

        private static double GetRiverCost(MapCell cell, CultureType type)
        {
            if (type == CultureType.River) return cell.RiverId > 0 ? 0 : 100;
            if (cell.RiverId == 0) return 0;
            return Math.Clamp(cell.Flux / 10.0, 20, 100);
        }

        private static double GetTypeCost(MapCell cell, CultureType type)
        {
            int t = cell.Distance; // 1 = coast, >1 = mainland, <0 = water
            if (t == 1) return (type == CultureType.Naval || type == CultureType.Lake) ? 0 : (type == CultureType.Nomadic ? 60 : 20);
            if (t == 2) return (type == CultureType.Naval || type == CultureType.Nomadic) ? 30 : 0;
            if (t != -1) return (type == CultureType.Naval || type == CultureType.Lake) ? 100 : 0;
            return 0;
        }

        #endregion

        #region Diplomacy

        public static void GenerateDiplomacy(MapPack pack)
        {
            var cells = pack.Cells;
            var states = pack.States;
            var valid = states.Where(s => s.Id > 0).ToList();

            var neibs = new Dictionary<DiplomacyRelation, int> { { DiplomacyRelation.Ally, 1 }, { DiplomacyRelation.Friendly, 2 }, { DiplomacyRelation.Neutral, 1 }, { DiplomacyRelation.Suspicion, 10 }, { DiplomacyRelation.Rival, 9 } };
            var neibsOfNeibs = new Dictionary<DiplomacyRelation, int> { { DiplomacyRelation.Ally, 10 }, { DiplomacyRelation.Friendly, 8 }, { DiplomacyRelation.Neutral, 5 }, { DiplomacyRelation.Suspicion, 1 } };
            var far = new Dictionary<DiplomacyRelation, int> { { DiplomacyRelation.Friendly, 1 }, { DiplomacyRelation.Neutral, 12 }, { DiplomacyRelation.Suspicion, 2 }, { DiplomacyRelation.Unknown, 6 } };
            var navals = new Dictionary<DiplomacyRelation, int> { { DiplomacyRelation.Neutral, 1 }, { DiplomacyRelation.Suspicion, 2 }, { DiplomacyRelation.Rival, 1 }, { DiplomacyRelation.Unknown, 1 } };

            foreach (var s in valid)
            {
                s.Diplomacy = Enumerable.Repeat(DiplomacyRelation.None, states.Count).ToArray();
            }

            if (valid.Count < 2) return;
            var rng = pack.Rng;

            // BUG REPLICATION: d3.mean([undefined]) returns undefined (NaN)
            double areaMean = double.NaN;

            for (int f = 1; f < states.Count; f++)
            {
                if (states[f].Id == 0) continue;

                int suzerainF = Array.IndexOf(states[f].Diplomacy, DiplomacyRelation.Vassal);
                if (suzerainF > 0)
                {
                    for (int i = 1; i < states.Count; i++)
                    {
                        if (i == f || i == suzerainF) continue;
                        states[f].Diplomacy[i] = states[suzerainF].Diplomacy[i];
                        if (states[suzerainF].Diplomacy[i] == DiplomacyRelation.Suzerain) states[f].Diplomacy[i] = DiplomacyRelation.Ally;

                        for (int e = 1; e < states.Count; e++)
                        {
                            if (e == f || e == suzerainF) continue;
                            if (states[e].Diplomacy[suzerainF] == DiplomacyRelation.Suzerain || states[e].Diplomacy[suzerainF] == DiplomacyRelation.Vassal) continue;
                            states[e].Diplomacy[f] = states[e].Diplomacy[suzerainF];
                        }
                    }
                    continue;
                }

                for (int t = f + 1; t < states.Count; t++)
                {
                    if (states[t].Id == 0) continue;

                    int suzerainT = Array.IndexOf(states[t].Diplomacy, DiplomacyRelation.Vassal);
                    if (suzerainT > 0)
                    {
                        states[f].Diplomacy[t] = states[f].Diplomacy[suzerainT];
                        continue;
                    }

                    bool naval = states[f].Type == CultureType.Naval && states[t].Type == CultureType.Naval &&
                                 cells[states[f].CenterCell].FeatureId != cells[states[t].CenterCell].FeatureId;

                    bool neib = !naval && states[f].Neighbors.Contains(t);

                    // AZGAAR STRING JOIN BUG REPLICATION
                    string jsBugString = string.Join("", states[f].Neighbors.Select(n => string.Join(",", states[n].Neighbors)));
                    bool neibOfNeib = !naval && !neib && jsBugString.Contains(t.ToString());

                    DiplomacyRelation status = naval ? rng.Rw(navals) :
                                               neib ? rng.Rw(neibs) :
                                               neibOfNeib ? rng.Rw(neibsOfNeibs) : rng.Rw(far);

                    // BUG REPLICATION: NaN bypasses the vassal area checks
                    if (neib && rng.P(0.8) && double.NaN > areaMean && double.NaN < areaMean && (double.NaN / Math.Max(double.NaN, 1)) > 2)
                    {
                        status = DiplomacyRelation.Vassal;
                    }

                    states[f].Diplomacy[t] = status == DiplomacyRelation.Vassal ? DiplomacyRelation.Suzerain : status;
                    states[t].Diplomacy[f] = status;
                }
            }

            int currentYear = pack.Options.Year;
            pack.DiplomacyChronicle.Clear();

            for (int attacker = 1; attacker < states.Count; attacker++)
            {
                if (states[attacker].Id == 0) continue;

                var ad = states[attacker].Diplomacy;
                if (!ad.Contains(DiplomacyRelation.Rival) || ad.Contains(DiplomacyRelation.Vassal) || ad.Contains(DiplomacyRelation.Enemy)) continue;

                var possibleDefenders = ad.Select((r, d) => r == DiplomacyRelation.Rival && !states[d].Diplomacy.Contains(DiplomacyRelation.Vassal) ? d : 0).Where(d => d != 0).ToList();
                if (possibleDefenders.Count == 0) continue;

                int defender = rng.Ra(possibleDefenders.ToArray());

                // BUG REPLICATION: undefined * exp = NaN. NaN < NaN evaluates to false.
                double ap = double.NaN;
                double dp = double.NaN;
                if (ap < dp * rng.Gauss(1.6, 0.8, 0, 10, 2)) continue;

                var attackers = new List<int> { attacker };
                var defenders = new List<int> { defender };
                var dd = states[defender].Diplomacy;

                string an = states[attacker].Name;
                string dn = states[defender].Name;
                string trimmedDn = LanguageUtils.TrimVowels(dn);

                string warName = $"{an}-{trimmedDn}ian War";
                int startYear = currentYear - (int)Math.Floor(rng.Gauss(2, 3, 0, 10) + 0.5);

                var warRecord = new List<string> { warName, $"{an} declared a war on its rival {dn}" };

                var campaign = new MapCampaign { Name = warName, Start = startYear, Attacker = attacker, Defender = defender };
                states[attacker].Campaigns.Add(campaign);
                states[defender].Campaigns.Add(campaign);

                for (int d = 1; d < ad.Length; d++)
                {
                    if (ad[d] == DiplomacyRelation.Suzerain) { attackers.Add(d); warRecord.Add($"{an}'s vassal {states[d].Name} joined the war on attackers side"); }
                }
                for (int d = 1; d < dd.Length; d++)
                {
                    if (dd[d] == DiplomacyRelation.Suzerain) { defenders.Add(d); warRecord.Add($"{dn}'s vassal {states[d].Name} joined the war on defenders side"); }
                }

                // BUG REPLICATION: d3.sum([NaN]) strips out invalid values and returns 0!
                ap = 0;
                dp = 0;

                // Defender allies join
                for (int d = 1; d < dd.Length; d++)
                {
                    if (dd[d] != DiplomacyRelation.Ally || states[d].Diplomacy.Contains(DiplomacyRelation.Vassal)) continue;

                    // 0/0 naturally evaluates to NaN. NaN > value is false.
                    if (states[d].Diplomacy[attacker] != DiplomacyRelation.Rival && (ap / dp) > 2 * rng.Gauss(1.6, 0.8, 0, 10, 2))
                    {
                        string reason = states[d].Diplomacy.Contains(DiplomacyRelation.Enemy) ? "Being already at war," : $"Frightened by {an},";
                        warRecord.Add($"{reason} {states[d].Name} severed the defense pact with {dn}");
                        dd[d] = states[d].Diplomacy[defender] = DiplomacyRelation.Suspicion;
                        continue;
                    }

                    defenders.Add(d);

                    // BUG REPLICATION: dp += NaN immediately turns dp into NaN for the rest of the loop!
                    dp += double.NaN;
                    warRecord.Add($"{dn}'s ally {states[d].Name} joined the war on defenders side");

                    for (int v = 1; v < states[d].Diplomacy.Length; v++)
                    {
                        if (states[d].Diplomacy[v] == DiplomacyRelation.Suzerain)
                        {
                            defenders.Add(v);
                            dp += double.NaN;
                            warRecord.Add($"{states[d].Name}'s vassal {states[v].Name} joined the war on defenders side");
                        }
                    }
                }

                // Attacker allies join
                for (int d = 1; d < ad.Length; d++)
                {
                    if (ad[d] != DiplomacyRelation.Ally || states[d].Diplomacy.Contains(DiplomacyRelation.Vassal) || defenders.Contains(d)) continue;
                    string allyName = states[d].Name;

                    // If dp became NaN earlier, 0 <= NaN is false, forcing the ally to join!
                    if (states[d].Diplomacy[defender] != DiplomacyRelation.Rival && (rng.P(0.2) || ap <= dp * 1.2))
                    {
                        warRecord.Add($"{an}'s ally {allyName} avoided entering the war");
                        continue;
                    }

                    bool alliesOnBothSides = false;
                    for (int aIdx = 1; aIdx < states[d].Diplomacy.Length; aIdx++)
                    {
                        if (states[d].Diplomacy[aIdx] == DiplomacyRelation.Ally && defenders.Contains(aIdx))
                        {
                            alliesOnBothSides = true;
                            break;
                        }
                    }

                    if (alliesOnBothSides)
                    {
                        warRecord.Add($"{an}'s ally {allyName} did not join the war as its allies are in war on both sides");
                        continue;
                    }

                    attackers.Add(d);
                    ap += double.NaN;
                    warRecord.Add($"{an}'s ally {allyName} joined the war on attackers side");

                    for (int v = 1; v < states[d].Diplomacy.Length; v++)
                    {
                        if (states[d].Diplomacy[v] == DiplomacyRelation.Suzerain)
                        {
                            attackers.Add(v);
                            dp += double.NaN; // PRESERVING AZGAAR'S DP TYPO!
                            warRecord.Add($"{states[d].Name}'s vassal {states[v].Name} joined the war on attackers side");
                        }
                    }
                }

                foreach (int a in attackers)
                {
                    foreach (int def in defenders)
                    {
                        states[a].Diplomacy[def] = states[def].Diplomacy[a] = DiplomacyRelation.Enemy;
                    }
                }
                pack.DiplomacyChronicle.Add(warRecord);
            }
        }

        public static void GenerateDiplomacyWithoutNaNBug(MapPack pack)
        {
            var cells = pack.Cells;
            var states = pack.States;
            var valid = states.Where(s => s.Id > 0).ToList();

            var neibs = new Dictionary<DiplomacyRelation, int> { { DiplomacyRelation.Ally, 1 }, { DiplomacyRelation.Friendly, 2 }, { DiplomacyRelation.Neutral, 1 }, { DiplomacyRelation.Suspicion, 10 }, { DiplomacyRelation.Rival, 9 } };
            var neibsOfNeibs = new Dictionary<DiplomacyRelation, int> { { DiplomacyRelation.Ally, 10 }, { DiplomacyRelation.Friendly, 8 }, { DiplomacyRelation.Neutral, 5 }, { DiplomacyRelation.Suspicion, 1 } };
            var far = new Dictionary<DiplomacyRelation, int> { { DiplomacyRelation.Friendly, 1 }, { DiplomacyRelation.Neutral, 12 }, { DiplomacyRelation.Suspicion, 2 }, { DiplomacyRelation.Unknown, 6 } };
            var navals = new Dictionary<DiplomacyRelation, int> { { DiplomacyRelation.Neutral, 1 }, { DiplomacyRelation.Suspicion, 2 }, { DiplomacyRelation.Rival, 1 }, { DiplomacyRelation.Unknown, 1 } };

            foreach (var s in valid)
            {
                s.Diplomacy = Enumerable.Repeat(DiplomacyRelation.None, states.Count).ToArray();
            }

            if (valid.Count < 2) return;
            double areaMean = valid.Average(s => s.Area);
            var rng = pack.Rng;

            for (int f = 1; f < states.Count; f++)
            {
                if (states[f].Id == 0) continue;

                int suzerainF = Array.IndexOf(states[f].Diplomacy, DiplomacyRelation.Vassal);
                if (suzerainF > 0)
                {
                    for (int i = 1; i < states.Count; i++)
                    {
                        if (i == f || i == suzerainF) continue;
                        states[f].Diplomacy[i] = states[suzerainF].Diplomacy[i];
                        if (states[suzerainF].Diplomacy[i] == DiplomacyRelation.Suzerain) states[f].Diplomacy[i] = DiplomacyRelation.Ally;

                        for (int e = 1; e < states.Count; e++)
                        {
                            if (e == f || e == suzerainF) continue;
                            if (states[e].Diplomacy[suzerainF] == DiplomacyRelation.Suzerain || states[e].Diplomacy[suzerainF] == DiplomacyRelation.Vassal) continue;
                            states[e].Diplomacy[f] = states[e].Diplomacy[suzerainF];
                        }
                    }
                    continue;
                }

                for (int t = f + 1; t < states.Count; t++)
                {
                    if (states[t].Id == 0) continue;

                    int suzerainT = Array.IndexOf(states[t].Diplomacy, DiplomacyRelation.Vassal);
                    if (suzerainT > 0)
                    {
                        states[f].Diplomacy[t] = states[f].Diplomacy[suzerainT];
                        continue;
                    }

                    bool naval = states[f].Type == CultureType.Naval && states[t].Type == CultureType.Naval &&
                                 cells[states[f].CenterCell].FeatureId != cells[states[t].CenterCell].FeatureId;

                    bool neib = !naval && states[f].Neighbors.Contains(t);

                    // AZGAAR STRING JOIN BUG REPLICATION
                    string jsBugString = string.Join("", states[f].Neighbors.Select(n => string.Join(",", states[n].Neighbors)));
                    bool neibOfNeib = !naval && !neib && jsBugString.Contains(t.ToString());

                    DiplomacyRelation status = naval ? rng.Rw(navals) :
                                               neib ? rng.Rw(neibs) :
                                               neibOfNeib ? rng.Rw(neibsOfNeibs) : rng.Rw(far);

                    // INLINED rng.P(0.8) to perfectly preserve JS && short-circuiting
                    if (neib && rng.P(0.8) && states[f].Area > areaMean && states[t].Area < areaMean && ((double)states[f].Area / (double)Math.Max(states[t].Area, 1)) > 2)
                    {
                        status = DiplomacyRelation.Vassal;
                    }

                    states[f].Diplomacy[t] = status == DiplomacyRelation.Vassal ? DiplomacyRelation.Suzerain : status;
                    states[t].Diplomacy[f] = status;
                }
            }

            int currentYear = pack.Options.Year;
            pack.DiplomacyChronicle.Clear();

            for (int attacker = 1; attacker < states.Count; attacker++)
            {
                if (states[attacker].Id == 0) continue;

                Console.WriteLine($"[C#] Attacker {attacker} start | Mochania Dip: {string.Join(",", states[2].Diplomacy)}");

                var ad = states[attacker].Diplomacy;
                if (!ad.Contains(DiplomacyRelation.Rival) || ad.Contains(DiplomacyRelation.Vassal) || ad.Contains(DiplomacyRelation.Enemy)) continue;

                var possibleDefenders = ad.Select((r, d) => r == DiplomacyRelation.Rival && !states[d].Diplomacy.Contains(DiplomacyRelation.Vassal) ? d : 0).Where(d => d != 0).ToList();
                if (possibleDefenders.Count == 0) continue;

                int defender = rng.Ra(possibleDefenders.ToArray());

                double ap = states[attacker].Area * states[attacker].Expansionism;
                double dp = states[defender].Area * states[defender].Expansionism;
                if (ap < dp * rng.Gauss(1.6, 0.8, 0, 10, 2)) continue;

                var attackers = new List<int> { attacker };
                var defenders = new List<int> { defender };
                var dd = states[defender].Diplomacy;

                string an = states[attacker].Name;
                string dn = states[defender].Name;
                string trimmedDn = LanguageUtils.TrimVowels(dn);

                string warName = $"{an}-{trimmedDn}ian War";
                int startYear = currentYear - (int)Math.Floor(rng.Gauss(2, 3, 0, 10) + 0.5);

                var warRecord = new List<string> { warName, $"{an} declared a war on its rival {dn}" };

                var campaign = new MapCampaign { Name = warName, Start = startYear, Attacker = attacker, Defender = defender };
                states[attacker].Campaigns.Add(campaign);
                states[defender].Campaigns.Add(campaign);

                for (int d = 1; d < ad.Length; d++)
                {
                    if (ad[d] == DiplomacyRelation.Suzerain) { attackers.Add(d); warRecord.Add($"{an}'s vassal {states[d].Name} joined the war on attackers side"); }
                }
                for (int d = 1; d < dd.Length; d++)
                {
                    if (dd[d] == DiplomacyRelation.Suzerain) { defenders.Add(d); warRecord.Add($"{dn}'s vassal {states[d].Name} joined the war on defenders side"); }
                }

                ap = attackers.Sum(a => states[a].Area * states[a].Expansionism);
                dp = defenders.Sum(d => states[d].Area * states[d].Expansionism);

                // Defender allies join
                for (int d = 1; d < dd.Length; d++)
                {
                    if (dd[d] != DiplomacyRelation.Ally || states[d].Diplomacy.Contains(DiplomacyRelation.Vassal)) continue;

                    // Native double division. 0/0 naturally evaluates to NaN, instantly failing the > check just like JS!
                    if (states[d].Diplomacy[attacker] != DiplomacyRelation.Rival && (ap / dp) > 2 * rng.Gauss(1.6, 0.8, 0, 10, 2))
                    {
                        string reason = states[d].Diplomacy.Contains(DiplomacyRelation.Enemy) ? "Being already at war," : $"Frightened by {an},";
                        warRecord.Add($"{reason} {states[d].Name} severed the defense pact with {dn}");
                        dd[d] = states[d].Diplomacy[defender] = DiplomacyRelation.Suspicion;
                        continue;
                    }

                    defenders.Add(d);
                    dp += states[d].Area * states[d].Expansionism;
                    warRecord.Add($"{dn}'s ally {states[d].Name} joined the war on defenders side");

                    for (int v = 1; v < states[d].Diplomacy.Length; v++)
                    {
                        if (states[d].Diplomacy[v] == DiplomacyRelation.Suzerain)
                        {
                            defenders.Add(v);
                            dp += states[v].Area * states[v].Expansionism;
                            warRecord.Add($"{states[d].Name}'s vassal {states[v].Name} joined the war on defenders side");
                        }
                    }
                }

                // Attacker allies join
                for (int d = 1; d < ad.Length; d++)
                {
                    // Inside the Attacker Allies loop: for (int d = 1; d < ad.Length; d++)
                    if (attacker == 5 && d == 17)
                    {
                        // HOVER OVER THESE:
                        // 1. ap (Attacker Power)
                        // 2. dp (Defender Power)
                        // 3. ap <= dp * 1.2
                        System.Diagnostics.Debugger.Break();
                    }


                    if (ad[d] != DiplomacyRelation.Ally || states[d].Diplomacy.Contains(DiplomacyRelation.Vassal) || defenders.Contains(d)) continue;
                    string allyName = states[d].Name;
                    var checkDimplomacy = states[d].Diplomacy[defender];

                    // INLINED rng.P(0.2) to strictly maintain short-circuiting parity!
                    if (checkDimplomacy != DiplomacyRelation.Rival && (rng.P(0.2) || ap <= dp * 1.2))
                    {
                        warRecord.Add($"{an}'s ally {allyName} avoided entering the war");
                        continue;
                    }

                    bool alliesOnBothSides = false;
                    for (int aIdx = 1; aIdx < states[d].Diplomacy.Length; aIdx++)
                    {
                        if (states[d].Diplomacy[aIdx] == DiplomacyRelation.Ally && defenders.Contains(aIdx))
                        {
                            alliesOnBothSides = true;
                            break;
                        }
                    }

                    if (alliesOnBothSides)
                    {
                        warRecord.Add($"{an}'s ally {allyName} did not join the war as its allies are in war on both sides");
                        continue;
                    }

                    attackers.Add(d);
                    ap += states[d].Area * states[d].Expansionism;
                    warRecord.Add($"{an}'s ally {allyName} joined the war on attackers side");

                    for (int v = 1; v < states[d].Diplomacy.Length; v++)
                    {
                        if (states[d].Diplomacy[v] == DiplomacyRelation.Suzerain)
                        {
                            attackers.Add(v);
                            dp += states[v].Area * states[v].Expansionism; // PRESERVING AZGAAR'S DP TYPO!
                            warRecord.Add($"{states[d].Name}'s vassal {states[v].Name} joined the war on attackers side");
                        }
                    }
                }

                foreach (int a in attackers)
                {
                    foreach (int def in defenders)
                    {
                        states[a].Diplomacy[def] = states[def].Diplomacy[a] = DiplomacyRelation.Enemy;
                    }
                }
                pack.DiplomacyChronicle.Add(warRecord);
            }
        }

        #endregion

        #region State Forms

        public static void DefineStateForms(MapPack pack)
        {
            var states = pack.States.Where(s => s.Id > 0).ToList();
            if (states.Count < 1) return;
            var rng = pack.Rng;

            // Integer weights with StateForm Enum
            var generic = new Dictionary<StateForm, int> { { StateForm.Monarchy, 25 }, { StateForm.Republic, 2 }, { StateForm.Union, 1 } };
            var naval = new Dictionary<StateForm, int> { { StateForm.Monarchy, 25 }, { StateForm.Republic, 8 }, { StateForm.Union, 3 } };

            var sortedAreas = states.Select(s => s.Area).OrderByDescending(a => a).ToList();
            double median = sortedAreas[sortedAreas.Count / 2];
            int expIndex = Math.Max((int)Math.Ceiling(Math.Pow(states.Count, 0.4)) - 2, 0);
            int empireMin = sortedAreas.Count > expIndex ? sortedAreas[expIndex] : 0;

            int[] expTiers = new int[pack.States.Count];
            foreach (var s in states)
            {
                int tier = Math.Min((int)Math.Floor((s.Area / Math.Max(median, 1)) * 2.6), 4);
                if (tier == 4 && s.Area < empireMin) tier = 3;
                expTiers[s.Id] = tier;
            }

            foreach (var s in states)
            {
                int tier = expTiers[s.Id];

                // Religion logic skipped for now, mocked probabilities using rng.Next() double
                bool isTheocracy = rng.Next() < 0.1;
                bool isAnarchy = rng.Next() < (0.01 - (tier / 500.0));

                if (isTheocracy) s.Form = StateForm.Theocracy;
                else if (isAnarchy) s.Form = StateForm.Anarchy;
                else s.Form = s.Type == CultureType.Naval ? rng.Rw(naval) : rng.Rw(generic);

                s.FormName = SelectForm(s, tier, pack);
                s.FullName = GetFullName(s, rng);
            }
        }

        private static string SelectForm(MapState s, int tier, MapPack pack)
        {
            int baseCulture = pack.Cultures[s.CultureId].BaseNameId;
            var rng = pack.Rng;

            if (s.Form == StateForm.Monarchy)
            {
                string form = MonarchyForms[tier];
                if (s.Diplomacy != null)
                {
                    // Random 0-5 integer via NextDouble projection since Next() returns double
                    int rand6 = (int)Math.Floor(rng.Next() * 6);
                    if (form == "Duchy" && s.Neighbors.Count > 1 && rand6 < s.Neighbors.Count && s.Diplomacy.Contains(DiplomacyRelation.Vassal)) return "Marches";
                    if (baseCulture == 1 && rng.Next() < 0.3 && s.Diplomacy.Contains(DiplomacyRelation.Vassal)) return "Dominion";
                    if (rng.Next() < 0.3 && s.Diplomacy.Contains(DiplomacyRelation.Vassal)) return "Protectorate";
                }

                if (baseCulture == 31 && (form == "Empire" || form == "Kingdom")) return "Khanate";
                if (baseCulture == 16 && form == "Principality") return "Beylik";
                if (baseCulture == 5 && (form == "Empire" || form == "Kingdom")) return "Tsardom";
                if (baseCulture == 16 && (form == "Empire" || form == "Kingdom")) return "Khaganate";
                if (baseCulture == 12 && (form == "Kingdom" || form == "Grand Duchy")) return "Shogunate";
                if ((baseCulture == 18 || baseCulture == 17) && form == "Empire") return "Caliphate";
                if (baseCulture == 18 && (form == "Grand Duchy" || form == "Duchy")) return "Emirate";
                if (baseCulture == 7 && (form == "Grand Duchy" || form == "Duchy")) return "Despotate";
                if (baseCulture == 31 && (form == "Grand Duchy" || form == "Duchy")) return "Ulus";
                if (baseCulture == 16 && (form == "Grand Duchy" || form == "Duchy")) return "Horde";
                if (baseCulture == 24 && (form == "Grand Duchy" || form == "Duchy")) return "Satrapy";

                return form;
            }

            if (s.Form == StateForm.Republic)
            {
                if (tier < 2 && s.BurgsCount == 1)
                {
                    if (LanguageUtils.TrimVowels(s.Name) == LanguageUtils.TrimVowels(pack.Burgs[s.CapitalId - 1].Name))
                    {
                        s.Name = pack.Burgs[s.CapitalId - 1].Name;
                        return "Free City";
                    }
                    if (rng.Next() < 0.3) return "City-state";
                }

                // Integer weights mirroring Azgaar's objects exactly
                var republic = new Dictionary<string, int> { { "Republic", 75 }, { "Federation", 4 }, { "Trade Company", 4 }, { "Most Serene Republic", 2 }, { "Oligarchy", 2 }, { "Tetrarchy", 1 }, { "Triumvirate", 1 }, { "Diarchy", 1 }, { "Junta", 1 } };
                return rng.Rw(republic);
            }

            if (s.Form == StateForm.Union)
            {
                var union = new Dictionary<string, int> { { "Union", 3 }, { "League", 4 }, { "Confederation", 1 }, { "United Kingdom", 1 }, { "United Republic", 1 }, { "United Provinces", 2 }, { "Commonwealth", 1 }, { "Heptarchy", 1 } };
                return rng.Rw(union);
            }

            if (s.Form == StateForm.Anarchy)
            {
                var anarchy = new Dictionary<string, int> { { "Free Territory", 2 }, { "Council", 3 }, { "Commune", 1 }, { "Community", 1 } };
                return rng.Rw(anarchy);
            }

            if (s.Form == StateForm.Theocracy)
            {
                var europeans = new HashSet<int> { 0, 1, 2, 3, 4, 6, 8, 9, 13, 15, 20 };
                if (europeans.Contains(baseCulture))
                {
                    if (rng.Next() < 0.1) return "Divine " + MonarchyForms[tier];
                    if (tier < 2 && rng.Next() < 0.5) return "Diocese";
                    if (tier < 2 && rng.Next() < 0.5) return "Bishopric";
                }
                if (rng.Next() < 0.9 && (baseCulture == 7 || baseCulture == 5))
                {
                    if (tier < 2) return "Eparchy";
                    if (tier == 2) return "Exarchate";
                    if (tier > 2) return "Patriarchate";
                }
                if (rng.Next() < 0.9 && (baseCulture == 21 || baseCulture == 16)) return "Imamah";
                if (tier > 2 && rng.Next() < 0.8 && (baseCulture == 18 || baseCulture == 17 || baseCulture == 28)) return "Caliphate";

                var theocracy = new Dictionary<string, int> { { "Theocracy", 20 }, { "Brotherhood", 1 }, { "Thearchy", 2 }, { "See", 1 }, { "Holy State", 1 } };
                return rng.Rw(theocracy);
            }

            return s.Form.ToString();
        }

        public static string GetFullName(MapState state, IRandom rng)
        {
            if (string.IsNullOrEmpty(state.FormName)) return state.Name;
            if (string.IsNullOrEmpty(state.Name) && !string.IsNullOrEmpty(state.FormName)) return "The " + state.FormName;

            bool adjName = AdjForms.Contains(state.FormName) && !state.Name.Contains("-") && !state.Name.Contains(" ");

            return adjName
                ? $"{LanguageUtils.GetAdjective(state.Name, rng)} {state.FormName}"
                : $"{state.FormName} of {state.Name}";
        }

        #endregion

        #region Statistics

        public static void CollectStatistics(MapPack pack)
        {
            var cells = pack.Cells;
            var stateDict = pack.States.ToDictionary(s => s.Id);

            foreach (var s in pack.States)
            {
                s.Area = 0;
                s.Population = 0;
                s.BurgsCount = 0;
            }

            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i].Height < 20) continue;
                int sId = cells[i].StateId;
                if (sId == 0 || !stateDict.ContainsKey(sId)) continue;

                var state = stateDict[sId];
                state.Area += cells[i].Area;
                state.Population += (int)cells[i].Population; // Rural population

                if (cells[i].BurgId > 0)
                {
                    state.BurgsCount++;
                    // state.Population += pack.Burgs[cells[i].BurgId - 1].Population; // Add Urban
                }
            }
        }

        #endregion

        #region Campains

        public static void GenerateCampaigns(MapPack pack)
        {
            var rng = pack.Rng;
            int currentYear = pack.Options.Year;

            foreach (var state in pack.States)
            {
                if (state.Id == 0) continue;

                var neighbors = state.Neighbors.Count > 0 ? state.Neighbors : new List<int> { 0 };
                var campaigns = new List<MapCampaign>();

                foreach (int neibId in neighbors)
                {
                    // 1. Base Name
                    bool useNeighborName = neibId > 0 && rng.P(0.8);
                    string baseName = useNeighborName
                        ? pack.States[neibId].Name
                        : NameModule.GetCultureShort(rng, pack.Cultures[state.CultureId].BaseNameId);

                    // 2. Start (JS Gauss evaluates first)
                    int start = (int)rng.Gauss(currentYear - 100, 150, 1, currentYear - 6);

                    // 3. End (JS Gauss evaluates second)
                    int end = start + (int)rng.Gauss(4, 5, 1, currentYear - start - 1);

                    // 4. Adjective and WarType (JS evaluates these inside the return statement)
                    string adj = LanguageUtils.GetAdjective(baseName, rng);
                    string warType = rng.Rw(WarsDict);
                    string campaignName = $"{adj} {warType}";

                    campaigns.Add(new MapCampaign
                    {
                        Name = campaignName,
                        Start = start,
                        End = end
                    });
                }

                // Sort campaigns chronologically so the history tab reads correctly
                state.Campaigns = campaigns.OrderBy(c => c.Start).ToList();
            }
        }

        #endregion
    }
}