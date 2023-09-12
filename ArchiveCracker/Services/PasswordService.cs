using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using ArchiveCracker.Models;
using ArchiveCracker.Strategies;
using Serilog;

namespace ArchiveCracker.Services;

public class PasswordService
{
    private readonly ReadOnlyCollection<string> _commonPasswords;
    private readonly ReadOnlyCollection<string> _userPasswords;
    private readonly ConcurrentBag<ArchivePasswordPair> _foundPasswords;

    private readonly ConcurrentDictionary<string, int> _attemptedPasswordsPerArchive = new();
    private readonly ConcurrentDictionary<string, int> _foundPasswordsPerArchive = new();
    private int _totalArchives;
    private int _processedArchives;

    private readonly TaskPool _taskPool;
    private int _activeArchives; // Number of archives currently being processed

    public PasswordService(ConcurrentBag<ArchivePasswordPair> foundPasswords, string userPasswordsFilePath,
        string commonPasswordsFilePath, int maxDegreeOfParallelism)
    {
        _taskPool = new TaskPool(maxDegreeOfParallelism);
        _foundPasswords = foundPasswords;

        _commonPasswords = LoadPasswords(commonPasswordsFilePath);
        Log.Information("{Count} common passwords loaded", _commonPasswords.Count);

        _userPasswords = LoadPasswords(userPasswordsFilePath);
        Log.Information("{Count} user passwords loaded", _userPasswords.Count);

        var totalPasswords = _commonPasswords.Count + _userPasswords.Count;
        Log.Information("{Count} total passwords loaded", totalPasswords);

        _processedArchives = 0;
    }

    private static ReadOnlyCollection<string> LoadPasswords(string filePath)
    {
        if (!File.Exists(filePath)) return new ReadOnlyCollection<string>(new List<string>());

        var passwords = File.ReadAllLines(filePath).ToList();
        return new ReadOnlyCollection<string>(passwords);
    }

    private void PrintProgress()
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

    public async Task CheckMultipleArchivesWithQueue(
        ConcurrentDictionary<IArchiveStrategy, ConcurrentBag<string>> protectedArchives, int maxDegreeOfParallelism)
    {
        InitializeQueue(out BlockingCollection<KeyValuePair<IArchiveStrategy, string>> queue, protectedArchives);
        var producer = ProduceTasks(queue, protectedArchives);
        var consumers = ConsumeTasks(queue, maxDegreeOfParallelism);

        await producer;
        await Task.WhenAll(consumers);
    }

    private void InitializeQueue(out BlockingCollection<KeyValuePair<IArchiveStrategy, string>> queue,
        ConcurrentDictionary<IArchiveStrategy, ConcurrentBag<string>> protectedArchives)
    {
        _totalArchives = protectedArchives.Count;
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

    private Task CreateConsumerTask(BlockingCollection<KeyValuePair<IArchiveStrategy, string>> queue)
    {
        return Task.Run(async () =>
        {
            foreach (var item in queue.GetConsumingEnumerable())
            {
                await ProcessArchive(item);
            }
        });
    }

    private async Task ProcessArchive(KeyValuePair<IArchiveStrategy, string> item)
    {
        try
        {
            Interlocked.Increment(ref _activeArchives);

            var subPoolSize = CalculateSubPoolSize();
            var subTaskPool = new TaskPool(subPoolSize);

            var found = await CheckPasswordsAsync(item.Key, item.Value, _commonPasswords, subTaskPool);
            if (!found)
            {
                await CheckPasswordsAsync(item.Key, item.Value, _userPasswords, subTaskPool);
            }
        }
        catch (Exception ex)
        {
            Log.Error("An error occurred: {Message}", ex.Message);
        }
        finally
        {
            Interlocked.Decrement(ref _activeArchives);
        }
    }

    private int CalculateSubPoolSize()
    {
        var remainingSlots = _taskPool.Capacity - _activeArchives;
        return remainingSlots / Math.Max(1, _activeArchives); // Math.Max to prevent division by zero
    }

    private async Task<bool> CheckPasswordsAsync(IArchiveStrategy strategy, string file, IEnumerable<string> passwords,
        TaskPool subTaskPool)
    {
        var cancellationTokenSource = new CancellationTokenSource();
        var foundPassword = false;

        var tasks = passwords.Select(password => subTaskPool.Run(() =>
        {
            if (cancellationTokenSource.IsCancellationRequested) return Task.CompletedTask;

            IncrementAttemptedPasswordsForArchive(file);

            if (!strategy.IsPasswordCorrect(file, password)) return Task.CompletedTask;

            _foundPasswords.Add(new ArchivePasswordPair { File = file, Password = password });
            IncrementFoundPasswordsForArchive(file);
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

        Interlocked.Increment(ref _processedArchives);
        PrintProgress();

        return foundPassword;
    }


    private static void IncrementValueInConcurrentDictionary(ConcurrentDictionary<string, int> dictionary, string key)
    {
        var newValue = 0;
        dictionary.AddOrUpdate(
            key,
            (_) => Interlocked.Increment(ref newValue),
            (_, oldValue) => Interlocked.Increment(ref oldValue)
        );
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