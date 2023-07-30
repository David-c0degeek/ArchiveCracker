using CommandLine;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

namespace PasswordCollector;

// ReSharper disable once ClassNeverInstantiated.Global
public class Options
{
    [Option('s', "source", Required = true, HelpText = "Source folder containing the text files.")]
    public string SourceFolder { get; set; }

    [Option('o', "output", Default = "all_passwords.txt", HelpText = "Output file to store unique passwords.")]
    public string OutputFile { get; set; }
}