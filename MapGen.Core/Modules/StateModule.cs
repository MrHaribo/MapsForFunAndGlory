using System;
using System.Collections.Generic;

namespace MapGen.Core.Modules
{
    public static class StateModule
    {
        public static void Generate(MapPack pack)
        {
            pack.States = CreateStates(pack);

            // ExpandStates(pack);
            // Normalize(pack);
            // GetPoles(pack);
            // FindNeighbors(pack);
            // AssignColors(pack);
            // GenerateCampaigns(pack);
            // GenerateDiplomacy(pack);

            static List<MapState> CreateStates(MapPack pack)
            {
                var rng = pack.Rng;
                var burgs = pack.Burgs;
                var cultures = pack.Cultures;

                // State 0 is always "Neutrals"
                var states = new List<MapState>
                {
                    new MapState { Id = 0, Name = "Neutrals", Color = "#777777" }
                };

                // sizeVariety from UI (usually 1-10)
                double sizeVariety = MapConstants.STATE_SIZE_VARIETY;

                foreach (var burg in burgs)
                {
                    // In your new 0-padded logic, burg.Id is 1-based.
                    // We only create states for capitals.
                    if (!burg.IsCapital) continue;

                    // expansionism: rn(Math.random() * sizeVariety + 1, 1)
                    double expansionism = Math.Round(rng.Next() * sizeVariety + 1, 1);

                    // logic for basename: if name is short and cell meets 'each(5)' criteria
                    // JS 'each(5)' is effectively 'cell % 5 === 0'
                    bool isEach5th = (burg.Cell % 5 == 0);
                    string basename = (burg.Name.Length < 9 && isEach5th)
                        ? burg.Name
                        : NameModule.GetCultureShort(rng, cultures[burg.CultureId].BaseNameId);

                    string name = NameModule.GetStateName(rng, basename, cultures[burg.CultureId].BaseNameId);
                    CultureType type = cultures[burg.CultureId].Type;

                    // COA logic would go here (placeholder for now)
                    var coa = new MapCoA { Type = type };

                    var state = new MapState
                    {
                        Id = burg.Id, // State ID in the list
                        Name = name,
                        Expansionism = expansionism,
                        CapitalId = burg.Id,    // Reference to the Burg ID (1-based)
                        Type = type,
                        CenterCell = burg.Cell,
                        CultureId = burg.CultureId,
                        CoA = coa
                    };

                    // Link the burg back to this specific state
                    burg.StateId = state.Id;

                    states.Add(state);
                }

                return states;
            }
        }

    }
}