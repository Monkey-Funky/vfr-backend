using Application.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace Infrastructure.Services.Security;

/// <summary>
/// AES-256-CBC encryption service for PII fields stored at rest
/// (e.g., PaymentMethod.CardholderNameEncrypted).
///
/// Key derivation:
///   The raw key string from configuration is SHA-256 hashed to produce
///   exactly 32 bytes, guaranteeing AES-256 regardless of key string length.
///
/// Ciphertext format:
///   Base64( IV[16 bytes] || EncryptedBytes )
///   The IV is randomly generated per encryption call and prepended to the
///   ciphertext, enabling safe decryption without a separate IV store.
///
/// Configuration:
///   "EncryptionSettings:Key" — must be set in user-secrets (dev) or
///   Azure Key Vault (prod). NEVER in appsettings.json.
/// </summary>
public sealed class AesEncryptionService : IEncryptionService
{
    private readonly byte[] _key;

    public AesEncryptionService(IConfiguration configuration)
    {
        string rawKey = configuration["EncryptionSettings:Key"]
            ?? throw new InvalidOperationException(
                "EncryptionSettings:Key is not configured. " +
                "Set it via user-secrets (dev) or Azure Key Vault (prod).");

        // SHA-256 the raw key string to guarantee 32-byte AES-256 key.
        _key = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
    }

    /// <inheritdoc />
    public string Encrypt(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        using Aes aes = Aes.Create();
        aes.Key = _key;
        aes.Mode = CipherMode.CBC;
        aes.GenerateIV(); // Random IV per encryption.

        using ICryptoTransform encryptor = aes.CreateEncryptor();
        byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        byte[] encryptedBytes = encryptor.TransformFinalBlock(
            plaintextBytes, 0, plaintextBytes.Length);

        // Prepend IV to ciphertext for use during decryption.
        byte[] result = new byte[aes.IV.Length + encryptedBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(encryptedBytes, 0, result, aes.IV.Length, encryptedBytes.Length);

        return Convert.ToBase64String(result);
    }

    /// <inheritdoc />
    public string Decrypt(string ciphertext)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);

        byte[] ciphertextBytes = Convert.FromBase64String(ciphertext);

        using Aes aes = Aes.Create();
        aes.Key = _key;
        aes.Mode = CipherMode.CBC;

        // Extract the IV from the first 16 bytes.
        byte[] iv = new byte[16];
        byte[] encryptedData = new byte[ciphertextBytes.Length - 16];

        Buffer.BlockCopy(ciphertextBytes, 0, iv, 0, 16);
        Buffer.BlockCopy(ciphertextBytes, 16, encryptedData, 0, encryptedData.Length);

        aes.IV = iv;

        using ICryptoTransform decryptor = aes.CreateDecryptor();
        byte[] plaintextBytes = decryptor.TransformFinalBlock(
            encryptedData, 0, encryptedData.Length);

        return Encoding.UTF8.GetString(plaintextBytes);
    }
}