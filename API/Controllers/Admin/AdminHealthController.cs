using API.Filters;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.Annotations;

namespace API.Controllers.Admin;

/// <summary>
/// Admin-only diagnostic endpoints.
/// Protected by X-Admin-Key header — not accessible to any Customer or Retailer JWT.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/admin/health")]
[ServiceFilter(typeof(AdminKeyAuthFilter))]
[Produces("application/json")]
[SwaggerTag("Admin Health Checks — internal diagnostics, not exposed to customers or retailers.")]
public sealed class AdminHealthController : ControllerBase
{
    private const string RateLimitCacheKey = "admin:health:fal-ai:last-called";

    private readonly FalAiSettings _falAiSettings;
    private readonly IConfiguration _configuration;
    private readonly ICacheService _cache;
    private readonly IApplicationDbContext _db;
    private readonly ILogger<AdminHealthController> _logger;

    public AdminHealthController(
        IOptions<FalAiSettings> falAiSettings,
        IConfiguration configuration,
        ICacheService cache,
        IApplicationDbContext db,
        ILogger<AdminHealthController> logger)
    {
        _falAiSettings = falAiSettings.Value;
        _configuration = configuration;
        _cache = cache;
        _db = db;
        _logger = logger;
    }

    // ==============================================================
    // GET api/admin/health/fal-ai
    // ==============================================================
    [HttpGet("fal-ai")]
    [SwaggerOperation(
        Summary = "Check fal.ai configuration",
        Description =
            "Verifies that the fal.ai API key and model IDs are present in server configuration. " +
            "Does NOT make any call to fal.ai, so no credits are consumed. " +
            "Rate-limited to one call per 24 hours per server instance. " +
            "Requires X-Admin-Key header.")]
    [ProducesResponseType(typeof(ApiResponse<FalAiHealthDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CheckFalAiConfig(CancellationToken cancellationToken)
    {
        // Rate limit: 1 call per 24 hours.
        var lastCalled = await _cache.GetAsync<LastCalledEntry>(RateLimitCacheKey, cancellationToken);

        if (lastCalled is not null)
        {
            var nextAllowedAt = lastCalled.CalledAt.AddHours(24);
            var waitMinutes = (int)Math.Ceiling((nextAllowedAt - DateTime.UtcNow).TotalMinutes);

            _logger.LogWarning(
                "Admin fal-ai health check rate limit hit. Next allowed at {NextAllowedAt}.",
                nextAllowedAt);

            return StatusCode(StatusCodes.Status429TooManyRequests,
                new ApiErrorResponse
                {
                    Code = "RATE_LIMIT_EXCEEDED",
                    Message = $"This endpoint can only be called once per 24 hours. Try again in {waitMinutes} minute(s).",
                    TraceId = HttpContext.TraceIdentifier
                });
        }

        // Record this call in cache (expires after 24 hours).
        await _cache.SetAsync(
            RateLimitCacheKey,
            new LastCalledEntry(DateTime.UtcNow),
            expiry: TimeSpan.FromHours(24),
            cancellationToken);

        // Check configuration — no external call, no credits consumed.
        var apiKey = _falAiSettings.ApiKey;
        var measurementUrl = _configuration["AiModels:MeasurementExtractionUrl"];

        var dto = new FalAiHealthDto(
            ApiKeyConfigured: !string.IsNullOrWhiteSpace(apiKey),
            ApiKeyPrefix: apiKey.Length >= 8 ? apiKey[..8] + "..." : "(too short)",
            BodyApiId: _falAiSettings.BodyApiId,
            ObjectsApiId: _falAiSettings.ObjectsApiId,
            AlignApiId: _falAiSettings.AlignApiId,
            QueueBaseUrl: _falAiSettings.QueueBaseUrl,
            MeasurementExtractionUrlConfigured: !string.IsNullOrWhiteSpace(measurementUrl),
            CheckedAt: DateTime.UtcNow
        );

        _logger.LogInformation(
            "Admin fal-ai health check executed. ApiKeyConfigured: {Configured}",
            dto.ApiKeyConfigured);

        return Ok(ApiResponse<FalAiHealthDto>.SuccessResponse(dto, "fal.ai configuration check completed. No credits were consumed."));
    }

    // ==============================================================
    // GET api/admin/health/ai-generation-errors
    // ==============================================================
    [HttpGet("ai-generation-errors")]
    [SwaggerOperation(
        Summary = "Get latest AI generation failures",
        Description = "Returns the 10 most recent failed AI generation cache entries with their error messages. Useful for diagnosing why 3D avatar generation is silently failing.")]
    [ProducesResponseType(typeof(ApiResponse<List<AiGenerationErrorDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAiGenerationErrors(CancellationToken cancellationToken)
    {
        var errors = await _db.AiGenerationCache
            .Where(x => x.Status == "Failed")
            .OrderByDescending(x => x.FailedAt)
            .Take(10)
            .Select(x => new AiGenerationErrorDto(
                x.Id,
                x.ModelId,
                x.ErrorCode,
                x.ErrorMessage,
                x.FailedAt,
                x.CustomerId))
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<List<AiGenerationErrorDto>>.SuccessResponse(errors, $"Found {errors.Count} recent failures."));
    }

    private sealed record LastCalledEntry(DateTime CalledAt);
}

public sealed record AiGenerationErrorDto(
    Guid Id,
    string? ModelId,
    string? ErrorCode,
    string? ErrorMessage,
    DateTime? FailedAt,
    Guid CustomerId
);

public sealed record FalAiHealthDto(
    bool ApiKeyConfigured,
    string ApiKeyPrefix,
    string BodyApiId,
    string ObjectsApiId,
    string AlignApiId,
    string QueueBaseUrl,
    bool MeasurementExtractionUrlConfigured,
    DateTime CheckedAt
);
