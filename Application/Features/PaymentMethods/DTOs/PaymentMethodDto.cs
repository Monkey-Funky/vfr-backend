using Domain.Entities.Retailer;

namespace Application.Features.PaymentMethods.DTOs;

/// <summary>
/// Read model for a saved payment method.
/// CardholderName is the decrypted value — never the encrypted DB column.
/// </summary>
public sealed record PaymentMethodDto(
    Guid Id,
    string ProviderType,
    string CardholderName,
    string CardNumberLast4,
    string ExpiryDate,
    bool IsDefault,
    bool IsSaved,
    bool IsExpired,
    DateTime CreatedAt
);

/// <summary>
/// Extension method to project a PaymentMethod entity to its DTO.
/// Requires the decrypted cardholder name (never stored on the entity).
/// No AutoMapper — explicit mapping per 03-CodingStandards.md.
/// </summary>
public static class PaymentMethodMappingExtensions
{
    public static PaymentMethodDto ToDto(
        this PaymentMethod method,
        string decryptedCardholderName) =>
        new(
            method.Id,
            method.ProviderType,
            decryptedCardholderName,
            method.CardNumberLast4,
            method.ExpiryDate,
            method.IsDefault,
            method.IsSaved,
            method.ExpiresAt < DateOnly.FromDateTime(DateTime.UtcNow),
            method.CreatedAt
        );
}