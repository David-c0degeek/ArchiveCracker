namespace VariationsGenerator.Strategies;

public class ReversedStrategy : IPasswordVariationStrategy
{
    public IEnumerable<string> GenerateVariations(string basePassword)
    {
        var charArray = basePassword.ToCharArray();
        Array.Reverse(charArray);
        yield return new string(charArray);
    }
}