using System.Threading.Tasks;

namespace BloxManager.Services
{
    public interface IEncryptionService
    {
        Task<string> EncryptAsync(string plainText);
        Task<string> DecryptAsync(string encryptedText);
        Task<string> EncryptWithPasswordAsync(string plainText, string password);
        Task<string> DecryptWithPasswordAsync(string encryptedText, string password);
    }
}
