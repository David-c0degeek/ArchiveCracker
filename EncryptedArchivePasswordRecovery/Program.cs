using System.Collections.Concurrent;
using SharpCompress.Archives;

namespace EncryptedArchivePasswordRecovery
{
    public static class Program
    {
        private static readonly char[] CharSet =
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray();

        private const int MaxPasswordLength = 6;

        private static async Task Main()
        {
            DisplayIntro();
            var selection = GetValidatedSelection();

            switch (selection)
            {
                case 1:
                    await DictionaryAttack();
                    break;
                case 2:
                    await RuleBasedDictionaryAttack();
                    break;
                case 3:
                    await BruteForceAttack();
                    break;
                case 4:
                    await HybridAttack();
                    break;
                case 5:
                    Console.WriteLine("Goodbye.");
                    break;
            }
        }

        private static void DisplayIntro()
        {
            Console.WriteLine("Encrypted Archive Password Recovery v2.2");
            Console.WriteLine("Select method of attack:");
            Console.WriteLine("1. Dictionary Attack");
            Console.WriteLine("2. Rule-based Dictionary Attack");
            Console.WriteLine("3. Brute Force Attack");
            Console.WriteLine("4. Hybrid Attack");
            Console.WriteLine("5. End");
        }

        private static int GetValidatedSelection()
        {
            int selection;
            while (true)
            {
                if (int.TryParse(Console.ReadLine(), out selection) && selection >= 1 && selection <= 5)
                    break;
                Console.WriteLine("Invalid selection. Please enter a number between 1 and 5.");
            }

            return selection;
        }

        private static string? RequestPath(string message)
        {
            Console.WriteLine(message);
            var path = Console.ReadLine();
            while (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                Console.WriteLine("Invalid path. Please try again.");
                Console.WriteLine(message);
                path = Console.ReadLine();
            }

            return path;
        }

        private static Task DictionaryAttack()
        {
            var archivePath = RequestPath("Enter path to encrypted archive:");
            var dictionaryPath = RequestPath("Enter path to dictionary file:");
            var passwords = File.ReadAllLines(dictionaryPath);
            var foundPasswords = new ConcurrentBag<string>();

            Parallel.ForEach(passwords, password =>
            {
                if (TryDecrypt(archivePath, password))
                {
                    foundPasswords.Add(password);
                }
            });

            ReportPasswordStatus(foundPasswords);
            return Task.CompletedTask;
        }

        private static Task RuleBasedDictionaryAttack()
        {
            var archivePath = RequestPath("Enter path to encrypted archive:");
            var dictionaryPath = RequestPath("Enter path to dictionary file:");
            var rules = GetTransformationRules();

            var basePasswords = File.ReadAllLines(dictionaryPath);
            var foundPasswords = new ConcurrentBag<string>();

            Parallel.ForEach(basePasswords, basePassword =>
            {
                foreach (var modifiedPassword in rules.Select(rule => rule(basePassword))
                             .Where(modifiedPassword => TryDecrypt(archivePath, modifiedPassword)))
                {
                    foundPasswords.Add(modifiedPassword);
                    return;
                }
            });

            ReportPasswordStatus(foundPasswords);
            return Task.CompletedTask;
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

        private static Task BruteForceAttack()
        {
            var archivePath = RequestPath("Enter path to encrypted archive:");
            var foundPassword = new ConcurrentBag<string>();

            Parallel.For(1, MaxPasswordLength + 1, length => GeneratePasswords(length, "", archivePath, foundPassword));

            ReportPasswordStatus(foundPassword);
            return Task.CompletedTask;
        }

        private static void GeneratePasswords(int length, string current, string? archivePath,
            ConcurrentBag<string> foundPassword)
        {
            if (length == 0 && TryDecrypt(archivePath, current))
            {
                foundPassword.Add(current);
                return;
            }

            foreach (var ch in CharSet)
            {
                GeneratePasswords(length - 1, current + ch, archivePath, foundPassword);
            }
        }

        private static Task HybridAttack()
        {
            var archivePath = RequestPath("Enter path to encrypted archive:");
            var dictionaryPath = RequestPath("Enter path to dictionary file (for the base words):");
            var baseWords = File.ReadAllLines(dictionaryPath);

            foreach (var baseWord in baseWords)
            {
                for (var i = 1; i <= MaxPasswordLength - baseWord.Length; i++)
                    GeneratePasswordsForHybrid(i, baseWord, archivePath);
            }

            return Task.CompletedTask;
        }

        private static void GeneratePasswordsForHybrid(int length, string baseWord, string? archivePath)
        {
            if (length == 0 && TryDecrypt(archivePath, baseWord))
            {
                Console.WriteLine($"Password found: {baseWord}");
                return;
            }

            foreach (var ch in CharSet)
            {
                GeneratePasswordsForHybrid(length - 1, baseWord + ch, archivePath);
            }
        }

        private static bool TryDecrypt(string? archivePath, string password)
        {
            if (string.IsNullOrWhiteSpace(archivePath))
                return false;

            try
            {
                using var archive = ArchiveFactory.Open(archivePath,
                    new SharpCompress.Readers.ReaderOptions { Password = password });
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void ReportPasswordStatus(ConcurrentBag<string> foundPasswords)
        {
            Console.WriteLine(!foundPasswords.IsEmpty
                ? $"Password found: {foundPasswords.First()}"
                : "Password not found.");
        }
    }
}