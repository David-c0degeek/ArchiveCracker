namespace VariationsGenerator.Strategies;

public class LeetSpeakStrategy : IPasswordVariationStrategy
{
    public IEnumerable<string> GenerateVariations(string basePassword)
    {
        yield return basePassword.Replace('e', '3').Replace('l', '1').Replace('o', '0').Replace('t', '7');
    }
}