using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Auth.Commands.RegisterStep1;

/// <summary>
/// Handles Step 1 of two-step retailer registration.
///
/// FLOW:
///   1. Check email uniqueness (case-insensitive) — throw ConflictException if taken
///   2. Check brand name uniqueness — throw ConflictException if taken
///   3. Hash password (BCrypt, work factor 12)
///   4. Create RetailerAccount with Status = PendingEmailVerification
///   5. Persist the partial account
///   6. Generate a 15-minute step token containing the new account's ID
///   7. Return Result<string> with the step token
/// </summary>
public sealed class RegisterStep1CommandHandler
    : IRequestHandler<RegisterStep1Command, Result<string>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly ILogger<RegisterStep1CommandHandler> _logger;

    public RegisterStep1CommandHandler(
        IUnitOfWork unitOfWork,
        ITokenService tokenService,
        ILogger<RegisterStep1CommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<Result<string>> Handle(
        RegisterStep1Command command,
        CancellationToken cancellationToken)
    {
        // ── 1. Uniqueness: email ──────────────────────────────────────────────
        //
        // Note: The global query filter (HasQueryFilter(r => !r.IsDeleted)) already
        // excludes soft-deleted accounts. The !r.IsDeleted predicate below is explicit
        // for clarity but is semantically redundant with the filter.
        // Soft-deleted emails CAN be re-registered — this matches the partial unique
        // index (WHERE is_deleted = false). See P-013 Finding F-04.
        bool emailExists = await _unitOfWork
            .Repository<RetailerAccount>()
            .AnyAsync(
                r => r.Email.ToLower() == command.Email.ToLower().Trim()
                  && !r.IsDeleted,
                cancellationToken);

        if (emailExists)
        {
            // Throw ConflictException (409) — not UnauthorizedException or ValidationException
            throw new ConflictException("RetailerAccount", "email", command.Email);
        }

        // ── 2. Uniqueness: brand name ─────────────────────────────────────────
        bool brandExists = await _unitOfWork
            .Repository<RetailerAccount>()
            .AnyAsync(
                r => r.BrandName.ToLower() == command.BrandName.ToLower().Trim()
                  && !r.IsDeleted,
                cancellationToken);

        if (brandExists)
        {
            throw new ConflictException("RetailerAccount", "brand name", command.BrandName);
        }

        // ── 3. Hash password ──────────────────────────────────────────────────
        //
        // BCrypt work factor 12 is mandatory per 07-SecurityArchitecture.md §3.1
        string passwordHash = BCrypt.Net.BCrypt.HashPassword(command.Password, workFactor: 12);

        // ── 4. Create partial account ─────────────────────────────────────────
        RetailerAccount account = RetailerAccount.Create(
            command.FullName,
            command.Email,
            passwordHash,
            command.BrandName);

        // ── 5. Persist (with unique-constraint safety net) ────────────────────
        //
        // FIX F-02: Even though the AnyAsync checks above guard against obvious
        // duplicates, a TOCTOU race between two simultaneous requests can still cause
        // a DbUpdateException when SaveChangesAsync hits the PostgreSQL unique constraint.
        // We catch it and convert it to the same ConflictException that the AnyAsync
        // check would have produced, ensuring the client always receives HTTP 409
        // instead of an unhandled HTTP 500.
        await _unitOfWork.Repository<RetailerAccount>().AddAsync(account, cancellationToken);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
            when (IsUniqueConstraintViolation(ex))
        {
            _logger.LogWarning(
                "RegisterStep1 — unique constraint violation (race condition). " +
                "Email: {Email}. BrandName: {BrandName}. Treated as ConflictException.",
                command.Email, command.BrandName);

            // The constraint was on email or brand name — we cannot determine which one
            // without inspecting the constraint name. Report email as the conflict field
            // because that is the most common contention point.
            throw new ConflictException("RetailerAccount", "email or brand name", command.Email);
        }

        _logger.LogInformation(
            "Registration Step 1 complete. RetailerId: {RetailerId}. Email: {Email}",
            account.Id, account.Email);

        // ── 6. Generate step token ────────────────────────────────────────────
        //
        // The step token is a short-lived HS256 JWT containing:
        // { token_type: "step", step: 1, temp_account_id: account.Id }
        // It expires in 15 minutes. The client supplies it to RegisterStep2Command.
        string stepToken = _tokenService.GenerateTempStepToken(account.Id, step: 1);

        // ── 7. Return ─────────────────────────────────────────────────────────
        return Result<string>.Success(
            stepToken,
            "Step 1 complete. Please complete your registration with business details.");
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if the <see cref="DbUpdateException"/> was caused by a
    /// PostgreSQL unique constraint violation (SQLSTATE 23505).
    ///
    /// Npgsql wraps the PostgreSQL error in a <see cref="Npgsql.PostgresException"/>
    /// which is the inner exception of the <see cref="DbUpdateException"/>.
    /// We check the type name by string to avoid a hard compile-time reference to
    /// the Npgsql assembly from the Application project (which only references EF Core).
    /// </summary>
    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        // Npgsql.PostgresException has SqlState "23505" for unique violations.
        // We use reflection-free string matching on the type name to avoid adding
        // a direct Npgsql package dependency to the Application project.
        var innerEx = ex.InnerException;
        if (innerEx is null) return false;

        // Check type name (works without referencing the Npgsql assembly)
        if (innerEx.GetType().Name == "PostgresException")
        {
            // SqlState is a property on PostgresException — access via dynamic
            // to remain infrastructure-agnostic in the Application layer.
            var sqlStateProperty = innerEx.GetType().GetProperty("SqlState");
            var sqlState = sqlStateProperty?.GetValue(innerEx) as string;
            return sqlState == "23505"; // PostgreSQL unique_violation
        }

        // Fallback: check message for common unique constraint error patterns
        return innerEx.Message.Contains("unique", StringComparison.OrdinalIgnoreCase)
            || innerEx.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase);
    }
}