namespace VariationsGenerator.Strategies;

public interface IPasswordVariationStrategy
{
    IEnumerable<string> GenerateVariations(string basePassword);
}
