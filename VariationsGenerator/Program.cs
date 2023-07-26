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

    public static class PasswordVariations
    {
        public static IEnumerable<string> SimpleVariations(string password)
        {
            yield return password.ToUpper();
            yield return password.ToLower();
        }

        public static IEnumerable<string> Reversed(string password)
        {
            var charArray = password.ToCharArray();
            Array.Reverse(charArray);
            yield return new string(charArray);
        }

        public static IEnumerable<string> CharacterReplacement(string password)
        {
            yield return password.Replace('a', '@').Replace('e', '3').Replace('i', '1').Replace('o', '0');
        }

        public static IEnumerable<string> AppendNumbersAndSymbols(string password)
        {
            for (var i = 0; i < 10; i++)
            {
                yield return password + i;
            }

            yield return password + "!";
            yield return password + "@";
            yield return password + "#";
        }

        public static IEnumerable<string> LeetSpeak(string password)
        {
            yield return password.Replace('e', '3').Replace('l', '1').Replace('o', '0').Replace('t', '7');
        }

        public static IEnumerable<string> AppendPrependDates(string password)
        {
            var years = Enumerable.Range(DateTime.Now.Year - 100, 100).Select(x => x.ToString());
            foreach (var year in years)
            {
                yield return password + year;
                yield return year + password;
            }
        }

        public static IEnumerable<string> AppendPrependCommonSequences(string password)
        {
            var sequences = new List<string> { "123", "111", "777", "000" };
            foreach (var seq in sequences)
            {
                yield return password + seq;
                yield return seq + password;
            }
        }

        public static IEnumerable<string> CapitalizeLetters(string password)
        {
            if (password.Length <= 0) yield break;

            yield return char.ToUpper(password[0]) + password[1..];
            yield return password.ToUpper();
        }

        public static IEnumerable<string> CommonPrefixesSuffixes(string password)
        {
            var prefixesSuffixes = new List<string> { "my", "your", "123", "!" };
            foreach (var prefixSuffix in prefixesSuffixes)
            {
                yield return prefixSuffix + password;
                yield return password + prefixSuffix;
            }
        }

        public static IEnumerable<string> DoublePassword(string password)
        {
            yield return password + password;
        }

        public static IEnumerable<string> MirrorPassword(string password)
        {
            var charArray = password.ToCharArray();
            Array.Reverse(charArray);
            var reversed = new string(charArray);

            yield return password + reversed;
        }

        public static IEnumerable<string> CombineWords(List<string> words, int maxLength)
        {
            for (var length = 1; length <= maxLength; length++)
            {
                foreach (var combination in GetCombinations(words, length))
                {
                    yield return string.Join(" ", combination);
                }
            }
        }

        private static IEnumerable<IEnumerable<T>> GetCombinations<T>(IReadOnlyCollection<T> list, int length)
        {
            if (length == 1) return list.Select(t => new[] { t });
            return GetCombinations(list, length - 1)
                .SelectMany(t => list.Where(o => !t.Contains(o)),
                    (t1, t2) => t1.Concat(new[] { t2 }));
        }
    }
}