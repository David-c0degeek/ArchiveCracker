namespace ArchiveCracker.Strategies;

public interface IArchiveStrategy
{
    bool IsPasswordProtected(string file);
    bool IsPasswordCorrect(string file, string password);
}