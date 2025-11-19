namespace Maliev.PaymentService.Infrastructure.Encryption;

/// <summary>
/// Interface for encryption and decryption operations.
/// Used for encrypting sensitive provider credentials at rest.
/// </summary>
public interface IEncryptionService
{
    /// <summary>
    /// Encrypts plaintext data.
    /// </summary>
    /// <param name="plaintext">Data to encrypt</param>
    /// <returns>Encrypted data as base64 string</returns>
    string Encrypt(string plaintext);

    /// <summary>
    /// Decrypts encrypted data.
    /// </summary>
    /// <param name="ciphertext">Encrypted data as base64 string</param>
    /// <returns>Decrypted plaintext</returns>
    string Decrypt(string ciphertext);
}
