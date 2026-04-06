using Application.Interfaces.Services;

namespace Infrastructure.Services.System;
public sealed class DateTimeService : IDateTime
{
    public DateTime UtcNow => DateTime.UtcNow;
}