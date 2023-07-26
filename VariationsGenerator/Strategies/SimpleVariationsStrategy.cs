namespace VariationsGenerator.Strategies;

public class SimpleVariationsStrategy : IPasswordVariationStrategy
{
    public IEnumerable<string> GenerateVariations(string basePassword)
    {
        yield return basePassword.ToUpper();
        yield return basePassword.ToLower();
    }
}
