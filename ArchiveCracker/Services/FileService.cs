using System.Collections.Concurrent;
using ArchiveCracker.Models;
using Newtonsoft.Json;
using Serilog;

namespace ArchiveCracker.Services
{
    public class FileService
    {
        private BlockingCollection<FileOperation> FileOperationsQueue { get; set; }
        private readonly string _commonPasswordsFilePath;
        private readonly string _foundPasswordsFilePath;
        
        public FileService(string commonPasswordsFilePath, string foundPasswordsFilePath, BlockingCollection<FileOperation> fileOperationsQueue)
        {
            _commonPasswordsFilePath = commonPasswordsFilePath;
            _foundPasswordsFilePath = foundPasswordsFilePath;
            FileOperationsQueue = fileOperationsQueue;
        }
        
        public void AppendToCommonPasswordsFile(string password)
        {
            FileOperationsQueue.Add(new FileOperation
            {
                Type = FileOperation.OperationType.AppendCommonPassword,
                Data = password
            });
        }

        public void SaveFoundPasswords(ConcurrentBag<ArchivePasswordPair> foundPasswords)
        {
            FileOperationsQueue.Add(new FileOperation
            {
                Type = FileOperation.OperationType.SaveFoundPasswords,
                Data = JsonConvert.SerializeObject(foundPasswords)
            });
        }

        public void FileOperationsWorker()
        {
            try
            {
                foreach (var operation in FileOperationsQueue.GetConsumingEnumerable())
                {
                    switch (operation.Type)
                    {
                        case FileOperation.OperationType.AppendCommonPassword:
                            File.AppendAllText(_commonPasswordsFilePath, operation.Data + Environment.NewLine);
                            break;
                        case FileOperation.OperationType.SaveFoundPasswords:
                            File.WriteAllText(_foundPasswordsFilePath, operation.Data);
                            break;
                        default:
                            Log.Error($"operation type: {operation.Type} not supported.");
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }
            catch (IOException ex)
            {
                Log.Error(ex, "An I/O error occurred in FileOperationsWorker: {Message}", ex.Message);
            }
        }
    }
}
