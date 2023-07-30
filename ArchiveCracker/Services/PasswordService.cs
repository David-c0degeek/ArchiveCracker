using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using ArchiveCracker.Models;
using ArchiveCracker.Strategies;
using Serilog;

namespace ArchiveCracker.Services;

public class PasswordService
{
    private readonly FileService _fileService;
    private readonly ReadOnlyCollection<string> _commonPasswords;
    private readonly ConcurrentBag<ArchivePasswordPair> _foundPasswords;
    private readonly string _userPasswordsFilePath;
    private readonly int _maxDegreeOfParallelism;

    public PasswordService(ConcurrentBag<ArchivePasswordPair> foundPasswords, string userPasswordsFilePath, FileService fileService, string commonPasswordsFilePath)
    {
        _foundPasswords = foundPasswords;
        _userPasswordsFilePath = userPasswordsFilePath;
        _fileService = fileService;

        // Initialize CommonPasswords list
        _commonPasswords = File.Exists(commonPasswordsFilePath)
            ? new ReadOnlyCollection<string>(File.ReadAllLines(commonPasswordsFilePath).ToList())
            : new ReadOnlyCollection<string>(new List<string>());
        
        // Determine MaxDegreeOfParallelism based on processor count.
        var processorCount = Environment.ProcessorCount;
        _maxDegreeOfParallelism = processorCount <= 3 ? 1 : processorCount <= 20 ? processorCount - 2 : processorCount - 4;
    }
        
public bool CheckCommonPasswords(IArchiveStrategy strategy, string file)
{
    var cancellationTokenSource = new CancellationTokenSource();
    var foundPassword = false;

    try
    {
        Parallel.ForEach(_commonPasswords, new ParallelOptions { CancellationToken = cancellationTokenSource.Token , MaxDegreeOfParallelism = _maxDegreeOfParallelism}, (password, state) =>
        {
            if (cancellationTokenSource.IsCancellationRequested)
            {
                state.Stop();
                return;
            }

            if (!strategy.IsPasswordCorrect(file, password)) return;

            Log.Information("Found password in common passwords for file: {File}", file);
            AddPasswordAndSave(file, password);
            foundPassword = true;
            
            cancellationTokenSource.Cancel();
            state.Stop();
        });
    }
    catch (OperationCanceledException)
    {
        Log.Information("Operation was canceled because a password was found");
    }

    return foundPassword;
}

public void CheckUserPasswords(IArchiveStrategy strategy, string file)
{
    var cancellationTokenSource = new CancellationTokenSource();
    var userPasswords = File.ReadAllLines(_userPasswordsFilePath);

    try
    {
        Parallel.ForEach(userPasswords, new ParallelOptions { CancellationToken = cancellationTokenSource.Token , MaxDegreeOfParallelism = _maxDegreeOfParallelism}, (line, state) =>
        {
            if (cancellationTokenSource.IsCancellationRequested)
            {
                state.Stop();
                return;
            }

            if (!strategy.IsPasswordCorrect(file, line)) return;

            Log.Information("Found password in user passwords for file: {File}", file);
            AddPasswordAndSave(file, line);

            // Check if it's already in common passwords
            if (!_commonPasswords.Contains(line))
            {
                _fileService.AppendToCommonPasswordsFile(line);
            }

            cancellationTokenSource.Cancel();
            state.Stop();
        });
    }
    catch (OperationCanceledException)
    {
        Log.Information("Operation was canceled because a password was found");
    }
}

    
    private void AddPasswordAndSave(string file, string password)
    {
        var archivePasswordPair = new ArchivePasswordPair { File = file, Password = password };
        _foundPasswords.Add(archivePasswordPair);
        Log.Information("Password {Password} for archive {File} was added to FoundPasswords", password, file);
        _fileService.SaveFoundPassword(archivePasswordPair);
    }
}
