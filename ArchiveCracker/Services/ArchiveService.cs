using System.Collections.Concurrent;
using ArchiveCracker.Models;
using ArchiveCracker.Strategies;
using Serilog;

namespace ArchiveCracker.Services;

public class ArchiveService
{
    private readonly ConcurrentDictionary<IArchiveStrategy, ConcurrentBag<string>> _protectedArchives;
    private readonly ConcurrentBag<ArchivePasswordPair> _foundPasswords;

    private readonly Dictionary<string, IArchiveStrategy> _archiveStrategies = new()
    {
        { ".rar", new RarArchiveStrategy() },
        { ".7z", new SevenZipArchiveStrategy() },
        { ".zip", new ZipArchiveStrategy() },
        { ".001", new SevenZipArchiveStrategy() } // Checking password protection on first volume
    };

    public ArchiveService(ConcurrentDictionary<IArchiveStrategy, ConcurrentBag<string>> protectedArchives,
        ConcurrentBag<ArchivePasswordPair> foundPasswords)
    {
        _protectedArchives = protectedArchives;
        _foundPasswords = foundPasswords;
    }

    public async Task LoadArchivesAsync(string pathToZipFiles)
    {
        try
        {
            var files = Directory.GetFiles(pathToZipFiles, "*.*", SearchOption.AllDirectories);

            Log.Information("Found {FileCount} files. Checking for protected archives...", files.Length);

            var tasks = files.Select(file =>
            {
                var ext = Path.GetExtension(file).ToLower();

                if (!_archiveStrategies.TryGetValue(ext, out var strategy) ||
                    !strategy.IsPasswordProtected(file) ||
                    _foundPasswords.Any(fp => fp.File == file)) return Task.CompletedTask;

                if (!_protectedArchives.ContainsKey(strategy))
                {
                    _protectedArchives[strategy] = new ConcurrentBag<string>();
                }

                _protectedArchives[strategy].Add(file);
                Log.Information("Password protected archive found: {File}", file);
                return Task.CompletedTask;
            });

            await Task.WhenAll(tasks);

            Log.Information("Found {ArchiveCount} protected archives",
                _protectedArchives.Values.Sum(bag => bag.Count));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while loading archives");
        }
    }
    
    public async Task CheckPasswordsAsync(PasswordService passwordService)
    {
        foreach (var (strategy, archives) in _protectedArchives)
        {
            var tasks = archives.Select(file =>
            {
                Log.Information("Checking passwords for file: {File}", file);

                if (passwordService.CheckCommonPasswords(strategy, file)) return Task.CompletedTask;
            
                Log.Information("No common password found, trying user passwords...");
                passwordService.CheckUserPasswords(strategy, file);
                return Task.CompletedTask;
            });

            await Task.WhenAll(tasks);
        }
    }

}