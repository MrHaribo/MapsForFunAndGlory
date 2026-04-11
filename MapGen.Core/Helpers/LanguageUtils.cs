using System;
using System.Collections.Generic;
using System.Text;

namespace MapGen.Core.Helpers
{
    public static class LanguageUtils
    {
        public static string Abbreviate(string name, List<string> restricted)
        {
            // 1. JS: name.replace("Old ", "O ").replace(/[()]/g, "")
            string parsed = name.Replace("Old ", "O ").Replace("(", "").Replace(")", "");

            // 2. JS: const words = parsed.split(" ");
            string[] words = parsed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string letters = string.Join("", words);

            // 3. Initial Code Logic
            // JS: words.length === 2 ? words[0][0] + words[1][0] : letters.slice(0, 2);
            string code = words.Length == 2
                ? $"{words[0][0]}{words[1][0]}"
                : (letters.Length >= 2 ? letters.Substring(0, 2) : letters);

            // 4. Collision Resolution Loop
            // JS: for (let i = 1; i < letters.length - 1 && restricted.includes(code); i++)
            for (int i = 1; i < letters.Length && restricted.Contains(code); i++)
            {
                // JS: code = letters[0] + letters[i].toUpperCase();
                code = $"{letters[0]}{char.ToUpper(letters[i])}";
            }

            return code;
        }
        public static bool IsVowel(char c)
        {
            const string vowels = "aeiouyɑ'əøɛœæɶɒɨɪɔɐʊɤɯаоиеёэыуюяàèìòùỳẁȁȅȉȍȕáéíóúýẃőűâêîôûŷŵäëïöüÿẅãẽĩõũỹąęįǫųāēīōūȳăĕĭŏŭǎěǐǒǔȧėȯẏẇạẹịọụỵẉḛḭṵṳ";
            return vowels.Contains(c);
        }
    }
}
