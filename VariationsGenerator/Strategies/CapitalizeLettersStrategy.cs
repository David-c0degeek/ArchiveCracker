namespace VariationsGenerator.Strategies;

public class CapitalizeLettersStrategy : IPasswordVariationStrategy
{
    public IEnumerable<string> GenerateVariations(string basePassword)
    {
        if (basePassword.Length <= 0) yield break;
        
        yield return char.ToUpper(basePassword[0]) + basePassword[1..];
        yield return basePassword.ToUpper();
    }
}