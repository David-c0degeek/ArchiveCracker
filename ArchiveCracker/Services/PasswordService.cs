using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using ArchiveCracker.Models;
using ArchiveCracker.Strategies;
using Serilog;

namespace ArchiveCracker.Services
{
    public class PasswordService
    {
        private readonly FileService _fileService;
        private readonly ReadOnlyCollection<string> _commonPasswords;
        private readonly ReadOnlyCollection<string> _userPasswords;
        private readonly ConcurrentBag<ArchivePasswordPair> _foundPasswords;
        private readonly int _maxDegreeOfParallelism;

        private readonly int _totalPasswords;
        private int _attemptedPasswords;

        public PasswordService(ConcurrentBag<ArchivePasswordPair> foundPasswords, string userPasswordsFilePath,
            FileService fileService, string commonPasswordsFilePath)
        {
            _foundPasswords = foundPasswords;
            _fileService = fileService;

            // Initialize CommonPasswords list
            _commonPasswords = LoadPasswords(commonPasswordsFilePath);

            // Load UserPasswords list into memory
            _userPasswords = LoadPasswords(userPasswordsFilePath);

            // Determine MaxDegreeOfParallelism based on processor count.
            var processorCount = Environment.ProcessorCount;
            _maxDegreeOfParallelism =
                processorCount <= 3 ? 1 : processorCount <= 20 ? processorCount - 2 : processorCount - 4;
            
            Log.Information("Total threads in machine: {ProcessorCount}, _maxDegreeOfParallelism: {MaxDegreeOfParallelism}", processorCount, _maxDegreeOfParallelism);
            
            _totalPasswords = _commonPasswords.Count + _userPasswords.Count;
        }

        private static ReadOnlyCollection<string> LoadPasswords(string filePath)
        {
            try
            {
                return File.Exists(filePath)
                    ? new ReadOnlyCollection<string>(File.ReadAllLines(filePath).ToList())
                    : new ReadOnlyCollection<string>(new List<string>());
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load passwords from file {FilePath}", filePath);
                return new ReadOnlyCollection<string>(new List<string>());
            }
        }

        public bool CheckCommonPasswords(IArchiveStrategy strategy, string file)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var foundPassword = false;

            try
            {
                Parallel.ForEach(_commonPasswords,
                    new ParallelOptions
                    {
                        CancellationToken = cancellationTokenSource.Token,
                        MaxDegreeOfParallelism = _maxDegreeOfParallelism
                    }, (password, state) =>
                    {
                        if (cancellationTokenSource.IsCancellationRequested)
                        {
                            state.Stop();
                            return;
                        }

                        Interlocked.Increment(ref _attemptedPasswords);
                        Log.Information("Passwords attempted: {AttemptedPasswords}. Remaining: {RemainingPasswords}",
                            _attemptedPasswords, _totalPasswords - _attemptedPasswords);

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

            try
            {
                Parallel.ForEach(_userPasswords,
                    new ParallelOptions
                    {
                        CancellationToken = cancellationTokenSource.Token,
                        MaxDegreeOfParallelism = _maxDegreeOfParallelism
                    }, (password, state) =>
                    {
                        if (cancellationTokenSource.IsCancellationRequested)
                        {
                            state.Stop();
                            return;
                        }

                        Interlocked.Increment(ref _attemptedPasswords);
                        Log.Information("Passwords attempted: {AttemptedPasswords}. Remaining: {RemainingPasswords}",
                            _attemptedPasswords, _totalPasswords - _attemptedPasswords);

                        if (!strategy.IsPasswordCorrect(file, password)) return;

                        Log.Information("Found password in user passwords for file: {File}", file);
                        AddPasswordAndSave(file, password);

                        // Check if it's already in common passwords
                        if (!_commonPasswords.Contains(password))
                        {
                            _fileService.AppendToCommonPasswordsFile(password);
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
}