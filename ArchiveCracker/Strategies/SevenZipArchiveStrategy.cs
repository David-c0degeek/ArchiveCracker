using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace ArchiveCracker.Strategies
{
    internal class SevenZipArchiveStrategy : IArchiveStrategy
    {
        public bool IsPasswordProtected(string file)
        {
            try
            {
                using var archive = SevenZipArchive.Open(file);
                var firstEntry = archive.Entries.FirstOrDefault(e => !e.IsDirectory);
                
                // If there are no entries or they are all directories, return false
                if (firstEntry == null) return false;
                
                // Access some property to force an attempt to decrypt the metadata
                _ = firstEntry.Size;
                
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
                using var archive = SevenZipArchive.Open(file, new ReaderOptions
                {
                    Password = password
                });
                var firstEntry = archive.Entries.FirstOrDefault(e => !e.IsDirectory);

                // If there are no entries or they are all directories, return false
                if (firstEntry == null) return false;
                
                // Access some property to force an attempt to decrypt the metadata
                _ = firstEntry.Size;

                return true;
            }
            catch (CryptographicException)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}