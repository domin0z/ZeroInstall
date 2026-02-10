using System.Security.Cryptography;

namespace ZeroInstall.Core.Transport;

/// <summary>
/// AES-256-CBC streaming encryption/decryption with PBKDF2 key derivation.
/// File format: "ZIME" magic (4 bytes) + salt (16 bytes) + IV (16 bytes) + encrypted data.
/// </summary>
public static class EncryptionHelper
{
    private static readonly byte[] MagicHeader = "ZIME"u8.ToArray();
    private const int SaltSize = 16;
    private const int KeySize = 32; // AES-256
    private const int IvSize = 16;
    private const int DefaultIterations = 100_000;

    /// <summary>
    /// Derives a 32-byte AES-256 key from a passphrase using PBKDF2-SHA256.
    /// </summary>
    public static byte[] DeriveKey(string passphrase, byte[] salt, int iterations = DefaultIterations)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(passphrase, salt, iterations, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(KeySize);
    }

    /// <summary>
    /// Generates a cryptographically random 16-byte salt.
    /// </summary>
    public static byte[] GenerateSalt()
    {
        return RandomNumberGenerator.GetBytes(SaltSize);
    }

    /// <summary>
    /// Encrypts a source stream to a destination stream using AES-256-CBC.
    /// Writes "ZIME" magic header + salt + IV, then encrypted data.
    /// </summary>
    public static async Task EncryptAsync(Stream source, Stream destination, string passphrase, CancellationToken ct = default)
    {
        var salt = GenerateSalt();
        var key = DeriveKey(passphrase, salt);

        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.GenerateIV();

        // Write header: magic + salt + IV
        await destination.WriteAsync(MagicHeader, ct);
        await destination.WriteAsync(salt, ct);
        await destination.WriteAsync(aes.IV, ct);

        using var encryptor = aes.CreateEncryptor();
        await using var cryptoStream = new CryptoStream(destination, encryptor, CryptoStreamMode.Write, leaveOpen: true);
        await source.CopyToAsync(cryptoStream, ct);
        await cryptoStream.FlushFinalBlockAsync(ct);
    }

    /// <summary>
    /// Decrypts a source stream to a destination stream.
    /// Reads "ZIME" header, derives key from salt, then decrypts.
    /// </summary>
    public static async Task DecryptAsync(Stream source, Stream destination, string passphrase, CancellationToken ct = default)
    {
        // Read and verify magic header
        var magic = new byte[MagicHeader.Length];
        await source.ReadExactlyAsync(magic, ct);
        if (!magic.AsSpan().SequenceEqual(MagicHeader))
            throw new InvalidDataException("Invalid encrypted file: missing ZIME header.");

        // Read salt and IV
        var salt = new byte[SaltSize];
        await source.ReadExactlyAsync(salt, ct);

        var iv = new byte[IvSize];
        await source.ReadExactlyAsync(iv, ct);

        var key = DeriveKey(passphrase, salt);

        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        await using var cryptoStream = new CryptoStream(source, decryptor, CryptoStreamMode.Read, leaveOpen: true);
        await cryptoStream.CopyToAsync(destination, ct);
    }

    /// <summary>
    /// Encrypts a byte array and returns the encrypted result.
    /// </summary>
    public static async Task<byte[]> EncryptBytesAsync(byte[] data, string passphrase, CancellationToken ct = default)
    {
        using var input = new MemoryStream(data);
        using var output = new MemoryStream();
        await EncryptAsync(input, output, passphrase, ct);
        return output.ToArray();
    }

    /// <summary>
    /// Decrypts a byte array and returns the plaintext result.
    /// </summary>
    public static async Task<byte[]> DecryptBytesAsync(byte[] encryptedData, string passphrase, CancellationToken ct = default)
    {
        using var input = new MemoryStream(encryptedData);
        using var output = new MemoryStream();
        await DecryptAsync(input, output, passphrase, ct);
        return output.ToArray();
    }
}
