// using System.Text;
// using Ionic.Zip;
//
// namespace ArchiveCracker;
//
// internal static class Program
// {
//     private static readonly List<string> _commonPasswords = new();
//     
//     private static void Main(string[] args)
//     {
//         Init();
//         
//         var inputPasswordListPath = args[0];
//
//         StartProcess(inputPasswordListPath);
//
//         Console.WriteLine("Done!");
//         Console.ReadLine();
//     }
//
//     private static void StartProcess(string inputPasswordListPath)
//     {
//         if (_rootPath is null) return;
//         
//         var archiveFiles = FindArchiveFilesRecursively(_rootPath);
//
//         foreach (var fInfo in archiveFiles)
//             CheckPasswordsForFile(fInfo);
//     }
//
//     private static void Init()
//     {
//         _rootPath = Directory.GetCurrentDirectory();
//
//         using var reader = File.OpenText($"{_rootPath}\\common_passwords.txt");
//         
//         while (!reader.EndOfStream) 
//         {
//             var line = reader.ReadLine();
//
//             if(!string.IsNullOrEmpty(line))
//                 _commonPasswords.Add(line);
//         }
//     }
//
//     private static bool IsArchiveExtensionValid(string extension) =>
//         new HashSet<string> { "zip", "rar", "7z" }.Contains(extension.ToLower());
//
//     private static IEnumerable<FileInfo> FindArchiveFilesRecursively(string directory)
//     {
//         var dirInfo = new DirectoryInfo(directory);
//         return dirInfo.GetFiles("*.*")
//             .Where(file => !IsExcludedFileName(file))
//             .Where(file => IsArchiveExtensionValid(file.Extension))
//             .Concat(dirInfo.EnumerateDirectories().SelectMany(subDirectory =>
//                 FindArchiveFilesRecursively(subDirectory.FullName)));
//     }
//
//     private const string ExcludedExtensionsString = ".exe,.dll";
//     private static readonly HashSet<string> ExcludedExtensionsSet = new(ExcludedExtensionsString.Split(','));
//     private static string? _rootPath;
//
//     private static bool IsExcludedFileName(FileSystemInfo fi) =>
//         ExcludedExtensionsSet.Contains(fi.Extension.ToLower()) ||
//         fi.Name == "Thumbs.db";
//
//     private static void CheckPasswordsForFile(FileSystemInfo filePath)
//     {
//         if (CheckCommonPasswords(filePath))
//         {
//             return;
//         }
//
//         CheckPasswordsFromPasswordList(filePath);
//     }
//     
//     using Ionic.Zip;
//     private static bool CheckCommonPasswords(FileSystemInfo filePath) 
//     {
//         // Get an instance of Ionic.Zip.ZipFile for reading and writing zip files
//         using (ZipFile zipFile = ZipFile.Read(filePath.FullName))
//         {
//             // Try each common password until either correct or no more left
//             foreach (var password in _commonPasswords) 
//             {
//                 try 
//                 {
//                     // Attempt to open the first entry in the archive with the given password
//                     zipFile.Password = password;
//                     ZipEntry firstEntry = zipFile.Entries.FirstOrDefault();
//
//                     if (firstEntry != null)
//                     {
//                         using (var ms = new MemoryStream())
//                         {
//                             firstEntry.ExtractWithPassword(ms, password);
//                         }
//
//                         // If successful, add the password used to our results log and exit loop
//                         AddFoundPasswordToFileLog(filePath.FullName, password);
//
//                         return true; // Exit out of loop once valid password has been found
//                     }
//
//                 } 
//                 catch (BadPasswordException ex) 
//                 {
//                     // Ignore exceptions due to incorrect password
//                 }
//             }
//         }
//         return false;    // Returning False because even though there may be some common passwords, they are not guaranteed to work on every single encrypted archive
//     }
//
//     /// <summary>
//     /// Adds the given password as well as its corresponding filename to the 'foundPasswords' text file located at '_rootPath'.
//     /// </summary>
//     /// <param name="filename">Full filepath including filename</param>
//     /// <param name="password"></param>
//     private static void AddFoundPasswordToFileLog(string fileNameWithFullPath, string password) 
//     {
//         StringBuilder sb = new ();
//         sb.AppendLine($"Filename:{fileNameWithFullPath}");
//         sb.AppendLine($"Password Used:{password}\n\n");
//
//         StreamWriter writer = new ($"{_rootPath}/foundPasswords.txt", true);     // Open/create file stream in append mode
//         writer.Write(sb.ToString());                                               // Write contents to end of existing data
//         writer.Close();                                                            // Close file stream when done
//     
//     }
// }