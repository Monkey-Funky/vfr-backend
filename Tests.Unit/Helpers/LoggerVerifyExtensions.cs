using Microsoft.Extensions.Logging;
using Moq;

namespace Tests.Unit.Helpers;

public static class LoggerVerifyExtensions
{
    public static void VerifyLog<T>(
        this Mock<ILogger<T>> loggerMock,
        LogLevel level,
        string messageContains,
        Times times)
    {
        loggerMock.Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains(messageContains)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);
    }
}