namespace VariationsGenerator.Strategies;

public class DoublePasswordStrategy : IPasswordVariationStrategy
{
    public IEnumerable<string> GenerateVariations(string basePassword)
    {
        yield return basePassword + basePassword;
    }
}
