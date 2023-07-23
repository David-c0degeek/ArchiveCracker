using CommandLine;

namespace ArchiveCracker;

public abstract class Options
{
    [Option('z', "zipPath", Required = false, HelpText = "Path to the directory containing zip files.")]
    public string PathToZipFiles { get; set; } = string.Empty;

    [Option('p', "passwordsPath", Required = false, HelpText = "Path to the file containing user passwords.")]
    public string UserPasswordsFilePath { get; set; } = string.Empty;
}
