using System.Net;
using System.Net.Http.Json;
using API.Controllers.PaymentMethods;
using Application.Features.PaymentMethods.DTOs;
using Application.Interfaces.Services;
using Domain.Common;
using Domain.Entities.Retailer;
using Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Shared.DTOs;
using Tests.Integration.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Tests.Integration.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class PaymentMethodsControllerTests : IntegrationTestBase
{
    private readonly Guid _retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);

    public PaymentMethodsControllerTests(CustomWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task GetPaymentMethods_ReturnsEmptyList_WhenNoMethodsExist()
    {
        var response = await Client.GetAsync($"/api/retailers/{_retailerId}/payment-methods");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<PaymentMethodDto>>>();
        result!.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task AddPaymentMethod_ReturnsCreated_WhenDataIsValid()
    {
        var request = new AddPaymentMethodRequest(
            ProviderType: "Visa",
            CardholderName: "Test User",
            CardNumberLast4: "4242",
            ExpiryDate: "12/2028",
            StripePaymentMethodId: "pm_test_123",
            IsSaved: true,
            SetAsDefault: false);

        var response = await Client.PostAsJsonAsync(
            $"/api/retailers/{_retailerId}/payment-methods", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task GetPaymentMethodById_ReturnsMethod_WhenExists()
    {
        var methodId = await SeedPaymentMethodAsync();

        var response = await Client.GetAsync(
            $"/api/retailers/{_retailerId}/payment-methods/{methodId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PaymentMethodDto>>();
        result!.Data!.Id.Should().Be(methodId);
    }

    [Fact]
    public async Task GetPaymentMethodById_ReturnsNotFound_WhenDoesNotExist()
    {
        var response = await Client.GetAsync(
            $"/api/retailers/{_retailerId}/payment-methods/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RemovePaymentMethod_ReturnsNoContent_WhenMethodExists()
    {
        var methodId = await SeedPaymentMethodAsync();

        var response = await Client.DeleteAsync(
            $"/api/retailers/{_retailerId}/payment-methods/{methodId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task SetDefault_ReturnsOk_WhenMethodExists()
    {
        var methodId = await SeedPaymentMethodAsync();

        var response = await Client.PutAsync(
            $"/api/retailers/{_retailerId}/payment-methods/{methodId}/default", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetPaymentMethods_ReturnsForbidden_WhenRetailerIdDoesNotMatch()
    {
        var response = await Client.GetAsync(
            $"/api/retailers/{Guid.NewGuid()}/payment-methods");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<Guid> SeedPaymentMethodAsync()
    {
        Guid id = Guid.Empty;

        // SeedPaymentMethodAsync must produce a valid AES-256 ciphertext for
        // CardholderNameEncrypted so that GetPaymentMethodsQueryHandler can call
        // IEncryptionService.Decrypt() without throwing FormatException / CryptographicException.
        //
        // We resolve the real AesEncryptionService (registered in the test host —
        // EncryptionSettings:Key is configured in CustomWebApplicationFactory) together with
        // the DbContext inside a single DI scope, keeping both the encryption key context
        // and the EF transaction consistent.
        await Factory.ExecuteInScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();
            var encryption = sp.GetRequiredService<IEncryptionService>();

            var method = PaymentMethod.Create(
                _retailerId,
                "Visa",
                encryption.Encrypt("Test User"),   // valid AES-256-CBC ciphertext
                "4242",
                "12/2028",
                "pm_test_seed");

            db.PaymentMethods.Add(method);
            await db.SaveChangesAsync();
            id = method.Id;
        });

        return id;
    }
}