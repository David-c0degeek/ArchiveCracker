using System.Collections.Concurrent;
using System.Text;
using ArchiveCracker.Models;
using ArchiveCracker.Services;
using ArchiveCracker.Strategies;
using CommandLine;
using Serilog;

namespace ArchiveCracker;

public static class Program
{
    private static readonly BlockingCollection<FileOperation> FileOperationsQueue =
        new(new ConcurrentQueue<FileOperation>());

    private static readonly ConcurrentBag<ArchivePasswordPair> FoundPasswords = new();
    private static ConcurrentDictionary<IArchiveStrategy, ConcurrentBag<string>> _protectedArchives = null!;
    private static readonly CancellationTokenSource CancellationTokenSource = new();

    private const string CommonPasswordsFileName = "common_passwords.txt";
    private const string UserPasswordsFileName = "user_passwords.txt";
    private const string FoundPasswordsFileName = "found_passwords.txt";

    private static FileService _fileService = null!;
    private static PasswordService _passwordService = null!;
    private static ArchiveService _archiveService = null!;
    private static string? _zipPath;
    private static string? _userPasswordsPath;
    private static string? _commonPasswordsPath;
    private static string? _foundPasswordsPath;

    public static async Task Main(string[] args)
    {
        InitLogger();

        try
        {
            var result = Parser.Default.ParseArguments<Options>(args);

            result.WithNotParsed(errors =>
            {
                foreach (var error in errors)
                {
                    Log.Error("An error occurred: {ErrorMessage}", error.ToString() ?? "Unknown error");
                }

                Environment.Exit(1);
            });

            var options = ((Parsed<Options>)result).Value;

            await SetupPaths(options);
            CreateServices();
            await StartOperations();

            Cleanup();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred in the Main method");
        }
    }

    private static Task SetupPaths(Options options)
    {
        _zipPath = string.IsNullOrWhiteSpace(options.PathToZipFiles)
            ? Environment.CurrentDirectory
            : options.PathToZipFiles;

        _userPasswordsPath = string.IsNullOrWhiteSpace(options.UserPasswordsFilePath)
            ? Path.Combine(Environment.CurrentDirectory, UserPasswordsFileName)
            : options.UserPasswordsFilePath;

        _commonPasswordsPath = string.IsNullOrWhiteSpace(options.CommonPasswordsFilePath)
            ? Path.Combine(Environment.CurrentDirectory, CommonPasswordsFileName)
            : options.CommonPasswordsFilePath;

        _foundPasswordsPath = string.IsNullOrWhiteSpace(options.FoundPasswordsFilePath)
            ? Path.Combine(Environment.CurrentDirectory, FoundPasswordsFileName)
            : options.FoundPasswordsFilePath;

        var errorMessage = new StringBuilder();

        if (_zipPath == null) errorMessage.AppendLine("Failed to determine the zip path");
        if (_userPasswordsPath == null) errorMessage.AppendLine("Failed to determine the user passwords path");
        if (_commonPasswordsPath == null) errorMessage.AppendLine("Failed to determine the common passwords path");
        if (_foundPasswordsPath == null) errorMessage.AppendLine("Failed to determine the found passwords path");

        if (errorMessage.Length > 0) throw new Exception(errorMessage.ToString());

        return Task.CompletedTask;
    }

    private static void CreateServices()
    {
        _fileService = new FileService(
            _commonPasswordsPath!,
            _userPasswordsPath!,
            _foundPasswordsPath!,
            FileOperationsQueue);

        _passwordService = new PasswordService(
            FoundPasswords,
            _userPasswordsPath!,
            _commonPasswordsPath!,
            GetMaxDegreeOfParallelism(),
            _fileService);

        _archiveService = new ArchiveService(
            FoundPasswords);
    }

    private static async Task StartOperations()
    {
        // Start the File Operations worker
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        Task.Factory.StartNew(() =>
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        {
            try
            {
                _fileService.FileOperationsWorker(CancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred in FileOperationsWorker");
            }
        }, TaskCreationOptions.LongRunning);

        // Load archives
        _protectedArchives = await _archiveService.LoadArchivesAsync(_zipPath!);

        // Check Passwords
        await _passwordService.CheckMultipleArchivesWithQueue(_protectedArchives);

    }

    private static void Cleanup()
    {
        Log.CloseAndFlush();
        CancellationTokenSource.Cancel();
    }

    private static void InitLogger()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .CreateLogger();
    }
    
    private static int GetMaxDegreeOfParallelism()
    {
        var processorCount = Environment.ProcessorCount;
        var maxDegreeOfParallelism =processorCount <= 3 ? 1 : processorCount <= 20 ? processorCount - 2 : processorCount - 4;
        
        Log.Information("Total threads in machine: {ProcessorCount}, _maxDegreeOfParallelism: {MaxDegreeOfParallelism}", processorCount, maxDegreeOfParallelism);
        
        return maxDegreeOfParallelism;
    }
}
