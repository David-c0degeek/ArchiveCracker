using System.Globalization;

namespace ArchiveCracker.Services
{
    public static class PasswordGuessService
    {
        private static readonly Dictionary<char, string[]> LeetReplacements = new()
        {
            { 'a', new[] { "4", "@" } },
            { 'b', new[] { "8" } },
            { 'c', new[] { "<", "(" } },
            { 'e', new[] { "3" } },
            { 'g', new[] { "9" } },
            { 'h', new[] { "#", "4" } },
            { 'i', new[] { "1", "!", "|" } },
            { 'l', new[] { "1", "|" } },
            { 'o', new[] { "0" } },
            { 's', new[] { "$", "5" } },
            { 't', new[] { "7", "+" } },
            { 'z', new[] { "2" } }
        };

        public static List<string> GenerateGuessPasswords(string filename)
        {
            var guessPasswords = new HashSet<string>();

            // 1. Extract the name without extension
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(filename);
            AddSingleWordVariants(guessPasswords, nameWithoutExtension);

            // 1.1 Consider spaces: split filename into words and generate combinations
            var words = SplitIntoWords(nameWithoutExtension);
            if (words.Length > 1)
            {
                foreach (var word in words)
                {
                    AddSingleWordVariants(guessPasswords, word);
                }

                // Generate combinations of 2 or more words
                AddWordCombinations(guessPasswords, words);
            }

            AddLeetCombinations(guessPasswords);

            return guessPasswords
                .ToList();
        }

        private static void AddLeetCombinations(HashSet<string> guessPasswords)
        {
            var leetPasswords = new HashSet<string>();

            foreach (var guess in guessPasswords)
            {
                GenerateLeetVariants(guess, "", 0, leetPasswords);
            }

            foreach (var leet in leetPasswords)
            {
                guessPasswords.Add(leet);
            }
        }

        private static void GenerateLeetVariants(string original, string current, int index, ISet<string> leetPasswords)
        {
            if (index == original.Length)
            {
                leetPasswords.Add(current);
                return;
            }

            var ch = original[index];
            GenerateLeetVariants(original, current + ch, index + 1, leetPasswords);

            if (!LeetReplacements.TryGetValue(ch, out var replacements)) return;

            foreach (var replacement in replacements)
            {
                GenerateLeetVariants(original, current + replacement, index + 1, leetPasswords);
            }
        }

        private static void AddSingleWordVariants(ISet<string> guessPasswords, string word)
        {
            guessPasswords.Add(word);
            guessPasswords.Add(word.ToUpperInvariant());
            guessPasswords.Add(word.ToLowerInvariant());
            guessPasswords.Add(CultureInfo.CurrentCulture.TextInfo.ToTitleCase(word));
        }

        private static string[] SplitIntoWords(string text)
        {
            return text.Split(new[] { ' ', '-', '_', '.' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static void AddWordCombinations(ISet<string> guessPasswords, IReadOnlyCollection<string> words)
        {
            for (var len = 2; len <= words.Count; len++)
            {
                for (var i = 0; i <= words.Count - len; i++)
                {
                    var combinationWords = words.Skip(i).Take(len).ToArray();
                    var combination = string.Join("", combinationWords);
                    AddSingleWordVariants(guessPasswords, combination);
                    AddMixedCaseVariants(guessPasswords, combination);
                }
            }
        }

        private static void AddMixedCaseVariants(ISet<string> guessPasswords, string text)
        {
            text = text.ToLowerInvariant();
            guessPasswords.Add(CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text));
            guessPasswords.Add(new string(text.Select((c, i) => i % 2 == 0 ? char.ToUpperInvariant(c) : c).ToArray()));
        }
        
        public static IEnumerable<string> ApplyTransformationRules(string password)
        {
            // Reuse the transformation rules logic from RuleBasedDictionaryAttackService
            var rules = GetTransformationRules();
            return rules.Select(rule => rule(password));
        }
        
        private static List<Func<string, string>> GetTransformationRules()
        {
            return new List<Func<string, string>>
            {
                password => password + "123",
                password => password + "!",
                password => password + "?",
                password => char.ToUpper(password[0]) + password[1..],
                password => new string(password.Reverse().ToArray()),
                password => password + DateTime.Now.Year,
                password => string.Concat(password, DateTime.Now.Year.ToString().AsSpan(2))
            };
        }
    }
}