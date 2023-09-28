using System.Collections.Concurrent;

namespace EncryptedArchivePasswordRecovery
{
    public class RuleBasedDictionaryAttackService
    {
        private readonly string _archivePath;
        private readonly string _dictionaryPath;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly ConcurrentBag<string> _foundPasswords = new();
        
        public RuleBasedDictionaryAttackService(string archivePath, string dictionaryPath)
        {
            _archivePath = archivePath;
            _dictionaryPath = dictionaryPath;
        }

        public async Task PerformAttackAsync()
        {
            var rules = GetTransformationRules();
            await foreach (var basePassword in ReadDictionaryInChunksAsync(_dictionaryPath))
            {
                var localBasePassword = basePassword; // To avoid closure issue
                var parallelOptions = new ParallelOptions
                {
                    CancellationToken = _cancellationTokenSource.Token,
                    MaxDegreeOfParallelism = Environment.ProcessorCount // Or any other suitable value
                };

                await Task.Run(() =>
                {
                    Parallel.ForEach(localBasePassword, parallelOptions, (password, loopState) =>
                    {
                        if (parallelOptions.CancellationToken.IsCancellationRequested)
                        {
                            loopState.Stop();
                            return;
                        }

                        foreach (var modifiedPassword in rules.Select(rule => rule(password))
                                     .Where(modifiedPassword => TryDecrypt(_archivePath, modifiedPassword)))
                        {
                            _foundPasswords.Add(modifiedPassword);
                            _cancellationTokenSource.Cancel();
                            return;
                        }
                    });
                });
                
                if (_cancellationTokenSource.IsCancellationRequested)
                {
                    break;
                }
            }

            ReportPasswordStatus(_foundPasswords);
        }

        private async IAsyncEnumerable<List<string>> ReadDictionaryInChunksAsync(string dictionaryPath, int chunkSize = 1000)
        {
            var chunk = new List<string>();
            await foreach (var line in ReadLinesAsync(dictionaryPath))
            {
                chunk.Add(line);
                if (chunk.Count < chunkSize) continue;
                yield return chunk;
                chunk = new List<string>();
            }

            if (chunk.Count != 0)
            {
                yield return chunk;
            }
        }

        private async IAsyncEnumerable<string> ReadLinesAsync(string filePath)
        {
            await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var streamReader = new StreamReader(fileStream, leaveOpen: true);

            while (!streamReader.EndOfStream)
            {
                yield return await streamReader.ReadLineAsync();
            }
        }

        private static List<Func<string, string>> GetTransformationRules()
        {
            return new List<Func<string, string>>
            {
                password => password.Replace('a', '@'),
                password => password.Replace('o', '0'),
                password => password.Replace('i', '1'),
                password => password + "123",
                password => password + "!",
                password => password + "?",
                password => char.ToUpper(password[0]) + password[1..],
                password => new string(password.Reverse().ToArray()),
                password => password + DateTime.Now.Year,
                password => string.Concat(password, DateTime.Now.Year.ToString().AsSpan(2))
            };
        }

        private static bool TryDecrypt(string archivePath, string password)
        {
            // Implement decryption logic here
            return false;
        }

        private static void ReportPasswordStatus(ConcurrentBag<string> foundPasswords)
        {
            Console.WriteLine(!foundPasswords.IsEmpty
                ? $"Password(s) found: {string.Join(", ", foundPasswords)}"
                : "No passwords found.");
        }
    }
}
