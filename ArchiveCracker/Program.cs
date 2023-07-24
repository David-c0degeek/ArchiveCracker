using System.Collections.Concurrent;
using System.Text;
using ArchiveCracker.Models;
using ArchiveCracker.Services;
using ArchiveCracker.Strategies;
using CommandLine;
using Serilog;

namespace ArchiveCracker;

public abstract class Program
{
    private static readonly BlockingCollection<FileOperation> FileOperationsQueue =
        new(new ConcurrentQueue<FileOperation>());

    private static readonly ConcurrentBag<ArchivePasswordPair> FoundPasswords = new();
    private static readonly ConcurrentDictionary<IArchiveStrategy, ConcurrentBag<string>> ProtectedArchives = new();
    private static readonly CancellationTokenSource CancellationTokenSource = new();
    
    private const string CommonPasswordsFileName = "common_passwords.txt";
    private const string UserPasswordsFileName = "user_passwords.txt";
    private const string FoundPasswordsFileName = "found_passwords.txt";

    private static FileService? _fileService;
    private static PasswordService? _passwordService;
    private static ArchiveService? _archiveService;
    private static string? _zipPath;
    private static string? _userPasswordsPath;
    private static string? _commonPasswordsPath;
    private static string? _foundPasswordsPath;

    public static void Main(string[] args)
    {
        InitLogger();

        Parser.Default.ParseArguments<Options>(args)
            .WithParsed(SetupPaths)
            .WithParsed(CreateServices)
            .WithParsed(StartOperations)
            .WithNotParsed(errors => 
            {
                foreach(var error in errors)
                {
                    Log.Error(error.ToString() ?? "Unknown error");
                }
            });

        Cleanup();
    }

private static void SetupPaths(Options options)
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
}

private static void CreateServices(Options options)
{
    _fileService = new FileService(
        _commonPasswordsPath!,
        _userPasswordsPath!,
        _foundPasswordsPath!,
        FileOperationsQueue);
    
    _passwordService = new PasswordService(
        FoundPasswords,
        _userPasswordsPath!,
        _fileService,
        _commonPasswordsPath!);
    
    _archiveService = new ArchiveService(
        ProtectedArchives, 
        FoundPasswords);
}


    private static void StartOperations(Options options)
    {
        // Start the File Operations worker
        Task.Factory.StartNew(() =>
        {
            try
            {
                _fileService!.FileOperationsWorker(CancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred in FileOperationsWorker");
            }
        }, TaskCreationOptions.LongRunning);

        // Load archives
        _archiveService!.LoadArchives(_zipPath!);

        // Check Passwords
        _archiveService.CheckPasswords(_passwordService!);
    }

    private static void Cleanup()
    {
        Log.CloseAndFlush();
        CancellationTokenSource.Cancel();
    }

    private static void InitLogger()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();
    }
}