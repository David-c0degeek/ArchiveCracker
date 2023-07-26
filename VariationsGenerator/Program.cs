using System.Collections.Concurrent;
using CommandLine;

namespace VariationsGenerator
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(opts =>
                {
                    try
                    {
                        ProcessPasswordsAsync(opts).GetAwaiter().GetResult();
                    }
                    catch (OperationCanceledException)
                    {
                        // Ignore cancellation exceptions
                    }
                    catch (AggregateException ae)
                    {
                        foreach (var e in ae.Flatten().InnerExceptions)
                        {
                            Console.WriteLine(e.Message);
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }
                });
        }

        private static async Task ProcessPasswordsAsync(Options options)
        {
            var strategies = new List<Func<string, IEnumerable<string>>>
            {
                PasswordVariations.SimpleVariations,
                PasswordVariations.Reversed,
                PasswordVariations.CharacterReplacement,
                PasswordVariations.AppendNumbersAndSymbols,
                PasswordVariations.LeetSpeak,
                PasswordVariations.AppendPrependDates,
                PasswordVariations.AppendPrependCommonSequences,
                PasswordVariations.CapitalizeLetters,
                PasswordVariations.CommonPrefixesSuffixes,
                PasswordVariations.DoublePassword,
                PasswordVariations.MirrorPassword
            };

            var knownPasswords = new HashSet<string>(File.ReadLines(options.InputFile));
            var variations = new ConcurrentQueue<string>();
            var cancellationTokenSource = new CancellationTokenSource();
            var writeTask = WriteVariationsToFile(variations, options.OutputFile, cancellationTokenSource.Token);

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
                        Console.WriteLine($"Error processing password '{basePassword}': {e}");
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellationTokenSource.Token));
            }

            await Task.WhenAll(tasks);
            cancellationTokenSource.Cancel();
            await writeTask;
        }

        private static void ProcessBasePassword(string basePassword, IEnumerable<string> knownPasswords,
            IReadOnlyCollection<Func<string, IEnumerable<string>>> strategies, ConcurrentQueue<string> variations)
        {
            foreach (var variation in GenerateVariations(basePassword, strategies))
            {
                variations.Enqueue(variation);
            }

            var otherWords = knownPasswords
                .Where(p => p != basePassword)
                .ToList();

            var combinations = PasswordVariations.CombineWords(otherWords, maxLength: 3);
            foreach (var combination in combinations)
            {
                var combinedPassword = basePassword + " " + combination;
                foreach (var variation in GenerateVariations(combinedPassword, strategies))
                {
                    variations.Enqueue(variation);
                }
            }
        }

        private static IEnumerable<string> GenerateVariations(string basePassword,
            IEnumerable<Func<string, IEnumerable<string>>> strategies)
        {
            yield return basePassword;

            foreach (var variation in strategies.SelectMany(strategy => strategy(basePassword)))
            {
                yield return variation;
            }
        }

        private static async Task WriteVariationsToFile(ConcurrentQueue<string> variations, string filename,
            CancellationToken cancellationToken)
        {
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
        }
    }
}