using CommandLine;
// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global

namespace ArchiveCracker.Models;

// ReSharper disable once ClassNeverInstantiated.Global
public class Options
{
    [Option('z', "zipPath", Required = false, HelpText = "Path to the directory containing zip files.")]
    public string? PathToZipFiles { get; set; } = string.Empty;

    [Option('p', "passwordsPath", Required = false, HelpText = "Path to the file containing user passwords.")]
    public string? UserPasswordsFilePath { get; set; } = string.Empty;
    
    [Option('c', "commonPasswordsPath", Required = false, HelpText = "Path to the file containing commonly found passwords.")]
    public string? CommonPasswordsFilePath { get; set; } = string.Empty;
    
    [Option('f', "foundPasswordsPath", Required = false, HelpText = "Path to the output file containing all found passwords")]
    public string? FoundPasswordsFilePath { get; set; } = string.Empty;
    
    [Option('n', "notFoundPasswordsPath", Required = false, HelpText = "Path to the output file containing all files we couldn't find the password of")]
    public string? NotFoundPasswordsFilePath { get; set; } = string.Empty;
}
