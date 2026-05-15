using System.Net;
using System.Net.Http.Json;
using Application.Features.Customer.Address.Commands.CreateAddress;
using Application.Features.Customer.Address.Commands.UpdateAddress;
using Application.Features.Customer.Address.DTOs;
using Domain.Common;
using Domain.Entities.Customer;
using Tests.Integration.Fixtures;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs;

namespace Tests.Integration.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class CustomerAddressesControllerTests : IntegrationTestBase
{
    private readonly Guid _customerId = Guid.Parse(TestAuthHandler.DefaultCustomerId);

    public CustomerAddressesControllerTests(CustomWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task GetAddresses_ReturnsEmptyList_WhenNoAddressesExist()
    {
        var response = await CustomerClient.GetAsync("/api/customer/addresses");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<CustomerAddressDto>>>();
        result!.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAddress_ReturnsOk_WhenDataIsValid()
    {
        var command = new CreateCustomerAddressCommand(
            Label: "Home",
            AddressLine1: "123 Test Street",
            AddressLine2: "Apt 4B",
            City: "Cairo",
            StateProvince: "Cairo",
            PostalCode: "11511",
            Country: "Egypt",
            IsDefault: true);

        var response = await CustomerClient.PostAsJsonAsync("/api/customer/addresses", command);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<CustomerAddressDto>>();
        result!.Data!.Label.Should().Be("Home");
        result.Data.City.Should().Be("Cairo");
        result.Data.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task GetAddressById_ReturnsAddress_WhenExists()
    {
        var addressId = await SeedAddressAsync("Work", false);

        var response = await CustomerClient.GetAsync($"/api/customer/addresses/{addressId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<CustomerAddressDto>>();
        result!.Data!.Id.Should().Be(addressId);
        result.Data.Label.Should().Be("Work");
    }

    [Fact]
    public async Task GetAddressById_ReturnsNotFound_WhenDoesNotExist()
    {
        var response = await CustomerClient.GetAsync($"/api/customer/addresses/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    

    [Fact]
    public async Task DeleteAddress_ReturnsOk_WhenAddressExists()
    {
        var addressId = await SeedAddressAsync("Deleteable", false);

        var response = await CustomerClient.DeleteAsync($"/api/customer/addresses/{addressId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SetDefaultAddress_ReturnsOk_WhenAddressExists()
    {
        var addressId = await SeedAddressAsync("Default Candidate", false);

        var response = await CustomerClient.PatchAsync(
            $"/api/customer/addresses/{addressId}/default", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var address = await db.CustomerAddresses.FirstAsync(a => a.Id == addressId);
            address.IsDefault.Should().BeTrue();
        });
    }

    [Fact]
    public async Task GetAddresses_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var anonymousClient = Factory.CreateAnonymousClient();

        var response = await anonymousClient.GetAsync("/api/customer/addresses");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<Guid> SeedAddressAsync(string label, bool isDefault)
    {
        Guid id = Guid.Empty;
        await Factory.ExecuteDbContextAsync(async db =>
        {
            var address = CustomerAddress.Create(
                _customerId,
                label,
                "123 Test Street",
                null,
                "Cairo",
                "Cairo",
                "11511",
                "Egypt",
                isDefault);
            db.CustomerAddresses.Add(address);
            await db.SaveChangesAsync();
            id = address.Id;
        });
        return id;
    }
}
