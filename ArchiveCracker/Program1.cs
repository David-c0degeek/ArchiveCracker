// using System.Text;
//
// // Only needed if we're not using other external libs like SharpCompress or IonicZipLib
//
// class Program {
//     static void Main(string[] args) {
//         var rootPath = @"C:\Users\JohnDoe\";
//
//         var archiveFiles = FindArchiveFilesRecursively(rootPath);
//         
//         foreach (var fInfo in archiveFiles)
//             CheckPasswordsForFile(fInfo);
//             
//         Console.WriteLine("Done!");
//         Console.ReadLine();
//     }
//
//     private static bool IsArchiveExtensionValid(string extension) => 
//         new HashSet<string> {"zip", "rar", "7z"}.Contains(extension.ToLower());
//     
//     public static IEnumerable<FileInfo> FindArchiveFilesRecursively(string directory) {
//         var dirInfo = new DirectoryInfo(directory);
//         return dirInfo.GetFiles("*.*")
//                      .Where(file =>!IsExcludedFileName(file))
//                      .Where(file => IsArchiveExtensionValid(file.Extension))
//                      .Concat(dirInfo.EnumerateDirectories().SelectMany(subdir => 
//                         FindArchiveFilesRecursively(subdir.FullName)));
//     }
//
//     private const string ExcludedExtensionsString = ".exe,.dll"; 
//     private static readonly HashSet<string> excludedExtensionsSet = new(ExcludedExtensionsString.Split(','));
//
//     private static bool IsExcludedFileName(FileInfo fi) => 
//         excludedExtensionsSet.Contains(fi.Extension.ToLower()) || 
//         fi.Name == "Thumbs.db";
//         
//     private static void CheckPasswordsForFile(FileInfo filePath) {
//         var fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath.FullName).TrimEnd('.').ToLower();
//         var numAttemptsRemaining = NumCommonPasswordsToTry() + NumUserProvidedPasswordsToTry();
//         while (--numAttemptsRemaining >= 0 && TryNextPasswordFromList(fileNameWithoutExt))
//         {
//         }
//     }
//
//     #region Password Lists & Helpers
//
//     private const string CommonPasswordsFilename = "common_passwords.txt";
//     private const string UserProvidedPasswordsFilename = "";//TODO: add user provided passowrd filenames here...
//
//     private static Dictionary<int, string[]> commonPasswordsByLength = null;
//     private static Dictionary<int, char[,]> characterSetsByLength = null;
//
//     private static int maxPasswordLengthToTest = 50;//TODO: adjust as necessary based on available resources/time constraints etc..
//
//     private static int NumCommonPasswordsToTry() => Math.Min((maxPasswordLengthToTest * 4), MaxCommonPasswordCount);
//
//     private static int NumUserProvidedPasswordsToTry() => GetUserProvidedPasswordLists().Sum(list => list?.Length?? 0);
//
//     private static int MaxCommonPasswordCount => 36864; // number of most commonly used passwords according to https://github.com/danielmiessler/SecLists/blob/master/Passwords/Common-Credentials/10k-most-common.txt
//
//     private static IEnumerable<IEnumerable<char>> GetUserProvidedPasswordLists() {//TODO: implement logic to read multiple lists of passwords from different sources such as command line arguments, text files, databases, etc..
//         yield break;
//     }
//
//     private static string[][] LoadAllPasswordLists() {
//         var result = new List<string[]>(GetUserProvidedPasswordLists()).ToArray();
//         Array.Resize(ref result[result.Length], result.Length+1);//add space for our own common passwords list
//         result[result.Length] = File.ReadAllLines($@"{Environment.CurrentDirectory}\{CommonPasswordsFilename}");
//         return result;
//     }
//
//     private static Dictionary<int, string[]> BuildDictionaryOfCommonPasswordsByLength() {
//         var dict = new Dictionary<int, string[]>();
//         foreach (var lines in LoadAllPasswordLists()) {
//             foreach (var word in lines)
//                 AppendWordToListInDict(dict, word);
//         }
//         return dict;
//     }
//
//     private static void InitializeCharacterSetsAndMaxPasswordLengths() {
//         commonPasswordsByLength = BuildDictionaryOfCommonPasswordsByLength();
//         characterSetsByLength = GenerateCharsetsForAllPasswordLengths();
//         SetMaxPasswordLengthBasedOnAvailableResourcesOrConstraints();
//     }
//
//     private static void SetMaxPasswordLengthBasedOnAvailableResourcesOrConstraints() {
//         // TODO: set maximum length based on system capabilities, time limits, etc..
//     }
//
//     private static Dictionary<int, char[,]> GenerateCharsetsForAllPasswordLengths() {
//         var charsetGenerator = new CharsetGenerator();
//         var result = new Dictionary<int, char[,]>();
//         for (int i = minPasswordLengthToGenerateCharset; i <= maxPasswordLengthToTest; ++i) {
//             result[i] = charsetGenerator.GenerateCharset(i);
//         }
//         return result;
//     }
//
//     private static int MinPasswordLengthToGenerateCharset => 1;
//
//     private class CharsetGenerator {
//         internal char[,] GenerateCharset(int len) {
//             var chars = Enumerable.Range('a', 'z'-'a'+1).Cast<char>().Union(Enumerable.Range('A', 'Z'-'A'+1)).Distinct().OrderBy(_=>Guid.NewGuid()).Take(len*2).ToArray();
//             var arr = new char[len, 2];
//             var rand = new Random();
//             for (var j=0;j<arr.GetLength(1);++j){
//                 arr[rand.Next(len),(j%2)] = chars[(chars.Length*(j/(double)(arr.GetLength(1)))) % chars.Length];
//             }
//             return arr;
//         }
//     }
//
//     private static bool TryNextPasswordFromList(string fileNameWithoutExt) {
//         foreach (var pair in commonPasswordsByLength) {
//             foreach (var pwd in pair.Value) {
//                 if (!CheckPasswordIsValid(pwd)){
//                     continue;
//                 }
//
//                 if (++passwordAttemptCounter > totalAllowedAttemptsPerFileBeforeBailingOut) {
//                     throw new Exception($"Too many attempts ({totalAllowedAttemptsPerFileBeforeBailingOut}) made trying to crack '{fileName}' with current settings."); 
//                 }
//                 
//                 var dataBytes = ReadDataFromFile(filePath);
//                 if (dataBytes!= null) {
//                     if (DecryptUsingPassword(dataBytes, Encoding.UTF8.GetBytes(pwd)))
//                         SaveFoundPasswordToFile(filePath, pwd);
//
//                     else if ((--pair.Key < minPasswordLengthToGenerateCharset) ||
//                              DecryptUsingBruteForceAttack(Encoding.UTF8.GetBytes(pwd), dataBytes))
//                             UpdateCommonPasswordListWithNewlyCrackedPassword(pwd);
//                     
//                     PrintProgressMessage(fileNameWithoutExt, pwd, isLastIteration: false);
//                     return true;
//                 }
//             }
//
//             ResetPasswordAttemptCountersIfNeeded();
//             
//             PrintProgressMessage(fileNameWithoutExt, "", isLastIteration: true);
//         }
//
//         return false;
//     }
//
//     private static long passwordAttemptCounter = 0;
//     private static int lastPrintedPercentComplete = -1;
//     private const double printIntervalInSeconds = 10;
//     private static DateTime nextTimeToUpdateProgressBar = default;
//
//     private static void PrintProgressMessage(string fileNameWithoutExt, string attemptedPwd, bool isLastIteration) {
//         lock ("progressLockObject") {
//             var percentCompleted = Convert.ToInt32(((float)archiveIndexBeingProcessed / archiveTotalCount)*100);
//             if ((!isLastIteration && percentCompleted!= lastPrintedPercentComplete) ||
//                 (DateTime.Now - nextTimeToUpdateProgressBar).Seconds >= printIntervalInSeconds) {
//                 Console.Write($"{percentCompleted}% complete | Attempting to decrypt '{fileNameWithoutExt}': ");
//                 if(!attemptedPwd.Equals(""))Console.Write("'" + attemptedPwd + "'...");
//                 Console.WriteLine("");
//                 lastPrintedPercentComplete = percentCompleted;
//                 nextTimeToUpdateProgressBar = DateTime.Now;
//             }
//         }
//     }
//
//     private static void ResetPasswordAttemptCountersIfNeeded() {
//         if (lastPrintedPercentComplete == 99) {
//             passwordAttemptCounter = 0;
//             lastPrintedPercentComplete = -1;
//         }
//     }
//
//     private static bool DecryptUsingPassword(byte[] cipherBytes, byte[] keyBytes) {
//         //TODO: Implement decryption algorithm that uses specified encryption method (such as AES) along with correct IV value derived from header bytes inside compressed stream
//         //      For example, see how RAR v5 handles compression headers when extracting files by looking up appropriate algorithms in its built-in tables
//         //return...;
//         return false;
//     }
//
//     private static bool DecryptUsingBruteForceAttack(byte[] knownPrefixBytes, byte[] ciphertextBytes) {
//         //TODO: Use dictionary attacks against hash functions generated over each prefix of plaintext until valid match found
//         //      This would require implementing custom hashing function compatible with chosen cryptographic primitive (such as SHA-256)
//         //      Also need to consider whether attack should stop after first successful guess or keep going till end of potential prefixes
//         //return...;
//         return false;
//     }
//
//     private static void SaveFoundPasswordToFile(FileInfo filePath, string password) {
//         var writer = new StreamWriter(@"c:\temp\found.txt", append:true);
//         writer.WriteLine("{0}: {1}", filePath.FullName, password);
//         writer.Close();
//     }
//
//     private static void UpdateCommonPasswordListWithNewlyCrackedPassword(string newlyCrackedPassword) {
//         //TODO: update existing common password list file with newly discovered password so future runs don't waste time attempting same ones again
//         //      Note that updating may involve appending to bottom of file instead of replacing entire contents since some entries could have been added manually later
//     }
//
//     private static byte[] ReadDataFromFile(FileInfo filepath) {
//         //TODO: extract relevant parts of compressed file content without actually uncompressing whole thing to save RAM usage
//         //      Consider reading small chunks of data at a time rather than loading everything into memory at once
//         //      If unable to access specific part of compressed file due to corruption or unsupported features, skip those sections and move onto remaining parts
//         //      Return decrypted binary data array containing original plain text content
//         return null;
//     }
//
//     private static bool CheckPasswordIsValid(string candidatePassword) {
//         //TODO: perform additional checks beyond basic syntax validation to ensure stronger passwords meet complexity requirements, entropy levels, age restrictions, etc..
//         //      May also want to exclude certain characters or patterns that might appear frequently but usually indicate weakness (e.g., repeated digits or letters)
//         //return...;
//         return true;
//     }
//
//     #endregion
//     
// }