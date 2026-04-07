using D3Sharp.QuadTree;
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

        #region Generate

        public static void Generate(MapPack pack, MapData grid, int count)
        {
            // 1. Initial Setup
            var cells = pack.Cells;
            ushort[] cultureIds = new ushort[cells.Length]; // Equivalent to Uint16Array

            // In JS: const populated = cells.i.filter(i => cells.s[i]);
            var populated = cells.Where(c => c.Suitability > 0).Select(c => c.Index).ToList();

            // 2. Population Safety Check
            if (populated.Count < count * 25)
            {
                count = Math.Max(1, populated.Count / 50);
                if (populated.Count < 25) // Extreme climate case
                {
                    pack.Cultures = new List<MapCulture> 
                    {
                        new MapCulture { Name = "Wildlands", Id = 0, BaseNameId = 1, Shield = "round" }
                    };
                    // In a real app, you'd trigger a Warning/Exception here instead of an Alert Dialog
                    return;
                }
            }
            var x = grid.Rng.Next();

            // 3. Selection & Preparation
            var selectedTemplates = selectCultures(count);


            var finalCultures = new List<MapCulture>();
            var codes = new List<string>();

            // JS: const centers = d3.quadtree();
            // Create an empty quadtree to track centers as we place them
            var quadDatas = new List<QuadPoint>();
            var tree = new QuadTree<QuadPoint, QuadPointNode>(quadDatas);
            var colors = ColorUtil.GetColors(count, grid.Rng);

            // 4. Main Generation Loop
            for (int i = 0; i < selectedTemplates.Count; i++)
            {
                var template = selectedTemplates[i];
                int newId = i + 1;

                // JS: const sortingFn = c.sort ? c.sort : i => cells.s[i];
                var sortingFn = template.SortingFn ?? ((int idx) => (double)cells[idx].Suitability);

                // Pass the actual tree reference so it can be searched
                int center = placeCenter(sortingFn, tree);

                var type = defineCultureType(center);
                var expansionism = defineCultureExpansionism(type);

                var code = LanguageUtils.Abbreviate(template.Name, codes);
                codes.Add(code);

                var mc = new MapCulture
                {
                    Id = newId,
                    Code = code,
                    Name = template.Name,
                    BaseNameId = template.BaseNameId,
                    CenterCell = center,
                    Shield = template.Shield,
                    Color = "getColors(i)", // Placeholder
                    Type = type,
                    Expansionism = expansionism,
                    // Expansionism, Code, etc will be added in the next step
                };
                
                finalCultures.Add(mc);
                cultureIds[center] = (ushort)newId;
            }

            // 5. Finalize Wildlands (JS: cultures.unshift)
            finalCultures.Insert(0, new MapCulture
            {
                Name = "Wildlands",
                Id = 0,
                BaseNameId = 1,
                Shield = "round"
            });

            pack.Cultures = finalCultures;
            // pack.Cells.Culture = cultureIds; (Update the cell-to-culture map)

            // --- LOCAL FUNCTIONS (Placeholders) ---

            List<CultureTemplate> selectCultures(int culturesNumber)
            {
                // Get the full list of available templates for this culture set
                List<CultureTemplate> defaultCultures = GetDefault(pack, grid, culturesNumber);
                List<CultureTemplate> selected = new List<CultureTemplate>();

                // 1. Logic for locked/pre-existing cultures (Skipped for fresh generate)

                // 2. If we need exactly as many as are in the default set, just return them
                if (culturesNumber == defaultCultures.Count) return defaultCultures;

                // 3. Selection Loop
                while (selected.Count < culturesNumber && defaultCultures.Count > 0)
                {
                    int attempt = 0;
                    CultureTemplate? picked = null;
                    int rndIdx = 0;

                    do
                    {
                        rndIdx = grid.Rng.Next(0, defaultCultures.Count - 1);
                        picked = defaultCultures[rndIdx];
                        attempt++;

                        // JS: while (i < 200 && !P(culture.odd));
                    } while (attempt < MapConstants.CULTURE_SELECT_MAX_ATTEMPTS && !grid.Rng.P(picked.Odd));

                    selected.Add(picked);
                    defaultCultures.RemoveAt(rndIdx);
                }

                return selected;
            }

            int placeCenter(Func<int, double> sortingFn, QuadTree<QuadPoint, QuadPointNode> tree)
            {

                double spacing = (pack.Width + pack.Height) / 2.0 / count;
                const int MAX_ATTEMPTS = 100;

                // Use a stable sort to match JS
                var sorted = populated
                    .OrderByDescending(id => sortingFn(id))
                    .ThenBy(id => id) // This is the crucial tie-breaker for 100% parity
                    .ToList();

                int max = (int)Math.Floor(sorted.Count / 2.0);

                int cellId = 0;
                for (int i = 0; i < MAX_ATTEMPTS; i++)
                {

                    int biasedIndex = grid.Rng.Biased(0, max, 5);
                    cellId = sorted[Math.Clamp(biasedIndex, 0, sorted.Count - 1)];

                    spacing *= 0.9;

                    var pos = cells[cellId].Point;

                    // JS: !centers.find(x, y, spacing)
                    // Find if ANY point exists within 'spacing'
                    var existingCenterInRange = tree.Find(pos.X, pos.Y, spacing);

                    // If no center is found within radius, and cell isn't already a culture center
                    if (existingCenterInRange == null && cultureIds[cellId] == 0)
                    {
                        break;
                    }
                }

                return cellId;
            }

            CultureType defineCultureType(int i)
            {
                var cell = pack.Cells[i];

                // 1. Nomadic: low height (<70) and specific biomes (1, 2, 4)
                int[] nomadicBiomes = { 1, 2, 4 };
                if (cell.Height < 70 && nomadicBiomes.Contains(cell.BiomeId))
                    return CultureType.Nomadic;

                // 2. Highland: high elevation (>50)
                if (cell.Height > 50)
                    return CultureType.Highland;

                // 3. Lake: Check the feature at the cell's 'haven' (nearest water)
                var havenCell = pack.Cells[cell.Haven];
                var havenFeature = pack.GetFeature(havenCell.FeatureId);

                if (havenFeature.Type == FeatureType.Lake && havenFeature.CellsCount > 5)
                    return CultureType.Lake;

                // 4. Naval: Coastal logic with probability checks
                // JS: pack.features[cells.f[i]] is the feature the cell itself belongs to
                var cellFeature = pack.GetFeature(cell.FeatureId);

                bool isNaval = (cell.Harbor > 0 && havenFeature.Type != FeatureType.Lake && grid.Rng.P(0.1)) ||
                               (cell.Harbor > 0 && grid.Rng.P(0.6)) ||
                               (cellFeature.Group == FeatureGroup.Island && grid.Rng.P(0.4));

                if (isNaval) return CultureType.Naval;

                // 5. River: Has a river ID and flux > 100
                if (cell.RiverId != 0 && cell.Flux > 100)
                    return CultureType.River;

                // 6. Hunting: Temperature > 2 and specific biomes
                int[] huntingBiomes = { 3, 7, 8, 9, 10, 12 };
                // Assuming temperature comes from the grid cell associated with the pack cell
                double temp = grid.Cells[cell.GridId].Temp;

                if (temp > 2 && huntingBiomes.Contains(cell.BiomeId))
                    return CultureType.Hunting;

                // 7. Default
                return CultureType.Generic;
            }

            double defineCultureExpansionism(CultureType type)
            {
                double baseExpansion = type switch
                {
                    CultureType.Lake => 0.8,
                    CultureType.Naval => 1.5,
                    CultureType.River => 0.9,
                    CultureType.Nomadic => 1.5,
                    CultureType.Hunting => 0.7,
                    CultureType.Highland => 1.2,
                    _ => 1.0 // Generic
                };

                // JS: rn(((Math.random() * sizeVariety) / 2 + 1) * base, 1)
                // rng.NextDouble() provides the Math.random() parity
                double randomFactor = (grid.Rng.Next() * MapConstants.CULTUE_EXPANSIONISM_SIZE_VARIETY / 2) + 1;
                double result = randomFactor * baseExpansion;

                // Rounding to 1 decimal place to match the JS 'rn(val, 1)'
                return NumberUtils.Round(result, 1);
            }

            
        }

        #endregion

        #region Default Cultures

        public static List<CultureTemplate> GetDefault(MapPack pack, MapData grid, int count)
        {
            var cultureSet = pack.Options.CultureSet;

            var cells = pack.Cells;
            double sMax = cells.Max(c => (double)c.Suitability);
            if (sMax <= 0) sMax = 1; // Prevent division by zero
                                     // 1. Force (double) cast to prevent integer division truncating to 0
            double N(int i) => Math.Ceiling((pack.Cells[i].Suitability / sMax) * 3);

            // 2. Exact truthy check parity
            double TD(int i, double goal)
            {
                double tempValue = grid.Cells[pack.Cells[i].GridId].Temp;
                double d = Math.Abs(tempValue - goal);

                // JS 'd ? d + 1 : 1' means if d is exactly 0.0, return 1, else d + 1
                // We use a small epsilon for safety with floating point 0
                return (d != 0) ? d + 1 : 1;
            }

            // 3. Biome logic is fine, but ensure types match
            double BD(int i, int[] biomes, double fee = 4)
                => biomes.Contains(pack.Cells[i].BiomeId) ? 1.0 : fee;

            // 4. Haven check - Note the Grid Index lookup
            double SF(int i, double fee = 4)
            {
                int havenIdx = pack.Cells[i].Haven;
                if (havenIdx == 0) return fee;

                // JS: pack.features[cells.f[cells.haven[cell]]]
                // Make sure we are looking up the feature of the HAVEN cell
                var feature = pack.GetFeature(pack.Cells[havenIdx].FeatureId);
                return feature.Type != FeatureType.Lake ? 1.0 : fee;
            }

            return cultureSet switch
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
                    new CultureTemplate { Name = NameModule.GetBase(grid.Rng, 1, 5, 9, ""), BaseNameId = 1, Odd = 1.0, SortingFn = null, Shield = "heater" },
                    new CultureTemplate { Name = NameModule.GetBase(grid.Rng, 1, 5, 9, ""), BaseNameId = 1, Odd = 1.0, SortingFn = null, Shield = "wedged" },
                    new CultureTemplate { Name = NameModule.GetBase(grid.Rng, 1, 5, 9, ""), BaseNameId = 1, Odd = 1.0, SortingFn = null, Shield = "swiss" },
                    new CultureTemplate { Name = NameModule.GetBase(grid.Rng, 1, 5, 9, ""), BaseNameId = 1, Odd = 1.0, SortingFn = null, Shield = "oldFrench" },
                    new CultureTemplate { Name = NameModule.GetBase(grid.Rng, 1, 5, 9, ""), BaseNameId = 1, Odd = 1.0, SortingFn = null, Shield = "swiss" },
                    new CultureTemplate { Name = NameModule.GetBase(grid.Rng, 1, 5, 9, ""), BaseNameId = 1, Odd = 1.0, SortingFn = null, Shield = "spanish" },
                    new CultureTemplate { Name = NameModule.GetBase(grid.Rng, 1, 5, 9, ""), BaseNameId = 1, Odd = 1.0, SortingFn = null, Shield = "hessen" },
                    new CultureTemplate { Name = NameModule.GetBase(grid.Rng, 1, 5, 9, ""), BaseNameId = 1, Odd = 1.0, SortingFn = null, Shield = "fantasy5" },
                    new CultureTemplate { Name = NameModule.GetBase(grid.Rng, 1, 5, 9, ""), BaseNameId = 1, Odd = 1.0, SortingFn = null, Shield = "fantasy4" },
                    new CultureTemplate { Name = NameModule.GetBase(grid.Rng, 1, 5, 9, ""), BaseNameId = 1, Odd = 1.0, SortingFn = null, Shield = "fantasy1" }
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
                    int rndId = NameModule.GetRandomBaseId(grid.Rng);

                    return new CultureTemplate
                    {
                        BaseNameId = rndId,
                        Name = NameModule.GetBaseShort(grid.Rng, rndId),
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
