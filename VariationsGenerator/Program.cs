using System.Collections.Concurrent;
using System.Diagnostics;
using CommandLine;
using Serilog;
using VariationsGenerator.Strategies;

namespace VariationsGenerator
{
    public static class Program
    {
     public static void Main(string[] args)
        {
            // Configure logging
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger();

            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(opts =>
                {
                    try
                    {
                        Log.Information("Starting password variation generation...");
                        var stopwatch = Stopwatch.StartNew();

                        ProcessPasswordsAsync(opts).GetAwaiter().GetResult();

                        stopwatch.Stop();
                        Log.Information("Finished password variation generation in {ElapsedMilliseconds}ms", stopwatch.ElapsedMilliseconds);
                    }
                    catch (OperationCanceledException)
                    {
                        // Ignore cancellation exceptions
                        Log.Warning("Operation was cancelled.");
                    }
                    catch (AggregateException ae)
                    {
                        foreach (var e in ae.Flatten().InnerExceptions)
                        {
                            Log.Error(e, "Error occurred during operation");
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error(e, "Error occurred during operation");
                    }
                });

            Log.CloseAndFlush();
        }

        private static async Task ProcessPasswordsAsync(Options options)
        {
            Log.Information("Initializing password variation strategies...");

            var strategies = new List<IPasswordVariationStrategy>
            {
                new SimpleVariationsStrategy(),
                // Add the rest of the strategies
            };

            Log.Information("Reading base passwords from input file...");

            var knownPasswords = new HashSet<string>(File.ReadLines(options.InputFile));
            var variations = new ConcurrentQueue<string>();
            var cancellationTokenSource = new CancellationTokenSource();

            Log.Information($"Starting to write variations to output file: {options.OutputFile}");

            var writeTask = WriteVariationsToFile(variations, options.OutputFile, cancellationTokenSource.Token);

            Log.Information("Processing base passwords...");

            var tasks = new List<Task>();
            var degreeOfParallelism = Environment.ProcessorCount;
            var semaphore = new SemaphoreSlim(degreeOfParallelism);
            foreach (var basePassword in knownPasswords)
            {
                await semaphore.WaitAsync(cancellationTokenSource.Token);
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        ProcessBasePassword(basePassword, knownPasswords, strategies, variations);
                    }
                    catch (Exception e)
                    {
                        // Handle exceptions individually and allow other tasks to continue
                        Log.Error(e, "Error processing password '{BasePassword}'", basePassword);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellationTokenSource.Token));
            }

            Log.Information("Waiting for all tasks to complete...");

            await Task.WhenAll(tasks);
            cancellationTokenSource.Cancel();

            Log.Information("All tasks completed. Waiting for write operation to complete...");

            await writeTask;

            Log.Information("All operations completed successfully.");
        }

        private static void ProcessBasePassword(string basePassword, IEnumerable<string> knownPasswords,
            IReadOnlyCollection<IPasswordVariationStrategy> strategies, ConcurrentQueue<string> variations)
        {
            Log.Information("Processing base password: {BasePassword}", basePassword);

            foreach (var strategy in strategies)
            {
                foreach (var variation in strategy.GenerateVariations(basePassword))
                {
                    variations.Enqueue(variation);
                }
            }

            Log.Information("Finished generating variations for: {BasePassword}", basePassword);

            var otherWords = knownPasswords
                .Where(p => p != basePassword)
                .ToList();

            Log.Information("Generating combinations with other words for: {BasePassword}", basePassword);

            var combinations = PasswordVariations.CombineWords(otherWords, maxLength: 3);
            foreach (var combination in combinations)
            {
                var combinedPassword = basePassword + " " + combination;
                foreach (var variation in GenerateVariations(combinedPassword, strategies))
                {
                    variations.Enqueue(variation);
                }
            }

            Log.Information("Finished processing base password: {BasePassword}", basePassword);
        }

        private static async Task WriteVariationsToFile(ConcurrentQueue<string> variations, string filename,
            CancellationToken cancellationToken)
        {
            Log.Information("Starting to write variations to file: {Filename}", filename);

            await using var writer = new StreamWriter(filename);

            while (true)
            {
                while (variations.TryDequeue(out var variation))
                {
                    await writer.WriteLineAsync(variation);
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    while (variations.TryDequeue(out var variation))
                    {
                        await writer.WriteLineAsync(variation);
                    }

                    break;
                }

                await Task.Delay(100, cancellationToken);
            }

            Log.Information("Finished writing variations to file: {Filename}", filename);
        }
        
        private static IEnumerable<string> GenerateVariations(string basePassword, IEnumerable<IPasswordVariationStrategy> strategies)
        {
            yield return basePassword;

            foreach (var variation in strategies.SelectMany(strategy => strategy.GenerateVariations(basePassword)))
            {
                yield return variation;
            }
        }

    }
}