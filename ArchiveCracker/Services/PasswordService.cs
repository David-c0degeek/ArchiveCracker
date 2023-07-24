using System.Collections.Concurrent;
using ArchiveCracker.Models;
using ArchiveCracker.Strategies;
using Serilog;

namespace ArchiveCracker.Services
{
    public class PasswordService
    {
        private readonly FileService _fileService;
        private List<string> CommonPasswords { get; set; }
        private ConcurrentBag<ArchivePasswordPair> FoundPasswords { get; set; }
        private readonly string _userPasswordsFilePath;
        
        public PasswordService(List<string> commonPasswords, ConcurrentBag<ArchivePasswordPair> foundPasswords, string userPasswordsFilePath, FileService fileService)
        {
            CommonPasswords = commonPasswords;
            FoundPasswords = foundPasswords;
            _userPasswordsFilePath = userPasswordsFilePath;
            _fileService = fileService;
        }
        
        public bool CheckCommonPasswords(IArchiveStrategy strategy, string file)
        {
            foreach (var password in CommonPasswords.Where(password => strategy.IsPasswordCorrect(file, password)))
            {
                Log.Information("Found password in common passwords for file: {File}", file);
                AddPasswordAndSave(file, password);
                return true;
            }

            return false;
        }

        public void CheckUserPasswords(IArchiveStrategy strategy, string file)
        {
            using var sr = new StreamReader(_userPasswordsFilePath);

            while (sr.ReadLine() is { } line)
            {
                if (!strategy.IsPasswordCorrect(file, line)) continue;

                Log.Information("Found password in user passwords for file: {File}", file);
                AddPasswordAndSave(file, line);

                // Check if it's already in common passwords
                if (!CommonPasswords.Contains(line))
                {
                    CommonPasswords.Add(line);
                    _fileService.AppendToCommonPasswordsFile(line);
                }

                break;
            }
        }

        private void AddPasswordAndSave(string file, string password)
        {
            FoundPasswords.Add(new ArchivePasswordPair { File = file, Password = password });
            Log.Information("Password {Password} for archive {File} was added to FoundPasswords.", password, file);
            _fileService.SaveFoundPasswords(FoundPasswords);
        }
    }
}
