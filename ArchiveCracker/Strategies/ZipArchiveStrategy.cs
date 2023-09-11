using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Readers.Zip;

namespace ArchiveCracker.Strategies
{
    internal class ZipArchiveStrategy : IArchiveStrategy
    {
        public bool IsPasswordProtected(string file)
        {
            try
            {
                using Stream stream = File.OpenRead(file);
                using var reader = ZipReader.Open(stream);
                return reader.MoveToNextEntry() && reader.Entry.IsEncrypted;
            }
            catch
            {
                // Some other error occurred
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

                using var reader = ZipReader.Open(stream, readerOptions);
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
}
