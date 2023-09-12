using System.Globalization;

namespace ArchiveCracker.Services
{
    public class PasswordGuessService
    {
        public static IEnumerable<string> GenerateGuessPasswords(string filename)
        {
            var guessPasswords = new HashSet<string>();

            // 1. Extract the name without extension
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(filename);
            AddSingleWordVariants(guessPasswords, nameWithoutExtension);

            // 1.1 Consider spaces: split filename into words and generate combinations
            var words = SplitIntoWords(nameWithoutExtension);
            if (words.Length <= 1) return guessPasswords;
            
            foreach (var word in words)
            {
                AddSingleWordVariants(guessPasswords, word);
            }

            // Generate combinations of 2 or more words
            AddWordCombinations(guessPasswords, words);

            return guessPasswords;
        }

        private static void AddSingleWordVariants(ISet<string> guessPasswords, string word)
        {
            guessPasswords.Add(word);
            guessPasswords.Add(word.ToUpper());
            guessPasswords.Add(word.ToLower());
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
            // Assuming `text` is in lower case
            guessPasswords.Add(CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text));
            guessPasswords.Add(new string(text.Select((c, i) => i % 2 == 0 ? char.ToUpper(c) : c).ToArray()));
        }
    }
}
