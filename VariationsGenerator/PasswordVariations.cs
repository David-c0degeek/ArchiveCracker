namespace VariationsGenerator;

public static class PasswordVariations
{
    public static IEnumerable<string> CombineWords(List<string> words, int maxLength)
    {
        for (var length = 1; length <= maxLength; length++)
        {
            foreach (var combination in GetCombinations(words, length))
            {
                yield return string.Join(" ", combination);
            }
        }
    }

    private static IEnumerable<IEnumerable<T>> GetCombinations<T>(IReadOnlyCollection<T> list, int length)
    {
        if (length == 1) return list.Select(t => new[] { t });
        return GetCombinations(list, length - 1)
            .SelectMany(t => list.Where(o => !t.Contains(o)),
                (t1, t2) => t1.Concat(new[] { t2 }));
    }
}