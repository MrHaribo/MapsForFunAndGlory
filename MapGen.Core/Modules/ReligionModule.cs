using MapGen.Core.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MapGen.Core.Modules
{
    public static class ReligionModule
    {
        #region Data Definition

        // --- 1. COMPACT BASE DATA ---
        private static readonly string[] BaseNumber = "One,Two,Three,Four,Five,Six,Seven,Eight,Nine,Ten,Eleven,Twelve".Split(',');
        private static readonly string[] BaseBeing = "Ancestor,Ancient,Avatar,Brother,Champion,Chief,Council,Creator,Deity,Divine One,Elder,Enlightened Being,Father,Forebear,Forefather,God,Goddess,Guardian,Immortal,Judge,Lord,Maker,Master,Mother,Omnipotent,Omnipresent,Prime,Protector,Ruler,Sister,Spirit,Supreme,Titan,Watcher".Split(',');
        private static readonly string[] BaseAdjective = "Absolute,Almighty,Bashing,Benevolent,Blind,Brave,Bright,Broken,Burning,Chosen,Cosmic,Crimson,Dark,Defeated,Divine,Earthly,Elevated,Eternal,Everlasting,Exalted,Fallen,Flaming,Forgiving,Golden,Great,Heavenly,Holy,Honorable,Illuminated,Immortal,Infallible,Infinite,Invisible,Iron,Last,Light,Living,Loving,Luminous,Magic,Merciful,Mighty,Night,Old,Omnipotent,Omniscient,Peaceful,Radiant,Sacred,Secret,Silent,Silver,Sleeping,Solitary,Sorrowful,Starry,Stone,Sun,Supreme,True,Ultimate,Underground,Universal,Unseen,Vengeful,Weeping,White,Wise,World".Split(',');
        private static readonly string[] BaseColor = "Amber,Black,Blue,Brown,Carmine,Cyan,Dark,Emerald,Gold,Gray,Green,Jade,Light,Olive,Orange,Pink,Purple,Red,Rose,Ruby,Sapphire,Silver,Violet,White,Yellow".Split(',');
        private static readonly string[] BaseAnimal = "Ape,Badger,Bear,Beaver,Bird,Boar,Bull,Cat,Condor,Cow,Coyote,Crane,Crow,Deer,Dog,Dragon,Eagle,Elephant,Elk,Falcon,Fox,Goat,Griffon,Hare,Hawk,Horse,Hound,Jackal,Jaguar,Leopard,Lion,Mantis,Monkey,Moose,Owl,Panther,Pegasus,Pelican,Puma,Rabbit,Rat,Raven,Rhino,Seagull,Serpent,Shark,Sheep,Snake,Spider,Stag,Tiger,Toad,Tortoise,Turtle,Whale,Wolf,Wolverine,Worm".Split(',');
        private static readonly string[] BaseGenitive = "Ancestors,Autumn,Blood,Bones,Chaos,Creation,Darkness,Death,Despair,Destruction,Dreams,Earth,Eternity,Fate,Fire,Fools,Forests,Glory,Gods,Gold,Harmony,Hatred,Heaven,Hell,Hope,Ice,Justice,Knowledge,Life,Light,Love,Lust,Magic,Men,Mercy,Minds,Miracles,Nature,Night,Order,Pain,Peace,Pestilence,Power,Rain,Retribution,Rivers,Seas,Shadows,Skies,Sorrow,Souls,Spring,Stars,Storms,Summer,Sun,Tears,Thunder,Time,Truth,Vengeance,War,Water,Wealth,Wind,Winter,Wisdom,World".Split(',');
        private static readonly string[] BaseTheocracy = "Brotherhood,Church,Coven,Cult,Faith,Order,Religion,Sect,Temple".Split(',');

        // --- 2. HIERARCHICAL DICTIONARIES ---
        private static readonly Dictionary<string, int> ApproachWeights = new Dictionary<string, int>
        {
            { "Number", 1 },
            { "Being", 3 },
            { "Adjective", 5 },
            { "Color + Animal", 5 },
            { "Adjective + Animal", 5 },
            { "Adjective + Being", 5 },
            { "Adjective + Genitive", 1 },
            { "Color + Being", 3 },
            { "Color + Genitive", 3 },
            { "Being + of + Genitive", 2 },
            { "Being + of the + Genitive", 1 },
            { "Animal + of + Genitive", 1 },
            { "Adjective + Being + of + Genitive", 2 },
            { "Adjective + Animal + of + Genitive", 2 }
        };

        private static readonly List<string> Approaches; // Populated in static constructor

        private static readonly Dictionary<ReligionGroup, Dictionary<string, int>> Forms = new Dictionary<ReligionGroup, Dictionary<string, int>>
        {
            { ReligionGroup.Folk, new Dictionary<string, int> {
                { "Shamanism", 4 }, { "Animism", 4 }, { "Polytheism", 4 },
                { "Ancestor Worship", 2 }, { "Nature Worship", 1 }, { "Totemism", 1 }
            }},
            { ReligionGroup.Organized, new Dictionary<string, int> {
                { "Polytheism", 7 }, { "Monotheism", 7 }, { "Dualism", 3 },
                { "Pantheism", 2 }, { "Non-theism", 2 }
            }},
            { ReligionGroup.Cult, new Dictionary<string, int> {
                { "Cult", 5 }, { "Dark Cult", 5 }, { "Sect", 1 }
            }},
            { ReligionGroup.Heresy, new Dictionary<string, int> {
                { "Heresy", 1 }
            }}
        };

        private static readonly Dictionary<ReligionGroup, Dictionary<string, int>> NamingMethods = new Dictionary<ReligionGroup, Dictionary<string, int>>
        {
            { ReligionGroup.Folk, new Dictionary<string, int> {
                { "Culture + type", 1 }
            }},
            { ReligionGroup.Organized, new Dictionary<string, int> {
                { "Random + type", 3 }, { "Random + ism", 1 }, { "Supreme + ism", 5 },
                { "Faith of + Supreme", 5 }, { "Place + ism", 1 }, { "Culture + ism", 2 },
                { "Place + ian + type", 6 }, { "Culture + type", 4 }
            }},
            { ReligionGroup.Cult, new Dictionary<string, int> {
                { "Burg + ian + type", 2 }, { "Random + ian + type", 1 }, { "Type + of the + meaning", 2 }
            }},
            { ReligionGroup.Heresy, new Dictionary<string, int> {
                { "Burg + ian + type", 3 }, { "Random + ism", 3 },
                { "Random + ian + type", 2 }, { "Type + of the + meaning", 1 }
            }}
        };

        private static readonly Dictionary<string, Dictionary<string, int>> Types = new Dictionary<string, Dictionary<string, int>>
        {
            { "Shamanism", new Dictionary<string, int> { { "Beliefs", 3 }, { "Shamanism", 2 }, { "Druidism", 1 }, { "Spirits", 1 } }},
            { "Animism", new Dictionary<string, int> { { "Spirits", 3 }, { "Beliefs", 1 } }},
            { "Polytheism", new Dictionary<string, int> { { "Deities", 3 }, { "Faith", 1 }, { "Gods", 1 }, { "Pantheon", 1 } }},
            { "Ancestor Worship", new Dictionary<string, int> { { "Beliefs", 1 }, { "Forefathers", 2 }, { "Ancestors", 2 } }},
            { "Nature Worship", new Dictionary<string, int> { { "Beliefs", 3 }, { "Druids", 1 } }},
            { "Totemism", new Dictionary<string, int> { { "Beliefs", 2 }, { "Totems", 2 }, { "Idols", 1 } }},

            { "Monotheism", new Dictionary<string, int> { { "Religion", 2 }, { "Church", 3 }, { "Faith", 1 } }},
            { "Dualism", new Dictionary<string, int> { { "Religion", 3 }, { "Faith", 1 }, { "Cult", 1 } }},
            { "Pantheism", new Dictionary<string, int> { { "Religion", 1 }, { "Faith", 1 } }},
            { "Non-theism", new Dictionary<string, int> { { "Beliefs", 3 }, { "Spirits", 1 } }},

            { "Cult", new Dictionary<string, int> { { "Cult", 4 }, { "Sect", 2 }, { "Arcanum", 1 }, { "Order", 1 }, { "Worship", 1 } }},
            { "Dark Cult", new Dictionary<string, int> { { "Cult", 2 }, { "Blasphemy", 1 }, { "Circle", 1 }, { "Coven", 1 }, { "Idols", 1 }, { "Occultism", 1 } }},
            { "Sect", new Dictionary<string, int> { { "Sect", 3 }, { "Society", 1 } }},

            { "Heresy", new Dictionary<string, int> { { "Heresy", 3 }, { "Sect", 2 }, { "Apostates", 1 }, { "Brotherhood", 1 }, { "Circle", 1 }, { "Dissent", 1 }, { "Dissenters", 1 }, { "Iconoclasm", 1 }, { "Schism", 1 }, { "Society", 1 } }}
        };

        private static readonly Dictionary<ReligionGroup, Func<IRandom, double>> ExpansionismMap = new Dictionary<ReligionGroup, Func<IRandom, double>>
        {
            { ReligionGroup.Folk, rng => 0.0 },
            { ReligionGroup.Organized, rng => rng.Gauss(5, 3, 0, 10, 1) },
            { ReligionGroup.Cult, rng => rng.Gauss(0.5, 0.5, 0, 5, 1) },
            { ReligionGroup.Heresy, rng => rng.Gauss(1, 0.5, 0, 5, 1) }
        };

        // Static constructor to perfectly mimic JS weighted array initialization
        static ReligionModule()
        {
            Approaches = new List<string>();
            foreach (var kvp in ApproachWeights)
            {
                for (int j = 0; j < kvp.Value; j++)
                {
                    Approaches.Add(kvp.Key);
                }
            }
        }

        #endregion

        #region Generation

        public static void Generate(MapPack pack)
        {
            var folkReligions = GenerateFolkReligions(pack);
            var organizedReligions = GenerateOrganizedReligions(pack, pack.Options.ReligionsCount);
            var combinedGenerated = folkReligions.Concat(organizedReligions).ToList();
            
            var namedReligions = SpecifyReligions(pack, combinedGenerated);
            var indexedReligions = CombineReligions(pack, namedReligions);
            var religionIds = ExpandReligions(pack, indexedReligions);
            var religions = DefineOrigins(pack, religionIds, indexedReligions);

            // JS: pack.religions = religions;
            pack.Religions = religions;

            // JS: pack.cells.religion = religionIds;
            for (int i = 0; i < pack.Cells.Length; i++)
            {
                pack.Cells[i].ReligionId = religionIds[i];
            }

            // JS: checkCenters();
            CheckCenters(pack);
        }

        private static List<MapReligion> GenerateFolkReligions(MapPack pack)
        {
            var folkReligions = new List<MapReligion>();

            // Filter for valid cultures (Id > 0)
            foreach (var culture in pack.Cultures.Where(c => c.Id > 0))
            {
                folkReligions.Add(new MapReligion
                {
                    Group = ReligionGroup.Folk,
                    Form = pack.Rng.Rw(Forms[ReligionGroup.Folk]), // Uses the Forms dictionary we set up earlier
                    CultureId = culture.Id,
                    CenterCell = culture.CenterCell
                });
            }

            return folkReligions;
        }

        private static List<MapReligion> GenerateOrganizedReligions(MapPack pack, int desiredReligionNumber)
        {
            // JS: const lockedReligionCount = lockedReligions.filter(({type}) => type !== "Folk").length || 0;
            // Since we ignore locks, this is 0.
            int lockedReligionCount = 0;
            int requiredReligionsNumber = desiredReligionNumber - lockedReligionCount;
            if (requiredReligionsNumber < 1) return new List<MapReligion>();

            var candidateCells = GetCandidateCells();
            var religionCores = PlaceReligions();

            // JS rand(min, max) is inclusive. C# Random.Next(min, max) is exclusive on the upper bound.
            int cultsCount = (int)Math.Floor((pack.Rng.Next(1, 5) / 10.0) * religionCores.Count);    // 10-40%
            int heresiesCount = (int)Math.Floor((pack.Rng.Next(0, 4) / 10.0) * religionCores.Count); // 0-30%
            int organizedCount = religionCores.Count - cultsCount - heresiesCount;

            // JS: const getType = index => { ... }
            ReligionGroup GetType(int index)
            {
                if (index < organizedCount) return ReligionGroup.Organized;
                if (index < organizedCount + cultsCount) return ReligionGroup.Cult;
                return ReligionGroup.Heresy;
            }

            var organizedReligions = new List<MapReligion>();

            // JS: return religionCores.map((cellId, index) => { ... })
            for (int i = 0; i < religionCores.Count; i++)
            {
                int cellId = religionCores[i];
                var type = GetType(i);
                var form = pack.Rng.Rw(Forms[type]);
                var cultureId = pack.Cells[cellId].CultureId;

                organizedReligions.Add(new MapReligion
                {
                    Group = type,
                    Form = form,
                    CultureId = cultureId,
                    CenterCell = cellId
                });
            }

            return organizedReligions;

            // ==============================================================================
            // LOCAL FUNCTIONS (Exact JS Parity)
            // ==============================================================================

            List<int> PlaceReligions()
            {
                var religionCells = new List<int>();

                // JS: const religionsTree = d3.quadtree();
                var religionsTree = new D3Sharp.QuadTree.QuadTree<QuadPoint, D3Sharp.QuadTree.QuadNode<QuadPoint>>();

                // Min distance between religion inceptions
                double spacing = (pack.Width + pack.Height) / 2.0 / desiredReligionNumber;

                foreach (int cellId in candidateCells)
                {
                    double x = pack.Cells[cellId].Point.X;
                    double y = pack.Cells[cellId].Point.Y;

                    // JS: if (religionsTree.find(x, y, spacing) === undefined)
                    if (religionsTree.Find(x, y, spacing) == null)
                    {
                        religionCells.Add(cellId);
                        religionsTree.Add(new QuadPoint { X = x, Y = y, DataIndex = cellId });

                        if (religionCells.Count == requiredReligionsNumber) return religionCells;
                    }
                }

                return religionCells;
            }

            List<int> GetCandidateCells()
            {
                var validBurgs = pack.Burgs.Where(b => b.Id > 0).ToList();

                if (validBurgs.Count >= requiredReligionsNumber)
                {
                    // JS NaN PARITY FIX: If MapBurg.Population defaults to 0 because burgs haven't been specified yet, 
                    // OrderByDescending performs a stable sort, perfectly keeping original insertion order just like JS!
                    return validBurgs
                        .OrderByDescending(b => b.Population)
                        .Select(b => b.CellId)
                        .ToList();
                }

                return pack.Cells
                    .Where(c => c.Suitability > 2)
                    .OrderByDescending(c => c.Suitability)
                    .Select(c => c.Index)
                    .ToList();
            }
        }

        #endregion

        #region Specify

        private static List<MapReligion> SpecifyReligions(MapPack pack, List<MapReligion> newReligions)
        {
            var cells = pack.Cells;
            var cultures = pack.Cultures;

            // JS: const rawReligions = newReligions.map(...)
            var rawReligions = newReligions.Select(religion =>
            {
                var type = religion.Group;        // JS: type
                var form = religion.Form;         // JS: form
                var cultureId = religion.CultureId; // JS: cultureId
                var center = religion.CenterCell; // JS: center

                string supreme = GetDeityName(cultureId);
                string deity = (form == "Non-theism" || form == "Animism") ? null : supreme;

                int stateId = cells[center].StateId;

                var (name, expansion) = GenerateReligionName(type, form, supreme, center);
                if (expansion == "state" && stateId == 0) expansion = "global";

                double expansionism = ExpansionismMap[type](pack.Rng);
                string color = GetReligionColor(cultures[cultureId], type);

                return new MapReligion
                {
                    Name = name,
                    Group = type,
                    Form = form,
                    CultureId = cultureId,
                    CenterCell = center,
                    Deity = deity,
                    Expansion = expansion,
                    Expansionism = expansionism,
                    Color = color
                };
            }).ToList();

            return rawReligions;

            // ==============================================================================
            // LOCAL FUNCTIONS (Exact JS Parity)
            // ==============================================================================

            string GetReligionColor(MapCulture culture, ReligionGroup type)
            {
                if (culture.Id == 0) return $"#{pack.Rng.Next(0, 0x1000000):x6}";

                if (type == ReligionGroup.Folk) return culture.Color;
                if (type == ReligionGroup.Heresy) return ColorUtils.GetMixedColor(culture.Color, pack.Rng, 0.35, 0.2);
                if (type == ReligionGroup.Cult) return ColorUtils.GetMixedColor(culture.Color, pack.Rng, 0.5, 0.0);

                return ColorUtils.GetMixedColor(culture.Color, pack.Rng, 0.25, 0.4);
            }

            string GetDeityName(int cultureId)
            {
                var culture = cultures[cultureId];
                // JS: const supreme = Names.getCulture(culture);
                string supreme = NameModule.GetCulture(pack.Rng, culture.BaseNameId);

                string approach = pack.Rng.Ra(Approaches.ToArray());
                switch (approach)
                {
                    case "Number": return pack.Rng.Ra(BaseNumber);
                    case "Being": return pack.Rng.Ra(BaseBeing);
                    case "Adjective": return pack.Rng.Ra(BaseAdjective);
                    case "Color + Animal": return $"{pack.Rng.Ra(BaseColor)} {pack.Rng.Ra(BaseAnimal)}";
                    case "Adjective + Animal": return $"{pack.Rng.Ra(BaseAdjective)} {pack.Rng.Ra(BaseAnimal)}";
                    case "Adjective + Being": return $"{pack.Rng.Ra(BaseAdjective)} {pack.Rng.Ra(BaseBeing)}";
                    case "Adjective + Genitive": return $"{pack.Rng.Ra(BaseAdjective)} {pack.Rng.Ra(BaseGenitive)}";
                    case "Color + Being": return $"{pack.Rng.Ra(BaseColor)} {pack.Rng.Ra(BaseBeing)}";
                    case "Color + Genitive": return $"{pack.Rng.Ra(BaseColor)} {pack.Rng.Ra(BaseGenitive)}";
                    case "Being + of + Genitive": return $"{pack.Rng.Ra(BaseBeing)} of {pack.Rng.Ra(BaseGenitive)}";
                    case "Being + of the + Genitive": return $"{pack.Rng.Ra(BaseBeing)} of the {pack.Rng.Ra(BaseGenitive)}";
                    case "Animal + of + Genitive": return $"{pack.Rng.Ra(BaseAnimal)} of {pack.Rng.Ra(BaseGenitive)}";
                    case "Adjective + Being + of + Genitive": return $"{pack.Rng.Ra(BaseAdjective)} {pack.Rng.Ra(BaseBeing)} of {pack.Rng.Ra(BaseGenitive)}";
                    case "Adjective + Animal + of + Genitive": return $"{pack.Rng.Ra(BaseAdjective)} {pack.Rng.Ra(BaseAnimal)} of {pack.Rng.Ra(BaseGenitive)}";
                    default: return supreme;
                }
            }

            (string name, string expansion) GenerateReligionName(ReligionGroup variety, string form, string supreme, int center)
            {
                // JS: const type = form === "Non-theism" ? "Beliefs" : rw(types[form]);
                string type = form == "Non-theism" ? "Beliefs" : pack.Rng.Rw(Types[form]);

                // JS: const deity = supreme.split(/[ ,]+/)[0];
                string deity = string.IsNullOrEmpty(supreme) ? "" : supreme.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)[0];

                var culture = cultures[cells[center].CultureId];
                string cultureName = culture.Name;
                int baseId = culture.BaseNameId;

                string GetPlace(bool adj)
                {
                    int burgId = cells[center].BurgId;
                    int stateId = cells[center].StateId;

                    string baseName = burgId > 0 ? pack.Burgs[burgId].Name : (stateId > 0 ? pack.States[stateId].Name : cultureName);
                    string placeName = LanguageUtils.TrimVowels(baseName.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)[0]);
                    return adj ? LanguageUtils.GetAdjective(placeName, pack.Rng) : placeName;
                }

                string m = pack.Rng.Rw(NamingMethods[variety]);

                // Integrates perfectly with NameModule using the Culture's BaseNameId
                if (m == "Random + type") return ($"{NameModule.GetCultureShort(pack.Rng, baseId)} {type}", "global");
                if (m == "Random + ism") return ($"{LanguageUtils.TrimVowels(NameModule.GetCulture(pack.Rng, baseId))}ism", "global");
                if (m == "Supreme + ism" && !string.IsNullOrEmpty(deity)) return ($"{LanguageUtils.TrimVowels(deity)}ism", "global");
                if (m == "Faith of + Supreme" && !string.IsNullOrEmpty(deity)) return ($"{pack.Rng.Ra(new[] { "Faith", "Way", "Path", "Word", "Witnesses" })} of {deity}", "global");
                if (m == "Place + ism") return ($"{GetPlace(false)}ism", "state");
                if (m == "Culture + ism") return ($"{LanguageUtils.TrimVowels(cultureName)}ism", "culture");
                if (m == "Place + ian + type") return ($"{GetPlace(true)} {type}", "state");
                if (m == "Culture + type") return ($"{cultureName} {type}", "culture");
                if (m == "Burg + ian + type") return ($"{GetPlace(true)} {type}", "global");
                if (m == "Random + ian + type") return ($"{LanguageUtils.GetAdjective(NameModule.GetCultureShort(pack.Rng, baseId), pack.Rng)} {type}", "global");
                if (m == "Type + of the + meaning") return ($"{type} of the {NameModule.GetCultureShort(pack.Rng, baseId)}", "global");

                return ($"{GetPlace(true)} {type}", "global");
            }
        }

        #endregion

        #region Combine

        private static List<MapReligion> CombineReligions(MapPack pack, List<MapReligion> namedReligions)
        {
            var indexedReligions = new List<MapReligion>
            {
                new MapReligion { Id = 0, Name = "No religion" }
            };

            var codes = new List<string>();
            int index = 1;

            foreach (var nextReligion in namedReligions)
            {
                string newName = RenameOld(nextReligion);
                string code = LanguageUtils.Abbreviate(newName, codes);
                codes.Add(code);

                nextReligion.Id = index;
                nextReligion.Name = newName;
                nextReligion.Code = code;

                indexedReligions.Add(nextReligion);
                index++;
            }

            return indexedReligions;

            // ==============================================================================
            // LOCAL FUNCTIONS
            // ==============================================================================

            // Prepend 'Old' to names of folk religions which have organized competitors
            string RenameOld(MapReligion rel)
            {
                if (rel.Group != ReligionGroup.Folk) return rel.Name;

                bool haveOrganized = namedReligions.Any(r =>
                    r.CultureId == rel.CultureId &&
                    r.Group == ReligionGroup.Organized &&
                    r.Expansion == "culture"
                );

                if (haveOrganized && !rel.Name.StartsWith("Old "))
                    return $"Old {rel.Name}";

                return rel.Name;
            }
        }

        #endregion

        #region Expand Religions

        private static ushort[] ExpandReligions(MapPack pack, List<MapReligion> religions)
        {
            var cells = pack.Cells;
            ushort[] religionIds = SpreadFolkReligions(pack, religions);

            // Queue elements: (int cellId, double p, int r, int state)
            var queue = new PriorityQueue<(int cellId, double p, int r, int state), double>();

            var cost = new double[cells.Length];
            for (int i = 0; i < cost.Length; i++) cost[i] = double.PositiveInfinity;

            // limit cost for organized religions growth
            double maxExpansionCost = (cells.Length / 20.0) * pack.Options.GrowthRate;

            foreach (var r in religions.Where(rel => rel.Id > 0 && rel.Group != ReligionGroup.Folk))
            {
                religionIds[r.CenterCell] = (ushort)r.Id;
                queue.Enqueue((r.CenterCell, 0.0, r.Id, cells[r.CenterCell].StateId), 0.0);
                cost[r.CenterCell] = 1.0;
            }

            var religionsMap = religions.ToDictionary(r => r.Id);
            var biomes = BiomModule.GetDefaultBiomes();

            while (queue.Count > 0)
            {
                queue.TryDequeue(out var current, out double _);
                int cellId = current.cellId;
                double p = current.p;
                int r = current.r;
                int state = current.state;

                var religion = religionsMap[r];
                int culture = religion.CultureId;
                string expansion = religion.Expansion;
                double expansionism = religion.Expansionism;

                foreach (int nextCell in cells[cellId].NeighborCells)
                {
                    if (expansion == "culture" && culture != cells[nextCell].CultureId) continue;
                    if (expansion == "state" && state != cells[nextCell].StateId) continue;

                    double cultureCost = (culture != cells[nextCell].CultureId) ? 10.0 : 0.0;
                    double stateCost = (state != cells[nextCell].StateId) ? 10.0 : 0.0;
                    double passageCost = GetPassageCost(cellId, nextCell);

                    double cellCost = cultureCost + stateCost + passageCost;
                    double totalCost = p + 10.0 + cellCost / expansionism;

                    if (totalCost > maxExpansionCost) continue;

                    if (totalCost < cost[nextCell])
                    {
                        if (cells[nextCell].CultureId > 0) religionIds[nextCell] = (ushort)r; // assign religion to cell
                        cost[nextCell] = totalCost;

                        queue.Enqueue((nextCell, totalCost, r, state), totalCost);
                    }
                }
            }

            return religionIds;

            // ==============================================================================
            // LOCAL FUNCTION (Exact JS Parity)
            // ==============================================================================

            double GetPassageCost(int cId, int nextCId)
            {
                int routeId = -1;

                bool hasRoute = pack.RouteLinks.TryGetValue(cId, out var nextLinks) &&
                                nextLinks.TryGetValue(nextCId, out routeId);

                MapRoute route = hasRoute ? pack.Routes.FirstOrDefault(rt => rt.Id == routeId) : null;

                if (cells[cId].Height < 20) // isWater(cellId)
                {
                    return route != null ? 50.0 : 500.0;
                }

                double biomePassageCost = biomes[cells[nextCId].BiomeId].MovementCost;

                if (route != null)
                {
                    if (route.Group == RouteType.Roads) return 1.0;
                    return biomePassageCost / 3.0; // trails and other routes
                }

                return biomePassageCost;
            }
        }

        // folk religions initially get all cells of their culture
        private static ushort[] SpreadFolkReligions(MapPack pack, List<MapReligion> religions)
        {
            var cells = pack.Cells;
            var religionIds = new ushort[cells.Length];

            var folkReligions = religions.Where(r => r.Group == ReligionGroup.Folk).ToList();

            var cultureToReligionMap = new Dictionary<int, ushort>();
            foreach (var r in folkReligions)
            {
                cultureToReligionMap[r.CultureId] = (ushort)r.Id;
            }

            foreach (var cell in cells)
            {
                int cellId = cell.Index;
                int cultureId = cell.CultureId;

                if (cultureToReligionMap.TryGetValue(cultureId, out ushort relId))
                {
                    religionIds[cellId] = relId;
                }
                else
                {
                    religionIds[cellId] = 0;
                }
            }

            return religionIds;
        }

        #endregion

        #region Origins
        private static List<MapReligion> DefineOrigins(MapPack pack, ushort[] religionIds, List<MapReligion> indexedReligions)
        {
            foreach (var religion in indexedReligions)
            {
                if (religion.Id == 0)
                {
                    religion.Origins = new List<int>(); // No religion
                    continue;
                }

                if (religion.Group == ReligionGroup.Folk)
                {
                    religion.Origins = new List<int> { 0 }; // Folk religions originate from their parent culture only
                    continue;
                }

                var folkReligion = indexedReligions.FirstOrDefault(r =>
                    r.Group == ReligionGroup.Folk && r.CultureId == religion.CultureId);

                // JS: const isFolkBased = folkReligion && cultureId && expansion === "culture" && each(2)(center);
                // We use center % 2 == 0 to mimic Azgaar's non-RNG-consuming deterministic 50% drop rate.
                bool isFolkBased = folkReligion != null &&
                                   religion.CultureId > 0 &&
                                   religion.Expansion == "culture" &&
                                   (religion.CenterCell % 2 == 0);

                if (isFolkBased)
                {
                    religion.Origins = new List<int> { folkReligion.Id };
                    continue;
                }

                // JS: const {clusterSize, maxReligions} = religionOriginsParamsMap[type];
                int clusterSize = 100;
                int maxReligions = 2;

                if (religion.Group == ReligionGroup.Cult)
                {
                    clusterSize = 50;
                    maxReligions = 3;
                }
                else if (religion.Group == ReligionGroup.Heresy)
                {
                    clusterSize = 50;
                    maxReligions = 4;
                }

                int fallbackOrigin = folkReligion != null ? folkReligion.Id : 0;

                religion.Origins = GetReligionsInRadius(
                    pack.Cells,
                    religion.CenterCell,
                    religionIds,
                    religion.Id,
                    clusterSize,
                    maxReligions,
                    fallbackOrigin
                );
            }

            return indexedReligions;
        }

        private static List<int> GetReligionsInRadius(MapCell[] cells, int center, ushort[] religionIds, int religionId, int clusterSize, int maxReligions, int fallbackOrigin)
        {
            var foundReligions = new HashSet<int>();
            var queue = new Queue<int>();
            queue.Enqueue(center);

            // Fast visited tracking array instead of JS object dictionary for maximum performance
            var checkedCells = new bool[cells.Length];

            for (int size = 0; queue.Count > 0 && size < clusterSize; size++)
            {
                int cellId = queue.Dequeue();
                checkedCells[cellId] = true;

                foreach (int neibId in cells[cellId].NeighborCells)
                {
                    if (checkedCells[neibId]) continue;
                    checkedCells[neibId] = true;

                    int neibReligion = religionIds[neibId];
                    if (neibReligion > 0 && neibReligion < religionId)
                    {
                        foundReligions.Add(neibReligion);
                    }

                    if (foundReligions.Count >= maxReligions)
                    {
                        return foundReligions.ToList();
                    }

                    queue.Enqueue(neibId);
                }
            }

            return foundReligions.Count > 0 ? foundReligions.ToList() : new List<int> { fallbackOrigin };
        }

        #endregion

        #region Check Centers

        private static void CheckCenters(MapPack pack)
        {
            var cells = pack.Cells;

            foreach (var r in pack.Religions)
            {
                if (r.Id == 0) continue;

                // Move religion center if it's not within religion area after expansion
                if (cells[r.CenterCell].ReligionId == r.Id) continue; // It is safely inside its area

                // JS: const firstCell = cells.i.find(i => cells.religion[i] === r.i);
                int firstCellId = -1;
                for (int i = 0; i < cells.Length; i++)
                {
                    if (cells[i].ReligionId == r.Id)
                    {
                        firstCellId = i;
                        break;
                    }
                }

                // Locate the culture's center, if the culture exists
                var culture = pack.Cultures.FirstOrDefault(c => c.Id == r.CultureId);
                int cultureHome = culture != null ? culture.CenterCell : -1;

                if (firstCellId != -1)
                {
                    r.CenterCell = firstCellId; // move center, otherwise it's an extinct religion
                }
                else if (r.Group == ReligionGroup.Folk && cultureHome != -1)
                {
                    r.CenterCell = cultureHome; // reset extinct culture centers
                }
            }
        }

        #endregion
    }
}