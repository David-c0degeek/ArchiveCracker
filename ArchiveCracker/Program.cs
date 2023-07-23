using System.Collections.Concurrent;
using Newtonsoft.Json;

namespace ArchiveCracker;

internal abstract class Program
{
    private static List<string>? CommonPasswords { get; set; }
    private const string UserPasswordsFilePath = "user_passwords.txt";
    private const string CommonPasswordsFilePath = "common_passwords.txt";
    private const string FoundPasswordsFilePath = "found_passwords.json";

    private static ConcurrentBag<(string, string)> FoundPasswords { get; set; } = new();

    private static readonly Dictionary<string, IArchiveStrategy> ArchiveStrategies;
    private static readonly ConcurrentDictionary<IArchiveStrategy, ConcurrentBag<string>> ProtectedArchives = new();

    static Program()
    {
        ArchiveStrategies = new Dictionary<string, IArchiveStrategy>
        {
            { ".rar", new RarArchiveStrategy() },
            { ".7z", new SevenZipArchiveStrategy() },
            { ".zip", new ZipArchiveStrategy() },
            { ".001", new SevenZipArchiveStrategy() } // Checking password protection on first volume
        };

        if (File.Exists(FoundPasswordsFilePath))
        {
            FoundPasswords =
                JsonConvert.DeserializeObject<ConcurrentBag<(string, string)>>(
                    File.ReadAllText(FoundPasswordsFilePath)) ?? new ConcurrentBag<(string, string)>();
        }
    }

    private static void Main()
    {
        Init();
        LoadArchives();
        CheckPasswords();
    }

    private static void Init()
    {
        if (!File.Exists(CommonPasswordsFilePath))
        {
            File.Create(CommonPasswordsFilePath).Close();
            CommonPasswords = new List<string>();
        }
        else
        {
            CommonPasswords = new List<string>(File.ReadAllLines(CommonPasswordsFilePath));
        }
    }

    private static void LoadArchives()
    {
        try
        {
            var files = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.*", SearchOption.AllDirectories);

            Parallel.ForEach(files, file =>
            {
                var ext = Path.GetExtension(file).ToLower();

                // If the file extension does not correspond to an archive or the file isn't password protected or the password has already been found, skip it
                if (!ArchiveStrategies.TryGetValue(ext, out var strategy) || 
                    !strategy.IsPasswordProtected(file) || 
                    FoundPasswords.Any(fp => fp.Item1 == file))
                {
                    return;
                }

                if (!ProtectedArchives.ContainsKey(strategy))
                {
                    ProtectedArchives[strategy] = new ConcurrentBag<string>();
                }

                ProtectedArchives[strategy].Add(file);
            });
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
                TryUnlockArchive(strategy, file);
            });
        }
    }

    private static void TryUnlockArchive(IArchiveStrategy strategy, string file)
    {
        if (CommonPasswords != null && CheckCommonPasswords(strategy, file)) return;
        CheckUserPasswords(strategy, file);
    }

    private static bool CheckCommonPasswords(IArchiveStrategy strategy, string file)
    {
        if (CommonPasswords == null) return false;

        foreach (var password in CommonPasswords.Where(password => strategy.IsPasswordCorrect(file, password)))
        {
            AddPasswordAndSave(file, password);
            return true;
        }

        return false;
    }


    private static void CheckUserPasswords(IArchiveStrategy strategy, string file)
    {
        if (!File.Exists(UserPasswordsFilePath)) return;

        using var sr = new StreamReader(UserPasswordsFilePath);

        while (sr.ReadLine() is { } line)
        {
            if (!strategy.IsPasswordCorrect(file, line)) continue;

            AddPasswordAndSave(file, line);

            // Check if it's already in common passwords
            if (CommonPasswords != null && !CommonPasswords.Contains(line))
            {
                CommonPasswords.Add(line);
                AppendToCommonPasswordsFile(line);
            }

            break;
        }
    }

    private static void AddPasswordAndSave(string file, string password)
    {
        FoundPasswords.Add((file, password));
        SaveFoundPasswords();
    }

    private static void AppendToCommonPasswordsFile(string password)
    {
        try
        {
            using var sw = new StreamWriter(CommonPasswordsFilePath, true);
            sw.WriteLine(password);
        }
        catch (IOException ex)
        {
            Console.WriteLine($"An I/O error occurred while updating the common passwords file: {ex.Message}");
        }
    }

    private static void SaveFoundPasswords()
    {
        try
        {
            var json = JsonConvert.SerializeObject(FoundPasswords, Formatting.Indented);
            File.WriteAllText(FoundPasswordsFilePath, json);
        }
        catch (IOException ex)
        {
            Console.WriteLine($"An I/O error occurred while saving found passwords: {ex.Message}");
        }
    }
}