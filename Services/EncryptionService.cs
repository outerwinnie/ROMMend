using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ROMMend.Services;

public class EncryptionService
{
    private static readonly byte[] Salt = Encoding.UTF8.GetBytes("ROMMendSalt123!@#");
    private static readonly int KeySize = 32; // 256 bits
    private static readonly int IvSize = 16;  // 128 bits

    public string Encrypt(string plainText)
    {
        if (OperatingSystem.IsWindows())
        {
            byte[] data = Encoding.UTF8.GetBytes(plainText);
            byte[] encrypted = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }
        else
        {
            using var aes = Aes.Create();
            var key = DeriveKey("ROMMend");
            aes.Key = key;

            byte[] iv = aes.IV;
            using var msEncrypt = new MemoryStream();
            msEncrypt.Write(iv, 0, iv.Length);

            using (var cryptoStream = new CryptoStream(msEncrypt, aes.CreateEncryptor(), CryptoStreamMode.Write))
            using (var writer = new StreamWriter(cryptoStream))
            {
                writer.Write(plainText);
            }

            return Convert.ToBase64String(msEncrypt.ToArray());
        }
    }

    public string Decrypt(string cipherText)
    {
        if (OperatingSystem.IsWindows())
        {
            byte[] data = Convert.FromBase64String(cipherText);
            byte[] decrypted = ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
        else
        {
            byte[] fullCipher = Convert.FromBase64String(cipherText);
            byte[] iv = new byte[IvSize];
            byte[] cipher = new byte[fullCipher.Length - IvSize];

            Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
            Buffer.BlockCopy(fullCipher, iv.Length, cipher, 0, cipher.Length);

            using var aes = Aes.Create();
            var key = DeriveKey("ROMMend");
            aes.Key = key;
            aes.IV = iv;

            using var msDecrypt = new MemoryStream(cipher);
            using var cryptoStream = new CryptoStream(msDecrypt, aes.CreateDecryptor(), CryptoStreamMode.Read);
            using var reader = new StreamReader(cryptoStream);
            
            return reader.ReadToEnd();
        }
    }

    private byte[] DeriveKey(string password)
    {
        using var deriveBytes = new Rfc2898DeriveBytes(password, Salt, 10000, HashAlgorithmName.SHA256);
        return deriveBytes.GetBytes(KeySize);
    }
} 