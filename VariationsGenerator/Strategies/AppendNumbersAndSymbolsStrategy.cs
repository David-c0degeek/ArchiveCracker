namespace VariationsGenerator.Strategies;

public class AppendNumbersAndSymbolsStrategy : IPasswordVariationStrategy
{
    public IEnumerable<string> GenerateVariations(string basePassword)
    {
        for (var i = 0; i < 10; i++)
        {
            yield return basePassword + i;
        }
        yield return basePassword + "!";
        yield return basePassword + "@";
        yield return basePassword + "#";
    }
}