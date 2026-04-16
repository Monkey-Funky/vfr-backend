using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Customer;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.Auth.Commands.Register;

public sealed class RegisterCustomerCommandHandler
    : IRequestHandler<RegisterCustomerCommand, Result<string>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly ILogger<RegisterCustomerCommandHandler> _logger;

    public RegisterCustomerCommandHandler(
        IUnitOfWork unitOfWork,
        ITokenService tokenService,
        ILogger<RegisterCustomerCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<Result<string>> Handle(
        RegisterCustomerCommand command,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = command.Email.Trim().ToLowerInvariant();
        bool exists = await _unitOfWork.Repository<CustomerAccount>()
            .AnyAsync(c => c.Email == normalizedEmail && !c.IsDeleted, cancellationToken);

        if (exists)
        {
            // FIX F-03: Prevent email enumeration by returning a generic success response
            _logger.LogInformation("RegisterCustomer — email enumeration attempt blocked. Email: {Email}", command.Email);
            
            // Equalize timing to prevent timing-based enumeration
            _ = BCrypt.Net.BCrypt.HashPassword(command.Password, workFactor: 12);

            return Result<string>.Success(
                string.Empty,
                "Registration request received. If the email is eligible, a verification link has been sent.");
        }

        string passwordHash = BCrypt.Net.BCrypt.HashPassword(command.Password, workFactor: 12);

        CustomerAccount customer = CustomerAccount.Create
            (command.FullName,
            normalizedEmail,
            passwordHash);

        await _unitOfWork.Repository<CustomerAccount>().AddAsync(customer, cancellationToken);
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
            when (IsUniqueConstraintViolation(ex))
        {
            _logger.LogWarning(
                "RegisterStep1 — unique constraint violation (race condition). " +
                "Email: {Email}. Treated as ConflictException.",
                command.Email);

            throw new ConflictException("A customer with this email already exists.");
        }

        _logger.LogInformation(
            "Registration Step 1 complete. CustomerId: {CustomerId}. Email: {Email}",
            customer.Id, customer.Email);

        // The step token is a short-lived HS256 JWT containing:
        // { token_type: "step", step: 1, temp_account_id: account.Id }
        // It expires in 15 minutes. The client supplies it to RegisterStep2Command.
        string stepToken = _tokenService.GenerateTempStepToken(customer.Id, step: 1);

        return Result<string>.Success(
            stepToken,
            "Step 1 complete. Please complete your registration with other details.");
    }
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

