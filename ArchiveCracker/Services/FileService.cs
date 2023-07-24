using System.Collections.Concurrent;
using ArchiveCracker.Models;
using Newtonsoft.Json;
using Serilog;

namespace ArchiveCracker.Services;

public class FileService
{
    private BlockingCollection<FileOperation> FileOperationsQueue { get; }
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

    public void SaveFoundPasswords(ArchivePasswordPair foundPassword)
    {
        FileOperationsQueue.Add(new FileOperation
        {
            Type = FileOperation.OperationType.SaveFoundPasswords,
            Data = JsonConvert.SerializeObject(foundPassword)
        });
    }

    public void FileOperationsWorker(CancellationToken cancellationToken)
    {
        try
        {
            foreach (var operation in FileOperationsQueue.GetConsumingEnumerable(cancellationToken))
            {
                switch (operation.Type)
                {
                    case FileOperation.OperationType.AppendCommonPassword:
                        File.AppendAllText(_commonPasswordsFilePath, operation.Data + Environment.NewLine);
                        break;
                    case FileOperation.OperationType.SaveFoundPasswords:
                        File.AppendAllText(_foundPasswordsFilePath, operation.Data + Environment.NewLine);
                        break;
                    default:
                        Log.Error($"operation type: {operation.Type} not supported.");
                        throw new ArgumentOutOfRangeException($"operation type: {operation.Type} not supported.");
                }
            }
        }
        catch (IOException ex)
        {
            Log.Error(ex, "An I/O error occurred in FileOperationsWorker: {Message}", ex.Message);
        }
    }

}