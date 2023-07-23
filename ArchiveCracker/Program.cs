using System.Collections.Concurrent;
using ArchiveCracker.Models;
using ArchiveCracker.Strategies;
using CommandLine;
using Newtonsoft.Json;

namespace ArchiveCracker
{
    internal abstract class Program
    {
        private static List<string> CommonPasswords { get; set; } = new List<string>();
        private static string _userPasswordsFilePath = "user_passwords.txt";
        private static string _commonPasswordsFilePath = "common_passwords.txt";
        private static string _foundPasswordsFilePath = "found_passwords.json";

        private static BlockingCollection<FileOperation> FileOperationsQueue { get; set; } = new();
        private static ConcurrentBag<ArchivePasswordPair> FoundPasswords { get; set; } = new();

        private static readonly Dictionary<string, IArchiveStrategy> ArchiveStrategies = new()
        {
            { ".rar", new RarArchiveStrategy() },
            { ".7z", new SevenZipArchiveStrategy() },
            { ".zip", new ZipArchiveStrategy() },
            { ".001", new SevenZipArchiveStrategy() } // Checking password protection on first volume
        };

        private static readonly ConcurrentDictionary<IArchiveStrategy, ConcurrentBag<string>> ProtectedArchives = new();

        private static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(o =>
                {
                    _commonPasswordsFilePath = Path.Combine(o.PathToZipFiles, "common_passwords.txt");
                    _userPasswordsFilePath = o.UserPasswordsFilePath;
                    _foundPasswordsFilePath = Path.Combine(o.PathToZipFiles, "found_passwords.json");

                    Init();
                    LoadArchives(o.PathToZipFiles);
                    CheckPasswords();
                });

            Task.Factory.StartNew(FileOperationsWorker, TaskCreationOptions.LongRunning);
        }

        private static void Init()
        {
            EnsureFileExistsAndPrintInfo(_commonPasswordsFilePath, "common passwords");
            CommonPasswords = new List<string>(File.ReadAllLines(_commonPasswordsFilePath));

            EnsureFileExistsAndPrintInfo(_userPasswordsFilePath, "user passwords");
            if (File.ReadAllLines(_userPasswordsFilePath).Length == 0)
            {
                Console.WriteLine(
                    "WARNING: No user passwords provided. The program will only attempt the common passwords. If there are no common passwords, no passwords will be attempted.");
            }

            EnsureFileExistsAndPrintInfo(_foundPasswordsFilePath, "previously found passwords");

            FoundPasswords =
                JsonConvert.DeserializeObject<ConcurrentBag<ArchivePasswordPair>>(
                    File.ReadAllText(_foundPasswordsFilePath)) ?? new ConcurrentBag<ArchivePasswordPair>();
        }

        private static void EnsureFileExistsAndPrintInfo(string filePath, string dataType)
        {
            EnsureFileExists(filePath);

            var dataCount = File.ReadAllLines(filePath).Length;
            Console.WriteLine($"Loaded {dataCount} {dataType}.");
        }

        private static void EnsureFileExists(string filePath)
        {
            if (File.Exists(filePath)) return;

            using (File.Create(filePath))
            {
            }

            Console.WriteLine($"WARNING: {filePath} file did not exist and was created.");
        }
        
        private static void LoadArchives(string pathToZipFiles)
        {
            try
            {
                var files = Directory.GetFiles(pathToZipFiles, "*.*", SearchOption.AllDirectories);

                Console.WriteLine($"Found {files.Length} files. Checking for protected archives...");

                Parallel.ForEach(files, file =>
                {
                    var ext = Path.GetExtension(file).ToLower();

                    if (!ArchiveStrategies.TryGetValue(ext, out var strategy) ||
                        !strategy.IsPasswordProtected(file) ||
                        FoundPasswords.Any(fp => fp.File == file)) return;

                    if (!ProtectedArchives.ContainsKey(strategy))
                    {
                        ProtectedArchives[strategy] = new ConcurrentBag<string>();
                    }

                    ProtectedArchives[strategy].Add(file);
                });

                Console.WriteLine($"Found {ProtectedArchives.Values.Sum(bag => bag.Count)} protected archives.");
            }
            catch (IOException ex)
            {
                Console.WriteLine($"An I/O error occurred while loading archives: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"Access to a file or directory was denied: {ex.Message}");
            }
        }

        private static void CheckPasswords()
        {
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };

            foreach (var (strategy, archives) in ProtectedArchives)
            {
                Parallel.ForEach(archives, parallelOptions, file =>
                {
                    Console.WriteLine($"Checking passwords for file: {file}");

                    if (!CheckCommonPasswords(strategy, file))
                    {
                        CheckUserPasswords(strategy, file);
                    }
                });
            }
        }

        private static bool CheckCommonPasswords(IArchiveStrategy strategy, string file)
        {
            foreach (var password in CommonPasswords.Where(password => strategy.IsPasswordCorrect(file, password)))
            {
                Console.WriteLine($"Found password in common passwords for file: {file}");
                AddPasswordAndSave(file, password);
                return true;
            }

            return false;
        }

        private static void CheckUserPasswords(IArchiveStrategy strategy, string file)
        {
            using var sr = new StreamReader(_userPasswordsFilePath);

            while (sr.ReadLine() is { } line)
            {
                if (!strategy.IsPasswordCorrect(file, line)) continue;

                Console.WriteLine($"Found password in user passwords for file: {file}");
                AddPasswordAndSave(file, line);

                // Check if it's already in common passwords
                if (!CommonPasswords.Contains(line))
                {
                    CommonPasswords.Add(line);
                    AppendToCommonPasswordsFile(line);
                }

                break;
            }
        }

        private static void AddPasswordAndSave(string file, string password)
        {
            FoundPasswords.Add(new ArchivePasswordPair { File = file, Password = password });
            SaveFoundPasswords();
        }

        private static void AppendToCommonPasswordsFile(string password)
        {
            FileOperationsQueue.Add(new FileOperation
            {
                Type = FileOperation.OperationType.AppendCommonPassword,
                Data = password
            });
        }

        private static void SaveFoundPasswords()
        {
            FileOperationsQueue.Add(new FileOperation
            {
                Type = FileOperation.OperationType.SaveFoundPasswords
            });
        }

        private static void FileOperationsWorker()
        {
            foreach (var op in FileOperationsQueue.GetConsumingEnumerable())
            {
                switch (op.Type)
                {
                    case FileOperation.OperationType.AppendCommonPassword:
                        if(op.Data != null)
                        {
                            PerformAppendToCommonPasswordsFile(op.Data);
                        }
                        else
                        {
                            Console.WriteLine("Attempted to append a null password to the common passwords file.");
                        }
                        break;
                    case FileOperation.OperationType.SaveFoundPasswords:
                        PerformSaveFoundPasswords();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        private static void PerformAppendToCommonPasswordsFile(string password)
        {
            try
            {
                using var sw = new StreamWriter(_commonPasswordsFilePath, true);
                sw.WriteLine(password);
            }
            catch (IOException ex)
            {
                Console.WriteLine($"An I/O error occurred while updating the common passwords file: {ex.Message}");
            }
        }
        
        private static void PerformSaveFoundPasswords()
        {
            try
            {
                var json = JsonConvert.SerializeObject(FoundPasswords, Formatting.Indented);
                File.WriteAllText(_foundPasswordsFilePath, json);
            }
            catch (IOException ex)
            {
                Console.WriteLine($"An I/O error occurred while saving found passwords: {ex.Message}");
            }
        }
    }
}