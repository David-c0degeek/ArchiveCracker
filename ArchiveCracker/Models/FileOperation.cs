﻿namespace ArchiveCracker.Models;

public class FileOperation
{
    public enum OperationType
    {
        AppendCommonPassword,
        SaveFoundPasswords,
        NotFound
    }

    public OperationType Type { get; init; }
    public string? Data { get; init; }
}