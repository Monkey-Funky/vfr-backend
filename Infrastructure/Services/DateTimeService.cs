namespace Infrastructure.Services;
public sealed class DateTimeService : IDateTime
{
    public DateTime UtcNow => DateTime.UtcNow;
}