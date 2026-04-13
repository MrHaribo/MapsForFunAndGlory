using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MapGen.Core.Helpers
{
    public static class LanguageUtils
    {
        private class AdjectiveRule
        {
            public string Name { get; set; }
            public double Probability { get; set; }
            public Regex Condition { get; set; }
            public Func<string, string> Action { get; set; }
        }

        private static readonly List<AdjectiveRule> AdjectivizationRules = new List<AdjectiveRule>
        {
            new AdjectiveRule { Name = "guo", Probability = 1.0, Condition = new Regex(" Guo$"), Action = noun => noun.Substring(0, noun.Length - 4) },
            new AdjectiveRule { Name = "orszag", Probability = 1.0, Condition = new Regex("orszag$"), Action = noun => noun.Length < 9 ? noun + "ian" : noun.Substring(0, noun.Length - 6) },
            new AdjectiveRule { Name = "stan", Probability = 1.0, Condition = new Regex("stan$"), Action = noun => noun.Length < 9 ? noun + "i" : TrimVowels(noun.Substring(0, noun.Length - 4)) },
            new AdjectiveRule { Name = "land", Probability = 1.0, Condition = new Regex("land$"), Action = noun =>
            {
                if (noun.Length > 9) return noun.Substring(0, noun.Length - 4);
                string root = TrimVowels(noun.Substring(0, noun.Length - 4), 0);
                if (root.Length < 3) return noun + "ic";
                if (root.Length < 4) return root + "lish";
                return root + "ish";
            }},
            new AdjectiveRule { Name = "que", Probability = 1.0, Condition = new Regex("que$"), Action = noun => Regex.Replace(noun, "que$", "can") },
            new AdjectiveRule { Name = "a", Probability = 1.0, Condition = new Regex("a$"), Action = noun => noun + "n" },
            new AdjectiveRule { Name = "o", Probability = 1.0, Condition = new Regex("o$"), Action = noun => Regex.Replace(noun, "o$", "an") },
            new AdjectiveRule { Name = "u", Probability = 1.0, Condition = new Regex("u$"), Action = noun => noun + "an" },
            new AdjectiveRule { Name = "i", Probability = 1.0, Condition = new Regex("i$"), Action = noun => noun + "an" },
            new AdjectiveRule { Name = "e", Probability = 1.0, Condition = new Regex("e$"), Action = noun => noun + "an" },
            new AdjectiveRule { Name = "ay", Probability = 1.0, Condition = new Regex("ay$"), Action = noun => noun + "an" },
            new AdjectiveRule { Name = "os", Probability = 1.0, Condition = new Regex("os$"), Action = noun =>
            {
                string root = TrimVowels(noun.Substring(0, noun.Length - 2), 0);
                if (root.Length < 4) return noun.Substring(0, noun.Length - 1);
                return root + "ian";
            }},
            new AdjectiveRule { Name = "es", Probability = 1.0, Condition = new Regex("es$"), Action = noun =>
            {
                string root = TrimVowels(noun.Substring(0, noun.Length - 2), 0);
                if (root.Length > 7) return noun.Substring(0, noun.Length - 1);
                return root + "ian";
            }},
            new AdjectiveRule { Name = "l", Probability = 0.8, Condition = new Regex("l$"), Action = noun => noun + "ese" },
            new AdjectiveRule { Name = "n", Probability = 0.8, Condition = new Regex("n$"), Action = noun => noun + "ese" },
            new AdjectiveRule { Name = "ad", Probability = 0.8, Condition = new Regex("ad$"), Action = noun => noun + "ian" },
            new AdjectiveRule { Name = "an", Probability = 0.8, Condition = new Regex("an$"), Action = noun => noun + "ian" },
            new AdjectiveRule { Name = "ish", Probability = 0.25, Condition = new Regex("^[a-zA-Z]{6}$"), Action = noun => TrimVowels(noun.Substring(0, noun.Length - 1)) + "ish" },
            new AdjectiveRule { Name = "an", Probability = 0.5, Condition = new Regex("^[a-zA-Z]{0,7}$"), Action = noun => TrimVowels(noun) + "an" }
        };

        // get adjective form from noun
        public static string GetAdjective(string noun, IRandom rng)
        {
            if (string.IsNullOrEmpty(noun)) return noun;

            foreach (var rule in AdjectivizationRules)
            {
                // P(rule.probability) equivalent
                if ((rng.Next() < rule.Probability) && rule.Condition.IsMatch(noun))
                {
                    return rule.Action(noun);
                }
            }
            return noun; // no rule applied, return noun as is
        }

        // get ordinal from integer: 1 => 1st
        public static string Nth(int n)
        {
            int mod100 = n % 100;
            int mod10 = n % 10;

            if (mod100 >= 11 && mod100 <= 13)
                return n + "th";

            switch (mod10)
            {
                case 1: return n + "st";
                case 2: return n + "nd";
                case 3: return n + "rd";
                default: return n + "th";
            }
        }

        // get two-letters code (abbreviation) from string
        public static string Abbreviate(string name, List<string> restricted)
        {
            // 1. JS: name.replace("Old ", "O ").replace(/[()]/g, "")
            string parsed = name.Replace("Old ", "O ").Replace("(", "").Replace(")", "");

            // 2. JS: const words = parsed.split(" ");
            string[] words = parsed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string letters = string.Join("", words);

            if (string.IsNullOrEmpty(letters)) return "";

            // 3. Initial Code Logic
            // JS: words.length === 2 ? words[0][0] + words[1][0] : letters.slice(0, 2);
            string code = words.Length == 2
                ? $"{words[0][0]}{words[1][0]}"
                : (letters.Length >= 2 ? letters.Substring(0, 2) : letters);

            // 4. Collision Resolution Loop
            // JS: for (let i = 1; i < letters.length - 1 && restricted.includes(code); i++)
            for (int i = 1; i < letters.Length - 1 && restricted.Contains(code); i++)
            {
                // JS: code = letters[0] + letters[i].toUpperCase();
                code = $"{letters[0]}{char.ToUpper(letters[i])}";
            }

            return code;
        }

        // conjunct array: [A,B,C] => "A, B and C"
        public static string List(IEnumerable<string> array)
        {
            var list = array?.ToList() ?? new List<string>();
            if (list.Count == 0) return string.Empty;
            if (list.Count == 1) return list[0];
            if (list.Count == 2) return $"{list[0]} and {list[1]}";

            // e.g., "A, B and C"
            string joinedFirstParts = string.Join(", ", list.Take(list.Count - 1));
            return $"{joinedFirstParts} and {list.Last()}";
        }

        // remove vowels from the end of the string
        public static string TrimVowels(string text, int minLength = 3)
        {
            if (string.IsNullOrEmpty(text)) return text;

            while (text.Length > minLength && IsVowel(text[text.Length - 1]))
            {
                text = text.Substring(0, text.Length - 1);
            }

            return text;
        }

        public static bool IsVowel(char c)
        {
            const string vowels = "aeiouyɑ'əøɛœæɶɒɨɪɔɐʊɤɯаоиеёэыуюяàèìòùỳẁȁȅȉȍȕáéíóúýẃőűâêîôûŷŵäëïöüÿẅãẽĩõũỹąęįǫųāēīōūȳăĕĭŏŭǎěǐǒǔȧėȯẏẇạẹịọụỵẉḛḭṵṳ";
            return vowels.Contains(char.ToLowerInvariant(c));
        }
    }
}