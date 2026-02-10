using System.Security.Cryptography;
using System.Text;
using ZeroInstall.Core.Transport;

namespace ZeroInstall.Core.Tests.Transport;

public class EncryptionHelperTests
{
    [Fact]
    public void DeriveKey_SameInputs_ProducesSameKey()
    {
        var salt = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
        var key1 = EncryptionHelper.DeriveKey("password123", salt);
        var key2 = EncryptionHelper.DeriveKey("password123", salt);

        key1.Should().BeEquivalentTo(key2);
    }

    [Fact]
    public void DeriveKey_DifferentPassphrases_ProduceDifferentKeys()
    {
        var salt = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
        var key1 = EncryptionHelper.DeriveKey("password1", salt);
        var key2 = EncryptionHelper.DeriveKey("password2", salt);

        key1.Should().NotBeEquivalentTo(key2);
    }

    [Fact]
    public void DeriveKey_DifferentSalts_ProduceDifferentKeys()
    {
        var salt1 = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
        var salt2 = new byte[] { 16, 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1 };
        var key1 = EncryptionHelper.DeriveKey("password", salt1);
        var key2 = EncryptionHelper.DeriveKey("password", salt2);

        key1.Should().NotBeEquivalentTo(key2);
    }

    [Fact]
    public void DeriveKey_Returns32Bytes()
    {
        var salt = EncryptionHelper.GenerateSalt();
        var key = EncryptionHelper.DeriveKey("test", salt);

        key.Should().HaveCount(32);
    }

    [Fact]
    public void GenerateSalt_Returns16Bytes()
    {
        var salt = EncryptionHelper.GenerateSalt();

        salt.Should().HaveCount(16);
    }

    [Fact]
    public void GenerateSalt_ProducesRandomOutput()
    {
        var salt1 = EncryptionHelper.GenerateSalt();
        var salt2 = EncryptionHelper.GenerateSalt();

        salt1.Should().NotBeEquivalentTo(salt2);
    }

    [Fact]
    public async Task EncryptDecrypt_SmallData_RoundTrips()
    {
        var original = Encoding.UTF8.GetBytes("Hello, World! This is a test of AES-256 encryption.");
        var passphrase = "my-secret-passphrase";

        var encrypted = await EncryptionHelper.EncryptBytesAsync(original, passphrase);
        var decrypted = await EncryptionHelper.DecryptBytesAsync(encrypted, passphrase);

        decrypted.Should().BeEquivalentTo(original);
    }

    [Fact]
    public async Task EncryptDecrypt_LargeData_RoundTrips()
    {
        var original = new byte[1024 * 1024]; // 1 MB
        Random.Shared.NextBytes(original);
        var passphrase = "large-data-test";

        using var input = new MemoryStream(original);
        using var encrypted = new MemoryStream();
        await EncryptionHelper.EncryptAsync(input, encrypted, passphrase);

        encrypted.Position = 0;
        using var decrypted = new MemoryStream();
        await EncryptionHelper.DecryptAsync(encrypted, decrypted, passphrase);

        decrypted.ToArray().Should().BeEquivalentTo(original);
    }

    [Fact]
    public async Task EncryptDecrypt_EmptyData_RoundTrips()
    {
        var original = Array.Empty<byte>();
        var passphrase = "empty-test";

        var encrypted = await EncryptionHelper.EncryptBytesAsync(original, passphrase);
        var decrypted = await EncryptionHelper.DecryptBytesAsync(encrypted, passphrase);

        decrypted.Should().BeEquivalentTo(original);
    }

    [Fact]
    public async Task Decrypt_WrongPassphrase_Throws()
    {
        var original = Encoding.UTF8.GetBytes("Secret data");
        var encrypted = await EncryptionHelper.EncryptBytesAsync(original, "correct-password");

        var act = () => EncryptionHelper.DecryptBytesAsync(encrypted, "wrong-password");

        await act.Should().ThrowAsync<CryptographicException>();
    }

    [Fact]
    public async Task Encrypt_WritesZimeMagicHeader()
    {
        var data = Encoding.UTF8.GetBytes("test");
        var encrypted = await EncryptionHelper.EncryptBytesAsync(data, "pass");

        // First 4 bytes should be "ZIME"
        var magic = Encoding.UTF8.GetString(encrypted, 0, 4);
        magic.Should().Be("ZIME");
    }

    [Fact]
    public async Task Decrypt_InvalidMagicHeader_Throws()
    {
        var badData = Encoding.UTF8.GetBytes("BADHthis is not encrypted");
        using var input = new MemoryStream(badData);
        using var output = new MemoryStream();

        var act = () => EncryptionHelper.DecryptAsync(input, output, "pass");

        await act.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*ZIME*");
    }

    [Fact]
    public async Task Encrypt_SameDataTwice_ProducesDifferentOutput()
    {
        var data = Encoding.UTF8.GetBytes("deterministic?");
        var passphrase = "same-pass";

        var encrypted1 = await EncryptionHelper.EncryptBytesAsync(data, passphrase);
        var encrypted2 = await EncryptionHelper.EncryptBytesAsync(data, passphrase);

        // Different random salt + IV means different ciphertext
        encrypted1.Should().NotBeEquivalentTo(encrypted2);
    }
}
