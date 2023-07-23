namespace ArchiveCracker.Strategies;

internal interface IArchiveStrategy
{
    bool IsPasswordProtected(string file);
    bool IsPasswordCorrect(string file, string password);
}