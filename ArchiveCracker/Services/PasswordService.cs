using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using ArchiveCracker.Models;
using ArchiveCracker.Strategies;
using Serilog;

namespace ArchiveCracker.Services;

public class PasswordService
{
    private readonly FileService _fileService;
    private readonly ReadOnlyCollection<string> _commonPasswords;
    private readonly ReadOnlyCollection<string> _userPasswords;
    private readonly ConcurrentBag<ArchivePasswordPair> _foundPasswords;

    private readonly int _totalPasswords;
    private int _attemptedPasswords;
    private readonly TaskPool _taskPool;

    public PasswordService(ConcurrentBag<ArchivePasswordPair> foundPasswords, string userPasswordsFilePath,
        FileService fileService, string commonPasswordsFilePath, int maxDegreeOfParallelism)
    {
    
        _taskPool = new TaskPool(maxDegreeOfParallelism);
        _foundPasswords = foundPasswords;
        _fileService = fileService;

        // Initialize CommonPasswords list
        _commonPasswords = LoadPasswords(commonPasswordsFilePath);
        Log.Information("{Count} common passwords loaded", _commonPasswords.Count);

        // Load UserPasswords list into memory
        _userPasswords = LoadPasswords(userPasswordsFilePath);
        Log.Information("{Count} user passwords loaded", _userPasswords.Count);

        _totalPasswords = _commonPasswords.Count + _userPasswords.Count;
        Log.Information("{Count} total passwords loaded", _totalPasswords);
    }

    private static ReadOnlyCollection<string> LoadPasswords(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                var passwords = File.ReadAllLines(filePath).ToList();
                Log.Information("{Count} passwords loaded from file {FilePath}", passwords.Count, filePath);
                return new ReadOnlyCollection<string>(passwords);
            }

            Log.Information("No passwords loaded from file {FilePath} because the file does not exist", filePath);
            return new ReadOnlyCollection<string>(new List<string>());
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load passwords from file {FilePath}", filePath);
            return new ReadOnlyCollection<string>(new List<string>());
        }
    }

    private async Task<bool> CheckPasswordsAsync(IArchiveStrategy strategy, string file, IReadOnlyCollection<string> passwords)
    {
        var cancellationTokenSource = new CancellationTokenSource();
        var foundPassword = false;

        var tasks = passwords.Select(password => _taskPool.Run(() =>
        {
            if (cancellationTokenSource.IsCancellationRequested)
            {
                return Task.CompletedTask;
            }

            Interlocked.Increment(ref _attemptedPasswords);
            // Log.Information("Passwords attempted: {AttemptedPasswords}. Remaining: {RemainingPasswords}",
            //     _attemptedPasswords, _totalPasswords - _attemptedPasswords);

            Console.Write("\rPasswords attempted: {0}. Remaining: {1}. Current password: {2}", _attemptedPasswords, _totalPasswords - _attemptedPasswords, password);
            
            if (!strategy.IsPasswordCorrect(file, password)) return Task.CompletedTask;

            Log.Information("Found password for file: {File}", file);
            AddPasswordAndSave(file, password);

            if (passwords.Equals(_userPasswords) && !_commonPasswords.Contains(password))
            {
                _fileService.AppendToCommonPasswordsFile(password);
            }

            foundPassword = true;
            cancellationTokenSource.Cancel();
            return Task.CompletedTask;
        }));

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            Log.Information("Operation was canceled because a password was found");
        }

        return foundPassword;
    }
    
    private void AddPasswordAndSave(string file, string password)
    {
        var archivePasswordPair = new ArchivePasswordPair { File = file, Password = password };
        _foundPasswords.Add(archivePasswordPair);
        Log.Information("Password {Password} for archive {File} was added to FoundPasswords", password, file);
        _fileService.SaveFoundPassword(archivePasswordPair);
    }

    public async Task CheckPasswordsAsync(
        ConcurrentDictionary<IArchiveStrategy, ConcurrentBag<string>> protectedArchives)
    {
        var stopwatch = new Stopwatch();
        stopwatch.Start();

        var tasks = new List<Task>();

        foreach (var (strategy, archives) in protectedArchives)
        {
            tasks.AddRange(archives.Select(archive => _taskPool.Run(async () =>
            {
                Log.Information("Checking passwords for file: {File}", archive);

                if (await CheckPasswordsAsync(strategy, archive, _commonPasswords)) return;

                Log.Information("No common password found, trying user passwords...");
                await CheckPasswordsAsync(strategy, archive, _userPasswords);
            })));
        }

        await Task.WhenAll(tasks);

        stopwatch.Stop();
        Log.Information("Checking passwords took {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);
    }
}