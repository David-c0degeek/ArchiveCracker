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
        private static List<string> _commonPasswords = new();
        
        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .CreateLogger();
            
            var fileService = new FileService(CommonPasswordsFilePath, FoundPasswordsFilePath, FileOperationsQueue);
            var passwordService = new PasswordService(_commonPasswords, FoundPasswords, UserPasswordsFilePath, fileService);
            var archiveService = new ArchiveService(ProtectedArchives, FoundPasswords);
            
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(options =>
                {
                    // Preload common passwords
                    if (File.Exists(CommonPasswordsFilePath))
                    {
                        _commonPasswords = File.ReadAllLines(CommonPasswordsFilePath).ToList();
                    }

                    // Start the File Operations worker
                    Task.Factory.StartNew(fileService.FileOperationsWorker, TaskCreationOptions.LongRunning);

                    // Load archives
                    archiveService.LoadArchives(options.PathToZipFiles);

                    // Check Passwords
                    archiveService.CheckPasswords(passwordService);
                });
        }
    }
}
