using System.Collections.Concurrent;
using ArchiveCracker.Models;
using Serilog;

namespace ArchiveCracker.Services;

public class FileService
{
    private readonly string _commonPasswordsFilePath;
    private readonly string _foundPasswordsFilePath;
    private BlockingCollection<FileOperation> FileOperationsQueue { get; }
    

    public FileService(string commonPasswordsPath, string userPasswordsPath, string foundPasswordsPath, BlockingCollection<FileOperation> fileOperationsQueue)
    {
        FileOperationsQueue = fileOperationsQueue;
        _commonPasswordsFilePath = commonPasswordsPath;
        _foundPasswordsFilePath = foundPasswordsPath;
        
        if (!File.Exists(commonPasswordsPath))
        {
            File.Create(commonPasswordsPath).Dispose();
        }

        if (!File.Exists(userPasswordsPath))
        {
            File.Create(userPasswordsPath).Dispose();
        }

        if (!File.Exists(foundPasswordsPath))
        {
            File.Create(foundPasswordsPath).Dispose();
        }
    }
        
    public void AppendToCommonPasswordsFile(string password)
    {
        FileOperationsQueue.Add(new FileOperation
        {
            Type = FileOperation.OperationType.AppendCommonPassword,
            Data = password
        });
    }

    public void SaveFoundPassword(ArchivePasswordPair foundPassword)
    {
        var formattedPassword = $"File: {foundPassword.File} <<||||>> Password: {foundPassword.Password}{Environment.NewLine}";
        FileOperationsQueue.Add(new FileOperation
        {
            Type = FileOperation.OperationType.SaveFoundPasswords,
            Data = formattedPassword
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
                        File.AppendAllText(_foundPasswordsFilePath, operation.Data);
                        break;
                    default:
                        Log.Error("operation type: {OperationType} not supported", operation.Type);
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