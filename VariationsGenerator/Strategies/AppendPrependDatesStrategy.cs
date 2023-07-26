namespace VariationsGenerator.Strategies;

public class AppendPrependDatesStrategy : IPasswordVariationStrategy
{
    public IEnumerable<string> GenerateVariations(string basePassword)
    {
        var years = Enumerable.Range(DateTime.Now.Year - 100, 100).Select(x => x.ToString());
        foreach (var year in years)
        {
            yield return basePassword + year;
            yield return year + basePassword;
        }
    }
}