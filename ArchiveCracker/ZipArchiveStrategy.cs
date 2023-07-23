using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Readers;

namespace ArchiveCracker;

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