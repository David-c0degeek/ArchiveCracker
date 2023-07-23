using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Readers;

namespace ArchiveCracker.Strategies;

internal class SevenZipArchiveStrategy : IArchiveStrategy
{
    public bool IsPasswordProtected(string file)
    {
        return new RarArchiveStrategy().IsPasswordProtected(file);
    }

    public bool IsPasswordCorrect(string file, string password)
    {
        try
        {
            using var archive = SevenZipArchive.Open(file, new ReaderOptions
            {
                Password = password,
                LookForHeader = true
            });
            foreach (var entry in archive.Entries)
            {
                if (!entry.IsDirectory)
                {
                    entry.WriteTo(Stream.Null);
                }
            }
            return true;
        }
        catch (SharpCompress.Common.CryptographicException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }
}