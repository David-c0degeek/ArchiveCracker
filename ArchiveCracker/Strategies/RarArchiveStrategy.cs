using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Readers;

namespace ArchiveCracker.Strategies;

internal class RarArchiveStrategy : IArchiveStrategy
{
    public bool IsPasswordProtected(string file)
    {
        try
        {
            using var archive = ArchiveFactory.Open(file);
            var firstEntry = archive.Entries.FirstOrDefault(e => !e.IsDirectory);

            // If there are no entries or they are all directories, return false
            if (firstEntry == null) return false;

            // Try to extract the first non-directory entry
            firstEntry.WriteTo(Stream.Null);

            return false;
        }
        catch (SharpCompress.Common.CryptographicException)
        {
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool IsPasswordCorrect(string file, string password)
    {
        try
        {
            using var archive = RarArchive.Open(file, new ReaderOptions
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