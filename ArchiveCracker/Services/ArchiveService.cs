using System.Collections.Concurrent;
using System.Diagnostics;
using ArchiveCracker.Models;
using ArchiveCracker.Strategies;
using Serilog;

namespace ArchiveCracker.Services
{
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
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            try
            {
                var files = Directory.GetFiles(pathToZipFiles, "*.*", SearchOption.AllDirectories);

                Log.Information("Found {FileCount} files. Checking for protected archives...", files.Length);

                await Task.WhenAll(files.Select(LoadArchiveAsync));

                Log.Information("Found {ArchiveCount} protected archives",
                    _protectedArchives.Values.Sum(bag => bag.Count));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while loading archives");
            }

            stopwatch.Stop();
            Log.Information("Loading archives took {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);
        }

        private async Task LoadArchiveAsync(string file)
        {
            var ext = Path.GetExtension(file).ToLower();

            if (!_archiveStrategies.TryGetValue(ext, out var strategy) ||
                !await Task.Run(() => strategy.IsPasswordProtected(file)) ||
                _foundPasswords.Any(fp => fp.File == file)) return;

            if (!_protectedArchives.ContainsKey(strategy))
            {
                _protectedArchives[strategy] = new ConcurrentBag<string>();
            }

            _protectedArchives[strategy].Add(file);
            Log.Information("Password protected archive found: {File}", file);
        }

        public async Task CheckPasswordsAsync(PasswordService passwordService)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            foreach (var (strategy, archives) in _protectedArchives)
            {
                await Task.WhenAll(archives.Select(async file =>
                {
                    Log.Information("Checking passwords for file: {File}", file);

                    if (passwordService.CheckCommonPasswords(strategy, file)) return;
                
                    Log.Information("No common password found, trying user passwords...");
                    passwordService.CheckUserPasswords(strategy, file);
                }));
            }

            stopwatch.Stop();
            Log.Information("Checking passwords took {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);
        }
    }
}
