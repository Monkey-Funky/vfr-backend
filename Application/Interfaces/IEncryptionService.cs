namespace Application.Interfaces;

/// <summary>
/// Abstraction over AES-256 symmetric encryption.
/// Implemented in Infrastructure.Services.AesEncryptionService.
///
/// Used exclusively for encrypting PII at rest:
///   - PaymentMethod.CardholderNameEncrypted (B.13)
///
/// The encryption key is read from configuration (EncryptionSettings:Key)
/// and must NEVER appear in appsettings.json. Use user-secrets (dev)
/// or Azure Key Vault (prod).
/// </summary>
public interface IEncryptionService
{
    /// <summary>
    /// Encrypts a plaintext string using AES-256-CBC.
    /// Returns a Base64-encoded ciphertext (includes IV prefix).
    /// </summary>
    string Encrypt(string plaintext);

    /// <summary>
    /// Decrypts a Base64-encoded ciphertext previously produced by Encrypt().
    /// Throws CryptographicException if the ciphertext is tampered.
    /// </summary>
    string Decrypt(string ciphertext);
}