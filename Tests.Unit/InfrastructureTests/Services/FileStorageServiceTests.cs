using Amazon.S3;
using Amazon.S3.Model;
using Application.Interfaces.Services;
using Infrastructure.Services.Storage;
using Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Registry;
using global::Domain.Exceptions;

namespace Tests.Unit.InfrastructureTests.Services;

public sealed class FileStorageServiceTests
{
    private readonly Mock<IAmazonS3> _s3Mock = new();
    private readonly Mock<ResiliencePipelineProvider<string>> _pipelineProviderMock = new();
    private readonly Mock<ILogger<FileStorageService>> _loggerMock = new();
    private readonly IOptions<S3Settings> _settings;
    private readonly FileStorageService _sut;

    public FileStorageServiceTests()
    {
        var s3Settings = new S3Settings
        {
            BucketName = "test-bucket",
            BaseUrl = "https://cdn.test.com"
        };
        _settings = Options.Create(s3Settings);

        _pipelineProviderMock.Setup(x => x.GetPipeline("s3")).Returns(ResiliencePipeline.Empty);

        _sut = new FileStorageService(
            _s3Mock.Object,
            _settings,
            _pipelineProviderMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task UploadAsync_ValidJpeg_UploadsToS3AndReturnsUrl()
    {
        // Arrange
        var jpegData = new byte[] { 0xFF, 0xD8, 0xFF, 0xDB, 0x00, 0x00 };
        using var stream = new MemoryStream(jpegData);
        var fileName = "test.jpg";
        var folder = "products";

        _s3Mock.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutObjectResponse());

        // Act
        var url = await _sut.UploadAsync(stream, fileName, folder);

        // Assert
        url.Should().StartWith("https://cdn.test.com/products/");
        url.Should().EndWith(".jpg");
        _s3Mock.Verify(x => x.PutObjectAsync(
            It.Is<PutObjectRequest>(r => 
                r.BucketName == "test-bucket" && 
                r.ContentType == "image/jpeg" &&
                r.Key.StartsWith("products/")), 
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task UploadAsync_InvalidMagicBytes_ThrowsBusinessRuleException()
    {
        // Arrange
        var badData = new byte[] { 0x00, 0x00, 0x00, 0x00 };
        using var stream = new MemoryStream(badData);

        // Act
        var act = () => _sut.UploadAsync(stream, "test.txt", "docs");

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>()
            .Where(e => e.Code == "INVALID_FILE_TYPE");
    }

    [Fact]
    public async Task DeleteAsync_ValidUrl_CallsS3Delete()
    {
        // Arrange
        var url = "https://cdn.test.com/products/image123.jpg";

        _s3Mock.Setup(x => x.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteObjectResponse());

        // Act
        await _sut.DeleteAsync(url);

        // Assert
        _s3Mock.Verify(x => x.DeleteObjectAsync(
            It.Is<DeleteObjectRequest>(r => 
                r.BucketName == "test-bucket" && 
                r.Key == "products/image123.jpg"), 
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_ForeignUrl_LogsWarningAndDoesNotCallS3()
    {
        // Arrange
        var url = "https://other-cdn.com/malicious.jpg";

        // Act
        await _sut.DeleteAsync(url);

        // Assert
        _s3Mock.Verify(x => x.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("URL does not match configured BaseUrl")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
