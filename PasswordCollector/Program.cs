using System.Collections.Concurrent;
using CommandLine;
using Serilog;

namespace PasswordCollector
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File("logfile.log")
                .CreateLogger();

            try
            {
                Parser.Default.ParseArguments<Options>(args)
                    .WithParsed(ProcessFiles)
                    .WithNotParsed(HandleParseError);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while processing files");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
        
private static void ProcessFiles(Options opts)
{
    // List to hold all text files.
    var allFiles = Directory.EnumerateFiles(opts.SourceFolder, "*.txt", SearchOption.AllDirectories).ToList();

    // ConcurrentDictionary to hold unique passwords.
    var uniquePasswords = new ConcurrentDictionary<string, byte>();

    // Counter for tracking progress.
    var progressCounter = new CountdownEvent(allFiles.Count);

    // Counter for tracking processed passwords.
    var processedPasswords = 0;

    // Determine MaxDegreeOfParallelism based on processor count.
    var processorCount = Environment.ProcessorCount;
    var maxDegreeOfParallelism = processorCount <= 3 ? 1 : processorCount <= 20 ? processorCount - 2 : processorCount - 4;

    // If the output file exists, read existing passwords into the ConcurrentDictionary.
    if (File.Exists(opts.OutputFile))
    {
        foreach (var password in File.ReadLines(opts.OutputFile))
        {
            uniquePasswords.TryAdd(password, 0);
        }
    }

    // Load all passwords into memory.
    var allPasswords = allFiles.AsParallel()
        .WithDegreeOfParallelism(maxDegreeOfParallelism)
        .SelectMany(file => 
        {
            Log.Information("Processing file: {File}", file);
            var passwords = File.ReadLines(file)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();

            // Decrement the counter and log the progress after reading each file.
            progressCounter.Signal();
            Log.Information("Progress: {Processed}/{Total} files", allFiles.Count - progressCounter.CurrentCount, allFiles.Count);

            return passwords;
        })
        .ToList();

    // Process all passwords.
    allPasswords.AsParallel()
        .WithDegreeOfParallelism(maxDegreeOfParallelism)
        .ForAll(password =>
        {
            // If the password is unique (not in uniquePasswords), add it to uniquePasswords.
            if (!uniquePasswords.TryAdd(password, 0)) return;
            
            Interlocked.Increment(ref processedPasswords);
            if (processedPasswords % 10000 == 0)  // Adjust this number based on the desired logging frequency.
            {
                Log.Information("Progress: {Processed}/{Total} passwords", processedPasswords, allPasswords.Count);
            }
        });

    // Write all unique passwords to the output file.
    File.WriteAllLines(opts.OutputFile, uniquePasswords.Keys);

    Log.Information("Password collection complete, please check the file {OptsOutputFile}", opts.OutputFile);
}


        private static void HandleParseError(IEnumerable<Error> errs)
        {
            foreach (var err in errs)
            {
                Log.Error("Command line parse Error: {Err}", err.ToString());
            }
        }
    }
}



/*

        private static void ProcessFiles1(Options opts)
        {
            // List to hold all text files.
            var allFiles = Directory.EnumerateFiles(opts.SourceFolder, "*.txt", SearchOption.AllDirectories).ToList();

            // ConcurrentDictionary to hold unique passwords.
            var uniquePasswords = new ConcurrentDictionary<string, byte>();

            // Counter for tracking progress.
            var progressCounter = new CountdownEvent(allFiles.Count);

            // Determine MaxDegreeOfParallelism based on processor count.
            var processorCount = Environment.ProcessorCount;
            var maxDegreeOfParallelism = processorCount <= 3 ? 1 : processorCount <= 20 ? processorCount - 2 : processorCount - 4;

            
            // If the output file exists, read existing passwords into the ConcurrentDictionary.
            if (File.Exists(opts.OutputFile))
            {
                foreach (var password in File.ReadLines(opts.OutputFile))
                {
                    uniquePasswords.TryAdd(password, 0);
                }
            }

            // Parallel processing of files.
            Parallel.ForEach(allFiles, new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism }, file =>
            {
                Log.Information("Processing file: {File}", file);

                try
                {
                    // Read all lines from the file, ignoring empty or whitespace lines.
                    var passwordsInFile = File.ReadLines(file)
                        .AsParallel()
                        .WithDegreeOfParallelism(maxDegreeOfParallelism)
                        .Where(line => !string.IsNullOrWhiteSpace(line));

                    foreach (var password in passwordsInFile)
                    {
                        // If the password is unique (not in uniquePasswords), add it to uniquePasswords.
                        uniquePasswords.TryAdd(password, 0);
                    }

                    Log.Information("File processed: {File}", file);

                    // Write all unique passwords to the output file.
                    File.AppendAllLines(opts.OutputFile, uniquePasswords.Keys);

                    // Clear uniquePasswords for the next file.
                    uniquePasswords.Clear();

                    // Decrement the counter and log the progress.
                    progressCounter.Signal();
                    Log.Information("Progress: {Processed}/{Total}", allFiles.Count - progressCounter.CurrentCount, allFiles.Count);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "An error occurred while processing file {File}", file);
                }
            });

            Log.Information("Password collection complete, please check the file {OptsOutputFile}", opts.OutputFile);
        }
        
        */