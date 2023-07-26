namespace VariationsGenerator.Strategies;

public class MirrorPasswordStrategy : IPasswordVariationStrategy
{
    public IEnumerable<string> GenerateVariations(string basePassword)
    {
        var charArray = basePassword.ToCharArray();
        Array.Reverse(charArray);
        var reversed = new string(charArray);
        yield return basePassword + reversed;
    }
}