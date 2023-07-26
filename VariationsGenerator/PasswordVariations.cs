using System.Collections.Concurrent;

namespace VariationsGenerator;

public static class PasswordVariations
{
    private static readonly ConcurrentDictionary<(List<string>, int), IEnumerable<string>> Cache = new();

    public static IEnumerable<string> CombineWords(IEnumerable<string> lines, int maxLength)
    {
        // Convert list to HashSet to remove duplicates
        var words = new HashSet<string>(lines
                .SelectMany(line => line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)))
            .ToList();

        if (Cache.TryGetValue((words, maxLength), out var cachedResult))
        {
            return cachedResult;
        }

        var result = ComputeCombinations(words, maxLength).ToList();

        Cache[(words, maxLength)] = result;

        return result;
    }
    
    private static IEnumerable<string> ComputeCombinations(IReadOnlyList<string> words, int maxLength)
    {
        var n = words.Count;

        // Run a loop for printing all 2^n subsets one by one
        for (var i = 0; i < (1<<n); i++)
        {
            var combination = new List<string>();

            // Print current subset
            for (var j = 0; j < n; j++)
            {
                // (1<<j) is a number with jth bit 1, so when we 'and' them with the subset number we get which numbers are present in the subset and which are not
                if ((i & (1 << j)) > 0 && combination.Count < maxLength)
                    combination.Add(words[j]);
            }

            if (combination.Count <= 0 || combination.Count > maxLength) continue;
            
            // Generate all permutations of the current combination
            foreach (var permutation in GeneratePermutations(combination))
            {
                yield return string.Join(" ", permutation);
            }
        }
    }


    private static IEnumerable<IEnumerable<T>> GeneratePermutations<T>(IReadOnlyCollection<T> sequence)
    {
        if (sequence.Count == 1)
            yield return sequence;
        else
        {
            foreach (var x in sequence)
            {
                var rest = sequence.Except(new[] {x}).ToList();
                var restPermutations = GeneratePermutations(rest);
                foreach (var restPermutation in restPermutations)
                {
                    yield return new[] {x}.Concat(restPermutation);
                }
            }
        }
    }
}