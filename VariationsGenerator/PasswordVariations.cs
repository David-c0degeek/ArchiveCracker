namespace VariationsGenerator;

public static class PasswordVariations
{
    public static IEnumerable<string> SimpleVariations(string password)
    {
        yield return password.ToUpper();
        yield return password.ToLower();
    }

    public static IEnumerable<string> Reversed(string password)
    {
        var charArray = password.ToCharArray();
        Array.Reverse(charArray);
        yield return new string(charArray);
    }

    public static IEnumerable<string> CharacterReplacement(string password)
    {
        yield return password.Replace('a', '@').Replace('e', '3').Replace('i', '1').Replace('o', '0');
    }

    public static IEnumerable<string> AppendNumbersAndSymbols(string password)
    {
        for (var i = 0; i < 10; i++)
        {
            yield return password + i;
        }

        yield return password + "!";
        yield return password + "@";
        yield return password + "#";
    }

    public static IEnumerable<string> LeetSpeak(string password)
    {
        yield return password.Replace('e', '3').Replace('l', '1').Replace('o', '0').Replace('t', '7');
    }

    public static IEnumerable<string> AppendPrependDates(string password)
    {
        var years = Enumerable.Range(DateTime.Now.Year - 100, 100).Select(x => x.ToString());
        foreach (var year in years)
        {
            yield return password + year;
            yield return year + password;
        }
    }

    public static IEnumerable<string> AppendPrependCommonSequences(string password)
    {
        var sequences = new List<string> { "123", "111", "777", "000" };
        foreach (var seq in sequences)
        {
            yield return password + seq;
            yield return seq + password;
        }
    }

    public static IEnumerable<string> CapitalizeLetters(string password)
    {
        if (password.Length <= 0) yield break;

        yield return char.ToUpper(password[0]) + password[1..];
        yield return password.ToUpper();
    }

    public static IEnumerable<string> CommonPrefixesSuffixes(string password)
    {
        var prefixesSuffixes = new List<string> { "my", "your", "123", "!" };
        foreach (var prefixSuffix in prefixesSuffixes)
        {
            yield return prefixSuffix + password;
            yield return password + prefixSuffix;
        }
    }

    public static IEnumerable<string> DoublePassword(string password)
    {
        yield return password + password;
    }

    public static IEnumerable<string> MirrorPassword(string password)
    {
        var charArray = password.ToCharArray();
        Array.Reverse(charArray);
        var reversed = new string(charArray);

        yield return password + reversed;
    }

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