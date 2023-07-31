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
            var firstEntry = archive.Entries.FirstOrDefault(e => !e.IsDirectory);

            // If there are no entries or they are all directories, return false
            if (firstEntry == null) return false;

            // Try to extract the first non-directory entry
            firstEntry.WriteTo(Stream.Null);

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