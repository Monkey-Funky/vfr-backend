using System.Text;
using System.Text.Json;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services.Customer;
using Domain.Entities.Customer;
using Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.Customer;

public sealed class AiGenerationCacheService : IAiGenerationCacheService
{
    private readonly IApplicationDbContext _context;
    private readonly AiGenerationSettings _settings;
    private readonly FalAiSettings _falAiSettings;
    private readonly VirtualTryOn2DSettings _tryOn2DSettings;
    private readonly ILogger<AiGenerationCacheService> _logger;

    public string PipelineVersion => _settings.PipelineVersion;
    public string FalAiBodyModelId => _falAiSettings.BodyApiId;
    public string FalAiObjectsModelId => _falAiSettings.ObjectsApiId;
    public string FalAiAlignModelId => _falAiSettings.AlignApiId;
    public string TryOn2DModelId => _tryOn2DSettings.ModelId;
    public int FailedRetryWindowHours => _settings.FailedRetryWindowHours;

    public AiGenerationCacheService(
        IApplicationDbContext context,
        IOptions<AiGenerationSettings> settings,
        IOptions<FalAiSettings> falAiSettings,
        IOptions<VirtualTryOn2DSettings> tryOn2DSettings,
        ILogger<AiGenerationCacheService> logger)
    {
        _context = context;
        _settings = settings.Value;
        _falAiSettings = falAiSettings.Value;
        _tryOn2DSettings = tryOn2DSettings.Value;
        _logger = logger;
    }

    // ── Hashing ───────────────────────────────────────────────────────────────

    public string HashBytes(byte[] bytes)
    {
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public string ComputeAvatarHash(
        string frontImageHash,
        string sideImageHash,
        decimal heightCm,
        string provider,
        string bodyModelId,
        string measurementModelId,
        string pipelineVersion)
    {
        var input = $"Avatar3D|{frontImageHash}|{sideImageHash}|{heightCm:F1}|{provider}|{bodyModelId}|{measurementModelId}|{pipelineVersion}";
        return Sha256Hex(input);
    }

    public string ComputeTryOn3DHash(
        Guid avatarId,
        string avatar3dModelUrl,
        double avatarFocalLength,
        string sourceImageUrl,
        Guid productId,
        string productImageUrl,
        string? selectedSize,
        string? selectedColor,
        string provider,
        string objectsModelId,
        string alignModelId,
        string pipelineVersion)
    {
        var input = $"TryOn3D|{avatarId:N}|{avatar3dModelUrl}|{avatarFocalLength:F4}|{sourceImageUrl}|{productId:N}|{productImageUrl}|{selectedSize ?? ""}|{selectedColor ?? ""}|{provider}|{objectsModelId}|{alignModelId}|{pipelineVersion}";
        return Sha256Hex(input);
    }

    public string ComputeTryOn2DHash(
        Guid avatarId,
        string avatarFrontImageUrl,
        Guid productId,
        string productImageUrl,
        string? selectedSize,
        string? selectedColor,
        string provider,
        string tryOn2DModelId,
        string pipelineVersion)
    {
        var input = $"TryOn2D|{avatarId:N}|{avatarFrontImageUrl}|{productId:N}|{productImageUrl}|{selectedSize ?? ""}|{selectedColor ?? ""}|{provider}|{tryOn2DModelId}|{pipelineVersion}";
        return Sha256Hex(input);
    }

    // ── Cache Lookup ─────────────────────────────────────────────────────────

    public async Task<AiGenerationCache?> GetByHashAsync(string requestHash, CancellationToken ct)
    {
        return await _context.AiGenerationCache
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.RequestHash == requestHash, ct);
    }

    // ── Cache Write ──────────────────────────────────────────────────────────

    public async Task<AiGenerationCache?> TryCreateProcessingAsync(
        Guid customerId,
        string requestHash,
        string type,
        string provider,
        string modelId,
        string pipelineVersion,
        string inputJson,
        CancellationToken ct)
    {
        var entry = AiGenerationCache.CreateProcessing(
            customerId, requestHash, type, provider, modelId, pipelineVersion, inputJson);

        try
        {
            _context.AiGenerationCache.Add(entry);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "AI generation cache entry created. Hash: {HashPrefix}..., Type: {Type}, Provider: {Provider}",
                requestHash[..8], type, provider);

            return entry;
        }
        catch (DbUpdateException ex) when (IsDuplicateKeyException(ex))
        {
            // Race condition: another concurrent request inserted the same hash first.
            // Caller should reload the existing entry via GetByHashAsync.
            _logger.LogInformation(
                "Duplicate hash detected for AI generation cache. Hash: {HashPrefix}..., Type: {Type}. Reloading existing entry.",
                requestHash[..8], type);
            return null;
        }
    }

    public async Task MarkCompletedAsync(
        Guid id,
        string? resultImageUrl,
        string? resultModelUrl,
        string? resultJson,
        CancellationToken ct)
    {
        var entry = await _context.AiGenerationCache
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (entry is null)
        {
            _logger.LogWarning("AI generation cache entry {Id} not found when marking completed.", id);
            return;
        }

        entry.MarkCompleted(resultImageUrl, resultModelUrl, resultJson);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "AI generation cache entry {Id} marked Completed. ResultImageUrl: {ImageUrl}, ResultModelUrl: {ModelUrl}",
            id, resultImageUrl, resultModelUrl);
    }

    public async Task MarkFailedAsync(Guid id, string errorCode, string errorMessage, CancellationToken ct)
    {
        var entry = await _context.AiGenerationCache
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (entry is null)
        {
            _logger.LogWarning("AI generation cache entry {Id} not found when marking failed.", id);
            return;
        }

        entry.MarkFailed(errorCode, errorMessage);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "AI generation cache entry {Id} marked Failed. ErrorCode: {ErrorCode}",
            id, errorCode);
    }

    // ── Quota ─────────────────────────────────────────────────────────────────

    public async Task<bool> IsAvatarQuotaExceededAsync(Guid customerId, CancellationToken ct)
    {
        var todayUtc = DateTime.UtcNow.Date;
        var tomorrowUtc = todayUtc.AddDays(1);

        var paidToday = await _context.AiGenerationCache
            .CountAsync(c =>
                c.CustomerId == customerId &&
                c.Type == AiGenerationType.Avatar3D &&
                c.Status != AiGenerationStatus.Failed &&
                c.CreatedAt >= todayUtc &&
                c.CreatedAt < tomorrowUtc, ct);

        return paidToday >= _settings.MaxPaidAvatarGenerationsPerDay;
    }

    public async Task<bool> IsTryOnQuotaExceededAsync(Guid customerId, CancellationToken ct)
    {
        var todayUtc = DateTime.UtcNow.Date;
        var tomorrowUtc = todayUtc.AddDays(1);

        var paidToday = await _context.AiGenerationCache
            .CountAsync(c =>
                c.CustomerId == customerId &&
                (c.Type == AiGenerationType.TryOn3D || c.Type == AiGenerationType.TryOn2D) &&
                c.Status != AiGenerationStatus.Failed &&
                c.CreatedAt >= todayUtc &&
                c.CreatedAt < tomorrowUtc, ct);

        return paidToday >= _settings.MaxPaidTryOnGenerationsPerDay;
    }

    // ── Private Helpers ───────────────────────────────────────────────────────

    private static string Sha256Hex(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool IsDuplicateKeyException(DbUpdateException ex)
    {
        return ex.InnerException?.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true ||
               ex.InnerException?.Message.Contains("unique constraint", StringComparison.OrdinalIgnoreCase) == true ||
               ex.InnerException?.Message.Contains("23505", StringComparison.OrdinalIgnoreCase) == true; // PostgreSQL unique violation
    }
}
