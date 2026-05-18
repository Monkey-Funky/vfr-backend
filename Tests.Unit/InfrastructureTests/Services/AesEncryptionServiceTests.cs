using Infrastructure.Services.Security;
using Microsoft.Extensions.Configuration;

namespace Tests.Unit.InfrastructureTests.Services;

public sealed class AesEncryptionServiceTests
{
    private readonly Mock<IConfiguration> _configMock = new();
    private readonly AesEncryptionService _sut;

    public AesEncryptionServiceTests()
    {
        _configMock.Setup(x => x["EncryptionSettings:Key"]).Returns("this-is-a-super-secret-key-for-testing");
        _sut = new AesEncryptionService(_configMock.Object);
    }

    [Fact]
    public void Encrypt_And_Decrypt_ReturnsOriginalText()
    {
        // Arrange
        var originalText = "Sensitive PII Data 123!";

        // Act
        var ciphertext = _sut.Encrypt(originalText);
        var decryptedText = _sut.Decrypt(ciphertext);

        // Assert
        ciphertext.Should().NotBe(originalText);
        decryptedText.Should().Be(originalText);
    }

    [Fact]
    public void Encrypt_Twice_ReturnsDifferentCiphertexts_DueToRandomIV()
    {
        // Arrange
        var text = "Consistent Text";

        // Act
        var cipher1 = _sut.Encrypt(text);
        var cipher2 = _sut.Encrypt(text);

        // Assert
        cipher1.Should().NotBe(cipher2);
        _sut.Decrypt(cipher1).Should().Be(text);
        _sut.Decrypt(cipher2).Should().Be(text);
    }

    [Fact]
    public void Decrypt_TamperedCiphertext_ThrowsCryptographicException()
    {
        // Arrange
        var ciphertext = _sut.Encrypt("Valid Data");
        var bytes = Convert.FromBase64String(ciphertext);
        bytes[bytes.Length - 1] ^= 0xFF; // Tamper with the last byte
        var tamperedCiphertext = Convert.ToBase64String(bytes);

        // Act
        var act = () => _sut.Decrypt(tamperedCiphertext);

        // Assert
        act.Should().Throw<System.Security.Cryptography.CryptographicException>();
    }
}
