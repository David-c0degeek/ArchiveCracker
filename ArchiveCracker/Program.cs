using System.Collections.Concurrent;
using ArchiveCracker.Services;
using ArchiveCracker.Models;
using ArchiveCracker.Strategies;
using CommandLine;
using Serilog;

namespace ArchiveCracker;

public abstract class Program
{
    private const string CommonPasswordsFilePath = "common_passwords.txt";
    private const string UserPasswordsFilePath = "user_passwords.txt";
    private const string FoundPasswordsFilePath = "found_passwords.txt";

    private static readonly BlockingCollection<FileOperation> FileOperationsQueue =
        new(new ConcurrentQueue<FileOperation>());

    private static readonly ConcurrentBag<ArchivePasswordPair> FoundPasswords = new();
    private static readonly ConcurrentDictionary<IArchiveStrategy, ConcurrentBag<string>> ProtectedArchives = new();

    public static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();

        var cts = new CancellationTokenSource();

        Parser.Default.ParseArguments<Options>(args)
            .WithParsed(options =>
            {
                // Use command-line options if provided, otherwise use current directory
                var zipPath = string.IsNullOrWhiteSpace(options.PathToZipFiles)
                    ? Environment.CurrentDirectory
                    : options.PathToZipFiles;
                var userPasswordsPath = string.IsNullOrWhiteSpace(options.UserPasswordsFilePath)
                    ? Environment.CurrentDirectory
                    : options.UserPasswordsFilePath;

                var fileService = new FileService(Path.Combine(userPasswordsPath, CommonPasswordsFilePath),
                    Path.Combine(userPasswordsPath, FoundPasswordsFilePath),
                    FileOperationsQueue);
                var passwordService = new PasswordService(FoundPasswords,
                    Path.Combine(userPasswordsPath, UserPasswordsFilePath),
                    fileService,
                    Path.Combine(userPasswordsPath, CommonPasswordsFilePath));
                var archiveService = new ArchiveService(ProtectedArchives, FoundPasswords);

                // Start the File Operations worker
                Task.Factory.StartNew(() =>
                {
                    try
                    {
                        fileService.FileOperationsWorker(cts.Token);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "An error occurred in FileOperationsWorker");
                    }
                }, TaskCreationOptions.LongRunning);

                // Load archives
                archiveService.LoadArchives(zipPath);

                // Check Passwords
                archiveService.CheckPasswords(passwordService);
            });

        // Make sure to flush and close logger to ensure all messages are written
        Log.CloseAndFlush();
        cts.Cancel();
    }
}