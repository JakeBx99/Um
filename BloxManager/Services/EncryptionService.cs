using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BloxManager.Services
{
    public class EncryptionService : IEncryptionService
    {
        private readonly ILogger<EncryptionService> _logger;
        private readonly byte[] _entropy;

        public EncryptionService(ILogger<EncryptionService> logger)
        {
            _logger = logger;
            _entropy = Encoding.UTF8.GetBytes("BloxManager-2025-Encryption-Key");
        }

        public async Task<string> EncryptAsync(string plainText)
        {
            try
            {
                var plainBytes = Encoding.UTF8.GetBytes(plainText);
                var encryptedBytes = ProtectedData.Protect(plainBytes, _entropy, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(encryptedBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to encrypt data");
                throw;
            }
        }

        public async Task<string> DecryptAsync(string encryptedText)
        {
            try
            {
                var encryptedBytes = Convert.FromBase64String(encryptedText);
                var plainBytes = ProtectedData.Unprotect(encryptedBytes, _entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decrypt data");
                return string.Empty;
            }
        }

        public async Task<string> EncryptWithPasswordAsync(string plainText, string password)
        {
            try
            {
                using var aes = Aes.Create();
                var key = DeriveKeyFromPassword(password, aes.KeySize / 8);
                aes.Key = key;
                aes.GenerateIV();

                using var encryptor = aes.CreateEncryptor();
                using var msEncrypt = new MemoryStream();
                using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                using (var swEncrypt = new StreamWriter(csEncrypt))
                {
                    swEncrypt.Write(plainText);
                }

                var encrypted = msEncrypt.ToArray();
                var result = new byte[aes.IV.Length + encrypted.Length];
                Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
                Buffer.BlockCopy(encrypted, 0, result, aes.IV.Length, encrypted.Length);

                return Convert.ToBase64String(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to encrypt data with password");
                throw;
            }
        }

        public async Task<string> DecryptWithPasswordAsync(string encryptedText, string password)
        {
            try
            {
                var fullCipher = Convert.FromBase64String(encryptedText);

                using var aes = Aes.Create();
                var key = DeriveKeyFromPassword(password, aes.KeySize / 8);
                aes.Key = key;

                var iv = new byte[aes.BlockSize / 8];
                var cipher = new byte[fullCipher.Length - iv.Length];

                Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
                Buffer.BlockCopy(fullCipher, iv.Length, cipher, 0, cipher.Length);

                aes.IV = iv;

                using var decryptor = aes.CreateDecryptor();
                using var msDecrypt = new MemoryStream(cipher);
                using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
                using var srDecrypt = new StreamReader(csDecrypt);
                return srDecrypt.ReadToEnd();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decrypt data with password");
                return string.Empty;
            }
        }

        private byte[] DeriveKeyFromPassword(string password, int keyLength)
        {
            using var rfc2898 = new Rfc2898DeriveBytes(password, _entropy, 10000, HashAlgorithmName.SHA256);
            return rfc2898.GetBytes(keyLength);
        }
    }
}
