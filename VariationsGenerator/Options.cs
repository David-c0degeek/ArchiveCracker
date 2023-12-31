﻿using CommandLine;

namespace VariationsGenerator;

// ReSharper disable once ClassNeverInstantiated.Global
public class Options
{
    [Option('i', "input", Required = true, HelpText = "Input file containing the passwords.")]
    // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Global
    public string InputFile { get; set; } = null!;

    [Option('o', "output", Required = false, Default = "output.txt", HelpText = "Output file to write the password variations.")]
    // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Global
    public string? OutputFile { get; set; }
}