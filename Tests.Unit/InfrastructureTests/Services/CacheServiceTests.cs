using System.Text;
using System.Text.Json;
using Infrastructure.Services.System;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;

namespace Tests.Unit.InfrastructureTests.Services;

public sealed class CacheServiceTests
{
    private sealed class SamplePayload
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }

    private readonly Mock<IDistributedCache> _cacheMock;
    private readonly Mock<IConnectionMultiplexer> _multiplexerMock;
    private readonly CacheService _sut;

    public CacheServiceTests()
    {
        _cacheMock = new Mock<IDistributedCache>();
        _multiplexerMock = new Mock<IConnectionMultiplexer>();
        _sut = new CacheService(_cacheMock.Object, _multiplexerMock.Object);
    }

    [Fact]
    public async Task SetAsync_StoresValueInCache()
    {
        var key = "test:set-key";
        var payload = new SamplePayload { Name = "Alice", Value = 42 };
        var capturedKey = string.Empty;
        var capturedBytes = Array.Empty<byte>();
        DistributedCacheEntryOptions? capturedOptions = null;

        _cacheMock
            .Setup(c => c.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>(
                (k, v, o, _) =>
                {
                    capturedKey = k;
                    capturedBytes = v;
                    capturedOptions = o;
                })
            .Returns(Task.CompletedTask);

        await _sut.SetAsync(key, payload);

        capturedKey.Should().Be(key);
        capturedBytes.Should().NotBeEmpty();

        var serializedJson = Encoding.UTF8.GetString(capturedBytes);
        var deserialized = JsonSerializer.Deserialize<SamplePayload>(serializedJson);
        deserialized.Should().NotBeNull();
        deserialized!.Name.Should().Be("Alice");
        deserialized.Value.Should().Be(42);

        capturedOptions.Should().NotBeNull();
        capturedOptions!.AbsoluteExpirationRelativeToNow.Should().Be(TimeSpan.FromHours(1));
    }

    [Fact]
    public async Task GetAsync_ExistingKey_ReturnsValue()
    {
        var key = "test:get-existing";
        var payload = new SamplePayload { Name = "Bob", Value = 99 };
        var serializedBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));

        _cacheMock
            .Setup(c => c.GetAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(serializedBytes);

        var result = await _sut.GetAsync<SamplePayload>(key);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Bob");
        result.Value.Should().Be(99);
    }

    [Fact]
    public async Task GetAsync_NonExistingKey_ReturnsDefault()
    {
        var key = "test:get-missing";

        _cacheMock
            .Setup(c => c.GetAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        var result = await _sut.GetAsync<SamplePayload>(key);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RemoveAsync_DeletesKeyFromCache()
    {
        var key = "test:remove-key";

        _cacheMock
            .Setup(c => c.RemoveAsync(key, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _sut.RemoveAsync(key);

        _cacheMock.Verify(
            c => c.RemoveAsync(key, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SetAsync_WithExpiry_ExpiresAfterTimeout()
    {
        var key = "test:expiry-key";
        var payload = new SamplePayload { Name = "Expiring", Value = 1 };
        var customExpiry = TimeSpan.FromMinutes(5);
        DistributedCacheEntryOptions? capturedOptions = null;

        _cacheMock
            .Setup(c => c.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>(
                (_, _, o, _) => capturedOptions = o)
            .Returns(Task.CompletedTask);

        await _sut.SetAsync(key, payload, customExpiry);

        capturedOptions.Should().NotBeNull();
        capturedOptions!.AbsoluteExpirationRelativeToNow.Should().Be(customExpiry);
        capturedOptions.AbsoluteExpirationRelativeToNow.Should().NotBe(TimeSpan.FromHours(1));
    }
}