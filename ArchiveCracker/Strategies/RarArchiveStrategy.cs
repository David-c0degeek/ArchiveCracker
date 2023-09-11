using SharpCompress.Archives;
using SharpCompress.Readers;
using SharpCompress.Common;
using SharpCompress.Readers.Rar;

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
        catch (CryptographicException)
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
            using Stream stream = File.OpenRead(file);
            var readerOptions = new ReaderOptions
            {
                Password = password,
                LookForHeader = true
            };

            using var reader = RarReader.Open(stream, readerOptions);
            return reader.MoveToNextEntry();
        }
        catch (CryptographicException)
        {
            // Password is incorrect
            return false;
        }
        catch (Exception)
        {
            // Some other error occurred
            return false;
        }
    }
}