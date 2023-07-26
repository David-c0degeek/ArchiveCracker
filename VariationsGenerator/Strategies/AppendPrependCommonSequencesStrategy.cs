namespace VariationsGenerator.Strategies;

public class AppendPrependCommonSequencesStrategy : IPasswordVariationStrategy
{
    public IEnumerable<string> GenerateVariations(string basePassword)
    {
        var sequences = new List<string> { "123", "111", "777", "000" };
        foreach (var seq in sequences)
        {
            yield return basePassword + seq;
            yield return seq + basePassword;
        }
    }
}