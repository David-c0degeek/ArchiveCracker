using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Readers;

namespace ArchiveCracker.Strategies;

internal class ZipArchiveStrategy : IArchiveStrategy
{
    public bool IsPasswordProtected(string file)
    {
        try
        {
            using var archive = ZipArchive.Open(file);
            foreach (var entry in archive.Entries)
            {
                if (!entry.IsDirectory)
                {
                    entry.WriteTo(Stream.Null);
                }
            }
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
            using var archive = ZipArchive.Open(file, new ReaderOptions
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