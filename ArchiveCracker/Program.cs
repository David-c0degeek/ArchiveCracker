using System.Collections.Concurrent;
using ArchiveCracker.Services;
using ArchiveCracker.Models;
using ArchiveCracker.Strategies;
using CommandLine;
using Serilog;

namespace ArchiveCracker
{
    public abstract class Program
    {
        private const string CommonPasswordsFilePath = "common_passwords.txt";
        private const string UserPasswordsFilePath = "user_passwords.txt";
        private const string FoundPasswordsFilePath = "found_passwords.txt";
        private static readonly BlockingCollection<FileOperation> FileOperationsQueue = new(new ConcurrentQueue<FileOperation>());
        private static readonly ConcurrentBag<ArchivePasswordPair> FoundPasswords = new();
        private static readonly ConcurrentDictionary<IArchiveStrategy, ConcurrentBag<string>> ProtectedArchives = new();
        
        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .CreateLogger();
            
            var fileService = new FileService(CommonPasswordsFilePath, FoundPasswordsFilePath, FileOperationsQueue);
            var passwordService = new PasswordService(FoundPasswords, UserPasswordsFilePath, fileService, CommonPasswordsFilePath);
            var archiveService = new ArchiveService(ProtectedArchives, FoundPasswords);

            var cts = new CancellationTokenSource();
            
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(options =>
                {
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
                    archiveService.LoadArchives(options.PathToZipFiles);

                    // Check Passwords
                    archiveService.CheckPasswords(passwordService);
                });
                
            // Make sure to flush and close logger to ensure all messages are written
            Log.CloseAndFlush();
            cts.Cancel();
        }
    }
}
