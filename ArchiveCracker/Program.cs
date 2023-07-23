using System.IO.Compression;
using System.Collections.Concurrent;
using Newtonsoft.Json;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace ArchiveCracker
{
    internal abstract class Program
    {
        private static List<string>? CommonPasswords { get; set; }

        private static ConcurrentBag<(string, string)> FoundPasswords { get; set; } = new();

        private static ConcurrentBag<string> _rarFiles = new();
        private static ConcurrentBag<string> _sevenZFiles = new();
        private static ConcurrentBag<string> _zipFiles = new();
        private static ConcurrentBag<string> _splitFiles = new();

        private static void Main()
        {
            Init();
            LoadArchives();
            CheckPasswords();
            SaveFoundPasswords();
        }

        private static void Init()
        {
            CommonPasswords = new List<string>(File.ReadAllLines("common_passwords.txt"));
        }

        private static void LoadArchives()
        {
            _rarFiles = new ConcurrentBag<string>();
            _sevenZFiles = new ConcurrentBag<string>();
            _zipFiles = new ConcurrentBag<string>();
            _splitFiles = new ConcurrentBag<string>();

            var files = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.*", SearchOption.AllDirectories);

            Parallel.ForEach(files, file =>
            {
                var ext = Path.GetExtension(file).ToLower();
                switch (ext)
                {
                    case ".rar":
                        if (IsRarPasswordProtected(file))
                        {
                            _rarFiles.Add(file);
                        }

                        break;
                    case ".7z":
                        if (IsSevenZPasswordProtected(file))
                        {
                            _sevenZFiles.Add(file);
                        }

                        break;
                    case ".zip":
                        if (IsZipPasswordProtected(file))
                        {
                            _zipFiles.Add(file);
                        }

                        break;
                    case ".001":
                        if (IsSevenZPasswordProtected(file)) // Checking password protection on first volume
                        {
                            _splitFiles.Add(file);
                        }

                        break;
                }
            });
        }

        private static bool IsZipPasswordProtected(string file)
        {
            try
            {
                using var archive = ZipFile.OpenRead(file);
                foreach (var entry in archive.Entries)
                {
                    if (entry.Name.EndsWith("/")) continue; // exclude directories

                    // Attempt to open the stream which will throw if password protected
                    using (entry.Open())
                    {
                    }
                }

                return false;
            }
            catch (InvalidDataException)
            {
                // If it throws an InvalidDataException, the file is password protected.
                return true;
            }
            catch
            {
                // If any other exception is thrown, the file might be corrupted.
                return false;
            }
        }

        private static bool IsRarPasswordProtected(string file)
        {
            try
            {
                using var archive = ArchiveFactory.Open(file);

                foreach (var entry in archive.Entries)
                {
                    if (!entry.IsDirectory)
                    {
                        entry.WriteTo(Stream.Null);
                    }
                }

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

        private static bool IsSevenZPasswordProtected(string file)
        {
            return IsRarPasswordProtected(file); // 7z and RAR password detection is the same in SharpCompress
        }

        private static void CheckPasswords()
        {
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };

            Parallel.ForEach(_rarFiles, parallelOptions, file =>
            {
                if (CommonPasswords == null) return;

                foreach (var password in CommonPasswords.Where(password => IsRarPasswordCorrect(file, password)))
                {
                    FoundPasswords.Add((file, password));
                    break;
                }
            });

            Parallel.ForEach(_sevenZFiles, parallelOptions, file =>
            {
                if (CommonPasswords == null) return;

                foreach (var password in CommonPasswords.Where(password => IsSevenZPasswordCorrect(file, password)))
                {
                    FoundPasswords.Add((file, password));
                    break;
                }
            });

            Parallel.ForEach(_zipFiles, parallelOptions, file =>
            {
                if (CommonPasswords == null) return;

                foreach (var password in CommonPasswords.Where(password => IsZipPasswordCorrect(file, password)))
                {
                    FoundPasswords.Add((file, password));
                    break;
                }
            });

            Parallel.ForEach(_splitFiles, parallelOptions, file =>
            {
                if (CommonPasswords == null) return;

                foreach (var password in CommonPasswords.Where(password => IsSplitPasswordCorrect(file, password)))
                {
                    FoundPasswords.Add((file, password));
                    break;
                }
            });
        }
        
        private static bool IsRarPasswordCorrect(string file, string password)
        {
            try
            {
                using var archive = RarArchive.Open(file, new ReaderOptions
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
            catch (CryptographicException)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsSevenZPasswordCorrect(string file, string password)
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
            catch (CryptographicException)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }

        // As the built-in .NET ZipFile does not support passwords, you may need to use SharpCompress or another library here.
        private static bool IsZipPasswordCorrect(string file, string password)
        {
            return false;
        }

        private static bool IsSplitPasswordCorrect(string file, string password)
        {
            return IsSevenZPasswordCorrect(file, password);
        }
        

        private static void SaveFoundPasswords()
        {
            var json = JsonConvert.SerializeObject(FoundPasswords, Formatting.Indented);
            File.WriteAllText("found_passwords.json", json);
        }
    }
}