namespace VariationsGenerator.Strategies;

public class CharacterReplacementStrategy : IPasswordVariationStrategy
{
    public IEnumerable<string> GenerateVariations(string basePassword)
    {
        yield return basePassword.Replace('a', '@').Replace('e', '3').Replace('i', '1').Replace('o', '0');
    }
}