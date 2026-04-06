using MapGen.Core.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MapGen.Core.Modules
{
    public class CultureTemplate
    {
        public string Name { get; set; } = string.Empty;
        public int BaseNameId { get; set; }
        public double Odd { get; set; } = 1.0;
        public string Shield { get; set; } = "round";
        // Delegate now takes both pack and grid for temperature/climate sorting logic
        public Func<int, double>? SortingFn { get; set; }
    }

    public static class CultureModule
    {
        #region Default Cultures

        public static List<CultureTemplate> GetDefault(Culture set, MapPack pack, MapData grid, int count)
        {
            var cells = pack.Cells;
            double sMax = cells.Max(c => (double)c.Suitability);
            if (sMax <= 0) sMax = 1; // Prevent division by zero

            // --- JS Helper Parity Methods ---
            // n: normalized cell score
            double N(int i) => Math.Ceiling((pack.Cells[i].Suitability / sMax) * 3);

            // td: temperature difference fee
            double TD(int i, double goal)
            {
                // Using grid.Cells to get the temperature from the underlying grid
                double d = Math.Abs(grid.Cells[pack.Cells[i].GridId].Temp - goal);
                return d > 0 ? d + 1 : 1;
            }

            // bd: biome difference fee
            double BD(int i, int[] biomes, double fee = 4)
                => biomes.Contains(pack.Cells[i].BiomeId) ? 1 : fee;

            // sf: sea/coast fee (not on sea coast fee)
            double SF(int i, double fee = 4)
            {
                int havenIdx = pack.Cells[i].Haven;
                if (havenIdx == 0) return fee;
                var feature = pack.GetFeature(pack.Cells[havenIdx].FeatureId);
                return feature.Type != FeatureType.Lake ? 1 : fee;
            }

            return set switch
            {
                Culture.European => new List<CultureTemplate>
                {
                    new CultureTemplate { Name = "Shwazen", BaseNameId = 0, Odd = 1.0, SortingFn = i => N(i) / TD(i, 10) / BD(i, new[] { 6, 8 }), Shield = "swiss" },
                    new CultureTemplate { Name = "Angshire", BaseNameId = 1, Odd = 1.0, SortingFn = i => N(i) / TD(i, 10) / SF(i), Shield = "wedged" },
                    new CultureTemplate { Name = "Luari", BaseNameId = 2, Odd = 1.0, SortingFn = i => N(i) / TD(i, 12) / BD(i, new[] { 6, 8 }), Shield = "french" },
                    new CultureTemplate { Name = "Tallian", BaseNameId = 3, Odd = 1.0, SortingFn = i => N(i) / TD(i, 15), Shield = "horsehead" },
                    new CultureTemplate { Name = "Astellian", BaseNameId = 4, Odd = 1.0, SortingFn = i => N(i) / TD(i, 16), Shield = "spanish" },
                    new CultureTemplate { Name = "Slovan", BaseNameId = 5, Odd = 1.0, SortingFn = i => (N(i) / TD(i, 6)) * pack.Cells[i].Distance, Shield = "polish" },
                    new CultureTemplate { Name = "Norse", BaseNameId = 6, Odd = 1.0, SortingFn = i => N(i) / TD(i, 5), Shield = "heater" },
                    new CultureTemplate { Name = "Elladan", BaseNameId = 7, Odd = 1.0, SortingFn = i => (N(i) / TD(i, 18)) * pack.Cells[i].Height, Shield = "boeotian" },
                    new CultureTemplate { Name = "Romian", BaseNameId = 8, Odd = 0.2, SortingFn = i => N(i) / TD(i, 15) / pack.Cells[i].Distance, Shield = "roman" },
                    new CultureTemplate { Name = "Soumi", BaseNameId = 9, Odd = 1.0, SortingFn = i => (N(i) / TD(i, 5) / BD(i, new[] { 9 })) * pack.Cells[i].Distance, Shield = "pavise" },
                    new CultureTemplate { Name = "Portuzian", BaseNameId = 13, Odd = 1.0, SortingFn = i => N(i) / TD(i, 17) / SF(i), Shield = "renaissance" },
                    new CultureTemplate { Name = "Vengrian", BaseNameId = 15, Odd = 1.0, SortingFn = i => (N(i) / TD(i, 11) / BD(i, new[] { 4 })) * pack.Cells[i].Distance, Shield = "horsehead2" },
                    new CultureTemplate { Name = "Turchian", BaseNameId = 16, Odd = 0.05, SortingFn = i => N(i) / TD(i, 14), Shield = "round" },
                    new CultureTemplate { Name = "Euskati", BaseNameId = 20, Odd = 0.05, SortingFn = i => (N(i) / TD(i, 15)) * pack.Cells[i].Height, Shield = "oldFrench" },
                    new CultureTemplate { Name = "Keltan", BaseNameId = 22, Odd = 0.05, SortingFn = i => (N(i) / TD(i, 11) / BD(i, new[] { 6, 8 })) * pack.Cells[i].Distance, Shield = "oval" }
                },

                Culture.Oriental => new List<CultureTemplate>
                {
                    new CultureTemplate { Name = "Koryo", BaseNameId = 10, Odd = 1.0, SortingFn = i => N(i) / TD(i, 12) / pack.Cells[i].Distance, Shield = "round" },
                    new CultureTemplate { Name = "Hantzu", BaseNameId = 11, Odd = 1.0, SortingFn = i => N(i) / TD(i, 13), Shield = "banner" },
                    new CultureTemplate { Name = "Yamoto", BaseNameId = 12, Odd = 1.0, SortingFn = i => N(i) / TD(i, 15) / pack.Cells[i].Distance, Shield = "round" },
                    new CultureTemplate { Name = "Turchian", BaseNameId = 16, Odd = 1.0, SortingFn = i => N(i) / TD(i, 12), Shield = "round" },
                    new CultureTemplate { Name = "Berberan", BaseNameId = 17, Odd = 0.2, SortingFn = i => (N(i) / TD(i, 19) / BD(i, new[] { 1, 2, 3 }, 7)) * pack.Cells[i].Distance, Shield = "oval" },
                    new CultureTemplate { Name = "Eurabic", BaseNameId = 18, Odd = 1.0, SortingFn = i => (N(i) / TD(i, 26) / BD(i, new[] { 1, 2 }, 7)) * pack.Cells[i].Distance, Shield = "oval" },
                    new CultureTemplate { Name = "Efratic", BaseNameId = 23, Odd = 0.1, SortingFn = i => (N(i) / TD(i, 22)) * pack.Cells[i].Distance, Shield = "round" },
                    new CultureTemplate { Name = "Tehrani", BaseNameId = 24, Odd = 1.0, SortingFn = i => (N(i) / TD(i, 18)) * pack.Cells[i].Height, Shield = "round" },
                    new CultureTemplate { Name = "Maui", BaseNameId = 25, Odd = 0.2, SortingFn = i => N(i) / TD(i, 24) / SF(i) / pack.Cells[i].Distance, Shield = "vesicaPiscis" },
                    new CultureTemplate { Name = "Carnatic", BaseNameId = 26, Odd = 0.5, SortingFn = i => N(i) / TD(i, 26), Shield = "round" },
                    new CultureTemplate { Name = "Vietic", BaseNameId = 29, Odd = 0.8, SortingFn = i => N(i) / TD(i, 25) / BD(i, new[] { 7 }, 7) / pack.Cells[i].Distance, Shield = "banner" },
                    new CultureTemplate { Name = "Guantzu", BaseNameId = 30, Odd = 0.5, SortingFn = i => N(i) / TD(i, 17), Shield = "banner" },
                    new CultureTemplate { Name = "Ulus", BaseNameId = 31, Odd = 1.0, SortingFn = i => (N(i) / TD(i, 5) / BD(i, new[] { 2, 4, 10 }, 7)) * pack.Cells[i].Distance, Shield = "banner" }
                },

                Culture.English => new List<CultureTemplate>
                {
                    new CultureTemplate { Name = NameModule.GetBase(1, 5, 9, "", ""), BaseNameId = 1, Odd = 1.0, SortingFn = null, Shield = "heater" },
                    new CultureTemplate { Name = NameModule.GetBase(1, 5, 9, "", ""), BaseNameId = 1, Odd = 1.0, SortingFn = null, Shield = "wedged" },
                    new CultureTemplate { Name = NameModule.GetBase(1, 5, 9, "", ""), BaseNameId = 1, Odd = 1.0, SortingFn = null, Shield = "swiss" },
                    new CultureTemplate { Name = NameModule.GetBase(1, 5, 9, "", ""), BaseNameId = 1, Odd = 1.0, SortingFn = null, Shield = "oldFrench" },
                    new CultureTemplate { Name = NameModule.GetBase(1, 5, 9, "", ""), BaseNameId = 1, Odd = 1.0, SortingFn = null, Shield = "swiss" },
                    new CultureTemplate { Name = NameModule.GetBase(1, 5, 9, "", ""), BaseNameId = 1, Odd = 1.0, SortingFn = null, Shield = "spanish" },
                    new CultureTemplate { Name = NameModule.GetBase(1, 5, 9, "", ""), BaseNameId = 1, Odd = 1.0, SortingFn = null, Shield = "hessen" },
                    new CultureTemplate { Name = NameModule.GetBase(1, 5, 9, "", ""), BaseNameId = 1, Odd = 1.0, SortingFn = null, Shield = "fantasy5" },
                    new CultureTemplate { Name = NameModule.GetBase(1, 5, 9, "", ""), BaseNameId = 1, Odd = 1.0, SortingFn = null, Shield = "fantasy4" },
                    new CultureTemplate { Name = NameModule.GetBase(1, 5, 9, "", ""), BaseNameId = 1, Odd = 1.0, SortingFn = null, Shield = "fantasy1" }
                },

                Culture.Antique => new List<CultureTemplate>
                {
                    new CultureTemplate { Name = "Roman", BaseNameId = 8, Odd = 1.0, SortingFn = i => N(i) / TD(i, 14) / pack.Cells[i].Distance, Shield = "roman" },
                    new CultureTemplate { Name = "Roman", BaseNameId = 8, Odd = 1.0, SortingFn = i => N(i) / TD(i, 15) / SF(i), Shield = "roman" },
                    new CultureTemplate { Name = "Roman", BaseNameId = 8, Odd = 1.0, SortingFn = i => N(i) / TD(i, 16) / SF(i), Shield = "roman" },
                    new CultureTemplate { Name = "Roman", BaseNameId = 8, Odd = 1.0, SortingFn = i => N(i) / TD(i, 17) / pack.Cells[i].Distance, Shield = "roman" },
                    new CultureTemplate { Name = "Hellenic", BaseNameId = 7, Odd = 1.0, SortingFn = i => (N(i) / TD(i, 18) / SF(i)) * pack.Cells[i].Height, Shield = "boeotian" },
                    new CultureTemplate { Name = "Hellenic", BaseNameId = 7, Odd = 1.0, SortingFn = i => (N(i) / TD(i, 19) / SF(i)) * pack.Cells[i].Height, Shield = "boeotian" },
                    new CultureTemplate { Name = "Macedonian", BaseNameId = 7, Odd = 0.5, SortingFn = i => (N(i) / TD(i, 12)) * pack.Cells[i].Height, Shield = "round" },
                    new CultureTemplate { Name = "Celtic", BaseNameId = 22, Odd = 1.0, SortingFn = i => N(i) / Math.Pow(TD(i, 11), 0.5) / BD(i, new[] { 6, 8 }), Shield = "round" },
                    new CultureTemplate { Name = "Germanic", BaseNameId = 0, Odd = 1.0, SortingFn = i => N(i) / Math.Pow(TD(i, 10), 0.5) / BD(i, new[] { 6, 8 }), Shield = "round" },
                    new CultureTemplate { Name = "Persian", BaseNameId = 24, Odd = 0.8, SortingFn = i => (N(i) / TD(i, 18)) * pack.Cells[i].Height, Shield = "oval" },
                    new CultureTemplate { Name = "Scythian", BaseNameId = 24, Odd = 0.5, SortingFn = i => N(i) / Math.Pow(TD(i, 11), 0.5) / BD(i, new[] { 4 }), Shield = "round" },
                    new CultureTemplate { Name = "Cantabrian", BaseNameId = 20, Odd = 0.5, SortingFn = i => (N(i) / TD(i, 16)) * pack.Cells[i].Height, Shield = "oval" },
                    new CultureTemplate { Name = "Estian", BaseNameId = 9, Odd = 0.2, SortingFn = i => (N(i) / TD(i, 5)) * pack.Cells[i].Distance, Shield = "pavise" },
                    new CultureTemplate { Name = "Carthaginian", BaseNameId = 42, Odd = 0.3, SortingFn = i => N(i) / TD(i, 20) / SF(i), Shield = "oval" },
                    new CultureTemplate { Name = "Hebrew", BaseNameId = 42, Odd = 0.2, SortingFn = i => (N(i) / TD(i, 19)) * SF(i), Shield = "oval" },
                    new CultureTemplate { Name = "Mesopotamian", BaseNameId = 23, Odd = 0.2, SortingFn = i => N(i) / TD(i, 22) / BD(i, new[] { 1, 2, 3 }), Shield = "oval" }
                },

                Culture.HighFantasy => new List<CultureTemplate>
                {
                    // fantasy races
                    new CultureTemplate { Name = "Quenian (Elfish)", BaseNameId = 33, Odd = 1.0, SortingFn = i => (N(i) / BD(i, new[] { 6, 7, 8, 9 }, 10)) * pack.Cells[i].Distance, Shield = "gondor" },
                    new CultureTemplate { Name = "Eldar (Elfish)", BaseNameId = 33, Odd = 1.0, SortingFn = i => (N(i) / BD(i, new[] { 6, 7, 8, 9 }, 10)) * pack.Cells[i].Distance, Shield = "noldor" },
                    new CultureTemplate { Name = "Trow (Dark Elfish)", BaseNameId = 34, Odd = 0.9, SortingFn = i => (N(i) / BD(i, new[] { 7, 8, 9, 12 }, 10)) * pack.Cells[i].Distance, Shield = "hessen" },
                    new CultureTemplate { Name = "Lothian (Dark Elfish)", BaseNameId = 34, Odd = 0.3, SortingFn = i => (N(i) / BD(i, new[] { 7, 8, 9, 12 }, 10)) * pack.Cells[i].Distance, Shield = "wedged" },
                    new CultureTemplate { Name = "Dunirr (Dwarven)", BaseNameId = 35, Odd = 1.0, SortingFn = i => N(i) + pack.Cells[i].Height, Shield = "ironHills" },
                    new CultureTemplate { Name = "Khazadur (Dwarven)", BaseNameId = 35, Odd = 1.0, SortingFn = i => N(i) + pack.Cells[i].Height, Shield = "erebor" },
                    new CultureTemplate { Name = "Kobold (Goblin)", BaseNameId = 36, Odd = 1.0, SortingFn = i => pack.Cells[i].Distance - pack.Cells[i].Suitability, Shield = "moriaOrc" },
                    new CultureTemplate { Name = "Uruk (Orkish)", BaseNameId = 37, Odd = 1.0, SortingFn = i => pack.Cells[i].Height * pack.Cells[i].Distance, Shield = "urukHai" },
                    new CultureTemplate { Name = "Ugluk (Orkish)", BaseNameId = 37, Odd = 0.5, SortingFn = i => (pack.Cells[i].Height * pack.Cells[i].Distance) / BD(i, new[] { 1, 2, 10, 11 }), Shield = "moriaOrc" },
                    new CultureTemplate { Name = "Yotunn (Giants)", BaseNameId = 38, Odd = 0.7, SortingFn = i => TD(i, -10), Shield = "pavise" },
                    new CultureTemplate { Name = "Rake (Drakonic)", BaseNameId = 39, Odd = 0.7, SortingFn = i => -pack.Cells[i].Suitability, Shield = "fantasy2" },
                    new CultureTemplate { Name = "Arago (Arachnid)", BaseNameId = 40, Odd = 0.7, SortingFn = i => pack.Cells[i].Distance - pack.Cells[i].Suitability, Shield = "horsehead2" },
                    new CultureTemplate { Name = "Aj'Snaga (Serpents)", BaseNameId = 41, Odd = 0.7, SortingFn = i => N(i) / BD(i, new[] { 12 }, 10), Shield = "fantasy1" },
                    // fantasy human
                    new CultureTemplate { Name = "Anor (Human)", BaseNameId = 32, Odd = 1.0, SortingFn = i => N(i) / TD(i, 10), Shield = "fantasy5" },
                    new CultureTemplate { Name = "Dail (Human)", BaseNameId = 32, Odd = 1.0, SortingFn = i => N(i) / TD(i, 13), Shield = "roman" },
                    new CultureTemplate { Name = "Rohand (Human)", BaseNameId = 16, Odd = 1.0, SortingFn = i => N(i) / TD(i, 16), Shield = "round" },
                    new CultureTemplate { Name = "Dulandir (Human)", BaseNameId = 31, Odd = 1.0, SortingFn = i => (N(i) / TD(i, 5) / BD(i, new[] { 2, 4, 10 }, 7)) * pack.Cells[i].Distance, Shield = "easterling" }
                },

                Culture.DarkFantasy => new List<CultureTemplate>
                {
                    // common real-world English
                    new CultureTemplate { Name = "Angshire", BaseNameId = 1, Odd = 1.0, SortingFn = i => N(i) / TD(i, 10) / SF(i), Shield = "heater" },
                    new CultureTemplate { Name = "Enlandic", BaseNameId = 1, Odd = 1.0, SortingFn = i => N(i) / TD(i, 12), Shield = "heater" },
                    new CultureTemplate { Name = "Westen", BaseNameId = 1, Odd = 1.0, SortingFn = i => N(i) / TD(i, 10), Shield = "heater" },
                    new CultureTemplate { Name = "Nortumbic", BaseNameId = 1, Odd = 1.0, SortingFn = i => N(i) / TD(i, 7), Shield = "heater" },
                    new CultureTemplate { Name = "Mercian", BaseNameId = 1, Odd = 1.0, SortingFn = i => N(i) / TD(i, 9), Shield = "heater" },
                    new CultureTemplate { Name = "Kentian", BaseNameId = 1, Odd = 1.0, SortingFn = i => N(i) / TD(i, 12), Shield = "heater" },
                    // rare real-world western
                    new CultureTemplate { Name = "Norse", BaseNameId = 6, Odd = 0.7, SortingFn = i => N(i) / TD(i, 5) / SF(i), Shield = "oldFrench" },
                    new CultureTemplate { Name = "Schwarzen", BaseNameId = 0, Odd = 0.3, SortingFn = i => N(i) / TD(i, 10) / BD(i, new[] { 6, 8 }), Shield = "gonfalon" },
                    new CultureTemplate { Name = "Luarian", BaseNameId = 2, Odd = 0.3, SortingFn = i => N(i) / TD(i, 12) / BD(i, new[] { 6, 8 }), Shield = "oldFrench" },
                    new CultureTemplate { Name = "Hetallian", BaseNameId = 3, Odd = 0.3, SortingFn = i => N(i) / TD(i, 15), Shield = "oval" },
                    new CultureTemplate { Name = "Astellian", BaseNameId = 4, Odd = 0.3, SortingFn = i => N(i) / TD(i, 16), Shield = "spanish" },
                    // rare real-world exotic
                    new CultureTemplate { Name = "Kiswaili", BaseNameId = 28, Odd = 0.05, SortingFn = i => N(i) / TD(i, 29) / BD(i, new[] { 1, 3, 5, 7 }), Shield = "vesicaPiscis" },
                    new CultureTemplate { Name = "Yoruba", BaseNameId = 21, Odd = 0.05, SortingFn = i => N(i) / TD(i, 15) / BD(i, new[] { 5, 7 }), Shield = "vesicaPiscis" },
                    new CultureTemplate { Name = "Koryo", BaseNameId = 10, Odd = 0.05, SortingFn = i => N(i) / TD(i, 12) / pack.Cells[i].Distance, Shield = "round" },
                    new CultureTemplate { Name = "Hantzu", BaseNameId = 11, Odd = 0.05, SortingFn = i => N(i) / TD(i, 13), Shield = "banner" },
                    new CultureTemplate { Name = "Yamoto", BaseNameId = 12, Odd = 0.05, SortingFn = i => N(i) / TD(i, 15) / pack.Cells[i].Distance, Shield = "round" },
                    new CultureTemplate { Name = "Guantzu", BaseNameId = 30, Odd = 0.05, SortingFn = i => N(i) / TD(i, 17), Shield = "banner" },
                    new CultureTemplate { Name = "Ulus", BaseNameId = 31, Odd = 0.05, SortingFn = i => (N(i) / TD(i, 5) / BD(i, new[] { 2, 4, 10 }, 7)) * pack.Cells[i].Distance, Shield = "banner" },
                    new CultureTemplate { Name = "Turan", BaseNameId = 16, Odd = 0.05, SortingFn = i => N(i) / TD(i, 12), Shield = "round" },
                    new CultureTemplate { Name = "Berberan", BaseNameId = 17, Odd = 0.05, SortingFn = i => (N(i) / TD(i, 19) / BD(i, new[] { 1, 2, 3 }, 7)) * pack.Cells[i].Distance, Shield = "round" },
                    new CultureTemplate { Name = "Eurabic", BaseNameId = 18, Odd = 0.05, SortingFn = i => (N(i) / TD(i, 26) / BD(i, new[] { 1, 2 }, 7)) * pack.Cells[i].Distance, Shield = "round" },
                    new CultureTemplate { Name = "Slovan", BaseNameId = 5, Odd = 0.05, SortingFn = i => (N(i) / TD(i, 6)) * pack.Cells[i].Distance, Shield = "round" },
                    new CultureTemplate { Name = "Keltan", BaseNameId = 22, Odd = 0.1, SortingFn = i => N(i) / Math.Pow(TD(i, 11), 0.5) / BD(i, new[] { 6, 8 }), Shield = "vesicaPiscis" },
                    new CultureTemplate { Name = "Elladan", BaseNameId = 7, Odd = 0.2, SortingFn = i => (N(i) / TD(i, 18) / SF(i)) * pack.Cells[i].Height, Shield = "boeotian" },
                    new CultureTemplate { Name = "Romian", BaseNameId = 8, Odd = 0.2, SortingFn = i => N(i) / TD(i, 14) / pack.Cells[i].Distance, Shield = "roman" },
                    // fantasy races
                    new CultureTemplate { Name = "Eldar", BaseNameId = 33, Odd = 0.5, SortingFn = i => (N(i) / BD(i, new[] { 6, 7, 8, 9 }, 10)) * pack.Cells[i].Distance, Shield = "fantasy5" }, // Elves
                    new CultureTemplate { Name = "Trow", BaseNameId = 34, Odd = 0.8, SortingFn = i => (N(i) / BD(i, new[] { 7, 8, 9, 12 }, 10)) * pack.Cells[i].Distance, Shield = "hessen" }, // Dark Elves
                    new CultureTemplate { Name = "Durinn", BaseNameId = 35, Odd = 0.8, SortingFn = i => N(i) + pack.Cells[i].Height, Shield = "erebor" }, // Dwarven
                    new CultureTemplate { Name = "Kobblin", BaseNameId = 36, Odd = 0.8, SortingFn = i => pack.Cells[i].Distance - pack.Cells[i].Suitability, Shield = "moriaOrc" }, // Goblin
                    new CultureTemplate { Name = "Uruk", BaseNameId = 37, Odd = 0.8, SortingFn = i => (pack.Cells[i].Height * pack.Cells[i].Distance) / BD(i, new[] { 1, 2, 10, 11 }), Shield = "urukHai" }, // Orc
                    new CultureTemplate { Name = "Yotunn", BaseNameId = 38, Odd = 0.8, SortingFn = i => TD(i, -10), Shield = "pavise" }, // Giant
                    new CultureTemplate { Name = "Drake", BaseNameId = 39, Odd = 0.9, SortingFn = i => -pack.Cells[i].Suitability, Shield = "fantasy2" }, // Draconic
                    new CultureTemplate { Name = "Rakhnid", BaseNameId = 40, Odd = 0.9, SortingFn = i => pack.Cells[i].Distance - pack.Cells[i].Suitability, Shield = "horsehead2" }, // Arachnid
                    new CultureTemplate { Name = "Aj'Snaga", BaseNameId = 41, Odd = 0.9, SortingFn = i => N(i) / BD(i, new[] { 12 }, 10), Shield = "fantasy1" } // Serpents
                },

                Culture.Random => Enumerable.Range(0, count).Select(_ =>
                {
                    // JS: rand(nameBases.length - 1)
                    // Using your extension: rng.Next(max) is inclusive 0 to max
                    int rnd = grid.Rng.Next(NameModule.NameBasesCount - 1);

                    return new CultureTemplate
                    {
                        Name = NameModule.GetBaseShort(rnd),
                        BaseNameId = rnd,
                        Odd = 1.0,
                        SortingFn = null, // Random cultures use default suitability sorting
                        Shield = GetRandomShield(grid.Rng)
                    };
                }).ToList(),

                _ => new List<CultureTemplate>
                {
                    new CultureTemplate { Name = "Shwazen", BaseNameId = 0, Odd = 0.7, SortingFn = i => N(i) / TD(i, 10) / BD(i, new[] { 6, 8 }), Shield = "hessen" },
                    new CultureTemplate { Name = "Angshire", BaseNameId = 1, Odd = 1.0, SortingFn = i => N(i) / TD(i, 10) / SF(i), Shield = "heater" },
                    new CultureTemplate { Name = "Luari", BaseNameId = 2, Odd = 0.6, SortingFn = i => N(i) / TD(i, 12) / BD(i, new[] { 6, 8 }), Shield = "oldFrench" },
                    new CultureTemplate { Name = "Tallian", BaseNameId = 3, Odd = 0.6, SortingFn = i => N(i) / TD(i, 15), Shield = "horsehead2" },
                    new CultureTemplate { Name = "Astellian", BaseNameId = 4, Odd = 0.6, SortingFn = i => N(i) / TD(i, 16), Shield = "spanish" },
                    new CultureTemplate { Name = "Slovan", BaseNameId = 5, Odd = 0.7, SortingFn = i => (N(i) / TD(i, 6)) * pack.Cells[i].Distance, Shield = "round" },
                    new CultureTemplate { Name = "Norse", BaseNameId = 6, Odd = 0.7, SortingFn = i => N(i) / TD(i, 5), Shield = "heater" },
                    new CultureTemplate { Name = "Elladan", BaseNameId = 7, Odd = 0.7, SortingFn = i => (N(i) / TD(i, 18)) * pack.Cells[i].Height, Shield = "boeotian" },
                    new CultureTemplate { Name = "Romian", BaseNameId = 8, Odd = 0.7, SortingFn = i => N(i) / TD(i, 15), Shield = "roman" },
                    new CultureTemplate { Name = "Soumi", BaseNameId = 9, Odd = 0.3, SortingFn = i => (N(i) / TD(i, 5) / BD(i, new[] { 9 })) * pack.Cells[i].Distance, Shield = "pavise" },
                    new CultureTemplate { Name = "Koryo", BaseNameId = 10, Odd = 0.1, SortingFn = i => N(i) / TD(i, 12) / pack.Cells[i].Distance, Shield = "round" },
                    new CultureTemplate { Name = "Hantzu", BaseNameId = 11, Odd = 0.1, SortingFn = i => N(i) / TD(i, 13), Shield = "banner" },
                    new CultureTemplate { Name = "Yamoto", BaseNameId = 12, Odd = 0.1, SortingFn = i => N(i) / TD(i, 15) / pack.Cells[i].Distance, Shield = "round" },
                    new CultureTemplate { Name = "Portuzian", BaseNameId = 13, Odd = 0.4, SortingFn = i => N(i) / TD(i, 17) / SF(i), Shield = "spanish" },
                    new CultureTemplate { Name = "Nawatli", BaseNameId = 14, Odd = 0.1, SortingFn = i => pack.Cells[i].Height / TD(i, 18) / BD(i, new[] { 7 }), Shield = "square" },
                    new CultureTemplate { Name = "Vengrian", BaseNameId = 15, Odd = 0.2, SortingFn = i => (N(i) / TD(i, 11) / BD(i, new[] { 4 })) * pack.Cells[i].Distance, Shield = "wedged" },
                    new CultureTemplate { Name = "Turchian", BaseNameId = 16, Odd = 0.2, SortingFn = i => N(i) / TD(i, 13), Shield = "round" },
                    new CultureTemplate { Name = "Berberan", BaseNameId = 17, Odd = 0.1, SortingFn = i => (N(i) / TD(i, 19) / BD(i, new[] { 1, 2, 3 }, 7)) * pack.Cells[i].Distance, Shield = "round" },
                    new CultureTemplate { Name = "Eurabic", BaseNameId = 18, Odd = 0.2, SortingFn = i => (N(i) / TD(i, 26) / BD(i, new[] { 1, 2 }, 7)) * pack.Cells[i].Distance, Shield = "round" },
                    new CultureTemplate { Name = "Inuk", BaseNameId = 19, Odd = 0.05, SortingFn = i => TD(i, -1) / BD(i, new[] { 10, 11 }) / SF(i), Shield = "square" },
                    new CultureTemplate { Name = "Euskati", BaseNameId = 20, Odd = 0.05, SortingFn = i => (N(i) / TD(i, 15)) * pack.Cells[i].Height, Shield = "spanish" },
                    new CultureTemplate { Name = "Yoruba", BaseNameId = 21, Odd = 0.05, SortingFn = i => N(i) / TD(i, 15) / BD(i, new[] { 5, 7 }), Shield = "vesicaPiscis" },
                    new CultureTemplate { Name = "Keltan", BaseNameId = 22, Odd = 0.05, SortingFn = i => (N(i) / TD(i, 11) / BD(i, new[] { 6, 8 })) * pack.Cells[i].Distance, Shield = "vesicaPiscis" },
                    new CultureTemplate { Name = "Efratic", BaseNameId = 23, Odd = 0.05, SortingFn = i => (N(i) / TD(i, 22)) * pack.Cells[i].Distance, Shield = "diamond" },
                    new CultureTemplate { Name = "Tehrani", BaseNameId = 24, Odd = 0.1, SortingFn = i => (N(i) / TD(i, 18)) * pack.Cells[i].Height, Shield = "round" },
                    new CultureTemplate { Name = "Maui", BaseNameId = 25, Odd = 0.05, SortingFn = i => N(i) / TD(i, 24) / SF(i) / pack.Cells[i].Distance, Shield = "round" },
                    new CultureTemplate { Name = "Carnatic", BaseNameId = 26, Odd = 0.05, SortingFn = i => N(i) / TD(i, 26), Shield = "round" },
                    new CultureTemplate { Name = "Inqan", BaseNameId = 27, Odd = 0.05, SortingFn = i => pack.Cells[i].Height / TD(i, 13), Shield = "square" },
                    new CultureTemplate { Name = "Kiswaili", BaseNameId = 28, Odd = 0.1, SortingFn = i => N(i) / TD(i, 29) / BD(i, new[] { 1, 3, 5, 7 }), Shield = "vesicaPiscis" },
                    new CultureTemplate { Name = "Vietic", BaseNameId = 29, Odd = 0.1, SortingFn = i => N(i) / TD(i, 25) / BD(i, new[] { 7 }, 7) / pack.Cells[i].Distance, Shield = "banner" },
                    new CultureTemplate { Name = "Guantzu", BaseNameId = 30, Odd = 0.1, SortingFn = i => N(i) / TD(i, 17), Shield = "banner" },
                    new CultureTemplate { Name = "Ulus", BaseNameId = 31, Odd = 0.1, SortingFn = i => (N(i) / TD(i, 5) / BD(i, new[] { 2, 4, 10 }, 7)) * pack.Cells[i].Distance, Shield = "banner" },
                    new CultureTemplate { Name = "Hebrew", BaseNameId = 42, Odd = 0.2, SortingFn = i => (N(i) / TD(i, 18)) * SF(i), Shield = "oval" }
                }
            };
        }

        private static string GetRandomShield(IRandom rng)
        {
            string[] shields = new[] {
                "round", "heater", "spanish", "french", "wedged", "swiss", "papal", "heart",
                "rhombus", "square", "circle", "oval", "vesicaPiscis", "shikula", "renaissance",
                "pavise", "lozenge", "flag", "banner", "slab", "bottleneck", "trapezoid",
                "horsehead", "horsehead2", "boeotian", "roman", "kite", "oldFrench", "swiss2",
                "swiss3", "swiss4", "fantasy1", "fantasy2", "fantasy3", "fantasy4", "fantasy5",
                "noldor", "gondor", "sinister", "wedged2", "ironHills", "erebor", "urukHai",
                "moriaOrc", "easterling", "militia", "viking"
            };
            return rng.Ra(shields);
        }

        #endregion
    }
}
