namespace VariationsGenerator.Strategies;

public class CommonPrefixesSuffixesStrategy : IPasswordVariationStrategy
{
    public IEnumerable<string> GenerateVariations(string basePassword)
    {
        var prefixesSuffixes = new List<string> { "my", "your", "123", "!" };
        foreach (var prefixSuffix in prefixesSuffixes)
        {
            yield return prefixSuffix + basePassword;
            yield return basePassword + prefixSuffix;
        }
    }
}