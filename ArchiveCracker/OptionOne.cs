// using System;
// using System.IO;
// using System.IO.Compression;
// using System.Linq;
// using System.Threading.Tasks;
//
// namespace BruteforceArchive
// {
//     class Program1
//     {
//         static void Main(string[] args)
//         {
//             if (args.Length < 2 || !File.Exists(args[0]) || string.IsNullOrEmpty(args[1]))
//                 Usage();
//
//             var archivePath = Path.GetFullPath(args[0]);
//             var passListPath = Path.GetFullPath(args[1]);
//
//             // Load saved passwords or create new empty dictionary
//             var loadedPasswords = PasswordManager.Load() ?? new Dictionary<string, bool>();
//
//             try
//             {
//                 Console.WriteLine($"Bruting force attack on '{archivePath}'...");
//
//                 Parallel.ForEach(
//                     File.ReadLines(passListPath),
//                     () => false, // initialize local variable 'found' as False in each thread
//                     (password, loopState, found) =>
//                         TestPassword(archivePath, password) ? true : found, // test current password against archive
//                     (_, __) => Interlocked.Exchange(ref found,
//                         true)); // update shared value 'found', return previous value
//
//                 SaveProgress(loadedPasswords); // persist successful attempts to disk
//             }
//             catch (Exception ex) when (!(ex is OperationCanceledException))
//             {
//                 Console.Error.WriteLine("An error occurred while attempting to crack the archive:");
//                 Console.Error.WriteLine(ex.ToString());
//             }
//         }
//
//         private static bool TestPassword(string filePath, string password)
//         {
//             try
//             {
//                 ArchiveHelpers.OpenWithPassword(filePath, password).Dispose();
//                 Console.WriteLine($"\tFound valid password: '{password}'.");
//
//                 // Add this password to our success history so we don't waste time trying it again next time
//                 lock (_successHistoryLockObject) _successfulAttempts[_currentAttemptIndex] = password;
//
//                 // Increment index for next iteration
//                 Interlocked.Increment(ref _currentAttemptIndex);
//
//                 return true;
//             }
//             catch
//             {
//             }
//
//             return false;
//         }
//
//
//         /// <summary>
//         /// Saves successfully attempted passwords to disk for later re-use.
//         /// </summary>
//         public static void SaveProgress(Dictionary<string, bool> successfulAttempts)
//         {
//             Directory.CreateDirectory(_saveFolder);
//
//             foreach ((int i, string p) in successfulAttempts.Select((p, i) => new ValueTuple<int, string>(i + 1, p)))
//             {
//                 File.AppendAllText($"{_saveFolder}\\attempt_{i}.txt",
//                     $"{Environment.NewLine}{DateTime.Now}: Found valid password: '{p}'.");
//             }
//         }
//
//
//         #region Private Fields & Constants
//
//         int MaxThreads = Environment.ProcessorCount * 4; // Use up to four times number of processors threads by default
//
//         readonly object
//             _successHistoryLockObject = new object(); // Lock used to synchronize access to '_successfulAttempts'.
//
//         readonly List<(int Index, DateTime Timestamp)>
//             _successfulAttempts =
//                 new List<(int Index, DateTime Timestamp)>(); // Keep track of which passwords were tried at what timestamps.
//
//         const string _saveFolder = "saved"; // Folder where we'll store previously successful attempts.
//
//         static volatile int
//             _currentAttemptIndex =
//                 -1; // Current position within '_successfulAttempts'; accessed via interlocked operations.
//
//         #endregion
//
//
//         static void Usage()
//         {
//             Console.WriteLine("Usage:\n\tBruteforceArchive [pathToArchive] [pathToPasslist]");
//             Environment.Exit(-1);
//         }
//     }
//
//     internal static class ArchiveHelpers
//     {
//         public static IDisposable OpenWithPassword(string pathToFile, string password)
//         {
//             switch (Path.GetExtension(pathToFile)?.ToLower())
//             {
//                 case ".zip":
//                     return ZipFile.OpenRead(pathToFile, options: new ZipOptions() { Password = password });
//                 case ".rar":
//                     return RarArchive.Open(pathToFile, options: new SharpCompressRarOptions() { Password = password });
//                 case ".7z":
//                     return SevenZipArchive.Open(pathToFile,
//                         options: new SevenZipExtractOptions() { Password = password });
//                 default: throw new NotSupportedException($"Unsupported format: {Path.GetExtension(pathToFile)}");
//             }
//         }
//     }
// }
//
//
// public interface IArchiveEntry
// {
//     long CompressedSize { get; set; }
//
//     Stream GetStream();
//
//     IEnumerable<IArchiveEntry> Children { get; }
// }