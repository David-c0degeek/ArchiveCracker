﻿using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using ArchiveCracker.Models;
using ArchiveCracker.Strategies;
using Serilog;

namespace ArchiveCracker.Services;

public class PasswordService
{
    private const int HardMaxSimultaneousArchivesToTry = 4;
    private readonly ConcurrentBag<string> _commonPasswords;
    private readonly ReadOnlyCollection<string> _userPasswords;
    private readonly ConcurrentBag<ArchivePasswordPair> _foundPasswords;
    private readonly FileService _fileService;

    private readonly ConcurrentDictionary<string, int> _attemptedPasswordsPerArchive = new();
    private readonly ConcurrentDictionary<string, int> _foundPasswordsPerArchive = new();
    private readonly ConcurrentDictionary<string, byte> _processedArchiveSet = new();
    
    private int _totalArchives;
    private int _processedArchives;

    private readonly TaskPool _taskPool;
    private int _activeArchives; // Number of archives currently being processed

    public PasswordService(ConcurrentBag<ArchivePasswordPair> foundPasswords, string userPasswordsFilePath,
        string commonPasswordsFilePath, int maxDegreeOfParallelism, FileService fileService)
    {
        _taskPool = new TaskPool(maxDegreeOfParallelism);
        _foundPasswords = foundPasswords;
        _fileService = fileService;

        _commonPasswords = new ConcurrentBag<string>(LoadPasswords(commonPasswordsFilePath));
        Log.Information("{Count} common passwords loaded", _commonPasswords.Count);

        _userPasswords = LoadPasswords(userPasswordsFilePath).ToList().AsReadOnly();
        Log.Information("{Count} user passwords loaded", _userPasswords.Count);

        var totalPasswords = _commonPasswords.Count + _userPasswords.Count;
        Log.Information("{Count} total passwords loaded", totalPasswords);

        _processedArchives = 0;
    }

    private static IEnumerable<string> LoadPasswords(string filePath)
    {
        if (!File.Exists(filePath)) return new List<string>();

        var passwords = File.ReadAllLines(filePath).ToList();
        return passwords;
    }

    private readonly object _consoleLock = new();
    
    private void PrintProgress()
    {
        lock (_consoleLock)
        {
            Console.WriteLine($"Total Archives: {_totalArchives}");
            Console.WriteLine($"Processed Archives: {_processedArchives}");
            foreach (var entry in _attemptedPasswordsPerArchive)
            {
                Console.WriteLine($"Archive: {entry.Key}, Attempted Passwords: {entry.Value}");
            }

            foreach (var entry in _foundPasswordsPerArchive)
            {
                Console.WriteLine($"Archive: {entry.Key}, Found Passwords: {entry.Value}");
            }
        }
    }

    private int CalculateMaxParallelArchives()
    {
        var maxCapacity = Math.Max(1, _taskPool.Capacity / 2);
        return maxCapacity > HardMaxSimultaneousArchivesToTry 
            ? HardMaxSimultaneousArchivesToTry
            : maxCapacity;
    }
    
    public async Task CheckMultipleArchivesWithQueue(
        ConcurrentDictionary<IArchiveStrategy, ConcurrentBag<string>> protectedArchives)
    {
        var maxParallelArchives = CalculateMaxParallelArchives();
    
        InitializeQueue(out var queue, protectedArchives);
        var producer = ProduceTasks(queue, protectedArchives);
        var consumers = ConsumeTasks(queue, maxParallelArchives);

        await producer;
        await Task.WhenAll(consumers);
    }

    private void InitializeQueue(out BlockingCollection<KeyValuePair<IArchiveStrategy, string>> queue,
        ConcurrentDictionary<IArchiveStrategy, ConcurrentBag<string>> protectedArchives)
    {
        _totalArchives = protectedArchives.Values.Sum(bag => bag.Count);
        Log.Information("Total archives to be processed: {Count}", _totalArchives);
        queue = new BlockingCollection<KeyValuePair<IArchiveStrategy, string>>(_taskPool.Capacity);
    }

    private static Task ProduceTasks(BlockingCollection<KeyValuePair<IArchiveStrategy, string>> queue,
        ConcurrentDictionary<IArchiveStrategy, ConcurrentBag<string>> protectedArchives)
    {
        return Task.Run(() =>
        {
            foreach (var (strategy, archives) in protectedArchives)
            {
                foreach (var archive in archives)
                {
                    queue.Add(new KeyValuePair<IArchiveStrategy, string>(strategy, archive));
                }
            }

            queue.CompleteAdding();
        });
    }

    private IEnumerable<Task> ConsumeTasks(BlockingCollection<KeyValuePair<IArchiveStrategy, string>> queue,
        int maxDegreeOfParallelism)
    {
        var consumers = new List<Task>();
        for (var i = 0; i < maxDegreeOfParallelism; i++)
        {
            var consumer = CreateConsumerTask(queue);
            consumers.Add(consumer);
        }

        return consumers;
    }

    private Task CreateConsumerTask(
        BlockingCollection<KeyValuePair<IArchiveStrategy, string>> queue)
    {
        return Task.Run(async () =>
        {
            foreach (var item in queue.GetConsumingEnumerable())
            {
                if (!_processedArchiveSet.TryAdd(item.Value, 0))
                {
                    Log.Information("Skipping already processed archive: {Archive}", item.Value);
                    continue;
                }

                await ProcessArchive(item);
            }
        });
    }

    private async Task ProcessArchive(KeyValuePair<IArchiveStrategy, string> item)
    {
        Log.Information("Processing started for archive: {Archive}", item.Value);
        
        Interlocked.Increment(ref _activeArchives);

        var subPoolSize = CalculateSubPoolSize();
        Log.Information("SubPool size calculated: {Size}", subPoolSize);

        var subTaskPool = new TaskPool(subPoolSize);

        try
        {
            // 1. Try common passwords
            Log.Information("Trying common passwords for archive: {Archive}", item.Value);
            var found = await CheckPasswordsAsync(item.Key, item.Value, _commonPasswords, subTaskPool);

            IEnumerable<string>? guessPasswords = null;
            // 2. Try guessed passwords
            if (!found)
            {
                Log.Information("Trying guessed passwords for archive: {Archive}", item.Value);
                guessPasswords = PasswordGuessService.GenerateGuessPasswords(item.Value);
                found = await CheckPasswordsAsync(item.Key, item.Value, guessPasswords, subTaskPool);
            }
            
            // 3. Try transformation rules
            if (!found && guessPasswords is not null)
            {
                Log.Information("Trying transformation rules on guessed passwords for archive: {Archive}", item.Value);
                var transformedGuesses = guessPasswords.SelectMany(PasswordGuessService.ApplyTransformationRules);
                found = await CheckPasswordsAsync(item.Key, item.Value, transformedGuesses, subTaskPool);
            }
            
            // 4. Try user-provided passwords
            if (!found)
            {
                Log.Information("Trying user passwords for archive: {Archive}", item.Value);
                await CheckPasswordsAsync(item.Key, item.Value, _userPasswords, subTaskPool);
            }
            
            // 5. Try transformations on user-provided passwords
            if (!found)
            {
                Log.Information("Trying transformation rules on user passwords for archive: {Archive}", item.Value);
                var transformedGuesses = _userPasswords.SelectMany(PasswordGuessService.ApplyTransformationRules);
                found = await CheckPasswordsAsync(item.Key, item.Value, transformedGuesses, subTaskPool);
            }

            if (!found)
            {
                Log.Information("Failed finding the password for archive: {Archive}", item.Value);
                _fileService.SaveNotFound(item.Value);
            }
        }
        catch (Exception ex)
        {
            Log.Error("An error occurred while processing archive {Archive}: {Message}", item.Value, ex.Message);
        }
        finally
        {
            Interlocked.Decrement(ref _activeArchives);
            Log.Information("Processing completed for archive: {Archive}", item.Value);
        }
    }

    private async Task<bool> CheckPasswordsAsync(IArchiveStrategy strategy, string file, IEnumerable<string> passwords,
        TaskPool subTaskPool)
    {
        var cancellationTokenSource = new CancellationTokenSource();
        const bool foundPassword = false;

        var tasks = passwords.Select(password => PerformPasswordCheckAsync(strategy, file, password, cancellationTokenSource, subTaskPool));

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            Log.Information("Operation was canceled because a password was found");
        }

        UpdateAndPrintProgress();

        return foundPassword;
    }

    private Task PerformPasswordCheckAsync(IArchiveStrategy strategy, string file, string password,
        CancellationTokenSource cancellationTokenSource, TaskPool subTaskPool)
    {
        return subTaskPool.Run(() =>
        {
            if (cancellationTokenSource.IsCancellationRequested) return Task.CompletedTask;

            IncrementAttemptedPasswordsForArchive(file);

            if (!strategy.IsPasswordCorrect(file, password)) return Task.CompletedTask;

            HandleFoundPassword(file, password, cancellationTokenSource);

            return Task.CompletedTask;
        });
    }

    private void HandleFoundPassword(string file, string password, CancellationTokenSource cancellationTokenSource)
    {
        var foundPasswordPair = new ArchivePasswordPair { File = file, Password = password };
        _foundPasswords.Add(foundPasswordPair);

        IncrementFoundPasswordsForArchive(file);
        _fileService.SaveFoundPassword(foundPasswordPair);

        if (!_commonPasswords.Contains(password))
        {
            _commonPasswords.Add(password);
            _fileService.AppendToCommonPasswordsFile(password);
        }

        cancellationTokenSource.Cancel();
    }

    private void UpdateAndPrintProgress()
    {
        Interlocked.Increment(ref _processedArchives);
        PrintProgress();
    }
    
    private int CalculateSubPoolSize()
    {
        var remainingSlots = _taskPool.Capacity - _activeArchives;
        var subPoolSize = remainingSlots / Math.Max(1, _activeArchives); // Math.Max to prevent division by zero
        return Math.Max(1, subPoolSize); // Ensure that subPoolSize is at least 1
    }

    private static void IncrementValueInConcurrentDictionary(
        ConcurrentDictionary<string, int> dictionary,
        string key)
    {
        dictionary.AddOrUpdate(key, 1, (_, oldValue) => oldValue + 1);
    }

    private void IncrementAttemptedPasswordsForArchive(string file)
    {
        IncrementValueInConcurrentDictionary(_attemptedPasswordsPerArchive, file);
    }

    private void IncrementFoundPasswordsForArchive(string file)
    {
        IncrementValueInConcurrentDictionary(_foundPasswordsPerArchive, file);
    }
}