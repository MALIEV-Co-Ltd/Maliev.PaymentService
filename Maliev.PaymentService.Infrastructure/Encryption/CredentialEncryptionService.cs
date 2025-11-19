using Microsoft.AspNetCore.DataProtection;

namespace Maliev.PaymentService.Infrastructure.Encryption;

/// <summary>
/// Encrypts and decrypts sensitive provider credentials using ASP.NET Core Data Protection API.
/// Uses a dedicated purpose string to isolate credential encryption from other data protection operations.
/// </summary>
public class CredentialEncryptionService : IEncryptionService
{
    private readonly IDataProtector _protector;

    /// <summary>
    /// Initializes the encryption service with a dedicated data protector.
    /// </summary>
    /// <param name="dataProtectionProvider">ASP.NET Core data protection provider</param>
    public CredentialEncryptionService(IDataProtectionProvider dataProtectionProvider)
    {
        // Create a dedicated protector with a specific purpose for provider credentials
        // This ensures credentials encrypted with this protector can only be decrypted by it
        _protector = dataProtectionProvider.CreateProtector("PaymentService.ProviderCredentials");
    }

    /// <summary>
    /// Encrypts plaintext data using Data Protection API.
    /// </summary>
    /// <param name="plaintext">Data to encrypt</param>
    /// <returns>Encrypted data as base64 string</returns>
    /// <exception cref="ArgumentNullException">Thrown when plaintext is null or empty</exception>
    public string Encrypt(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
        {
            throw new ArgumentNullException(nameof(plaintext), "Plaintext cannot be null or empty");
        }

        return _protector.Protect(plaintext);
    }

    /// <summary>
    /// Decrypts encrypted data using Data Protection API.
    /// </summary>
    /// <param name="ciphertext">Encrypted data as base64 string</param>
    /// <returns>Decrypted plaintext</returns>
    /// <exception cref="ArgumentNullException">Thrown when ciphertext is null or empty</exception>
    /// <exception cref="System.Security.Cryptography.CryptographicException">Thrown when decryption fails</exception>
    public string Decrypt(string ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext))
        {
            throw new ArgumentNullException(nameof(ciphertext), "Ciphertext cannot be null or empty");
        }

        return _protector.Unprotect(ciphertext);
    }
}
