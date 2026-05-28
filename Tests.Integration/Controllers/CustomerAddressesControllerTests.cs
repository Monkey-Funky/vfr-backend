using Application.Features.Customer.Address.Commands.CreateAddress;
using Application.Features.Customer.Address.Commands.UpdateAddress;
using Application.Features.Customer.Address.DTOs;
using Domain.Entities.Customer;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs;
using Tests.Integration.Fixtures;

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
    public async Task CreateAddress_WithValidData_ShouldReturn201()
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
        result!.Data.Should().NotBeNull();
        result.Data!.Label.Should().Be("Home");
        result.Data.City.Should().Be("Cairo");
        result.Data.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAddress_FirstAddress_ShouldBeSetAsDefault()
    {
        var command = new CreateCustomerAddressCommand(
            Label: "First Address",
            AddressLine1: "1 Main Street",
            AddressLine2: null,
            City: "Cairo",
            StateProvince: "Cairo",
            PostalCode: "11511",
            Country: "Egypt",
            IsDefault: true);

        var response = await CustomerClient.PostAsJsonAsync("/api/customer/addresses", command);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<CustomerAddressDto>>();
        result!.Data!.IsDefault.Should().BeTrue();

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var saved = await db.CustomerAddresses.FirstAsync(a => a.Id == result.Data.Id);
            saved.IsDefault.Should().BeTrue();
            saved.CustomerId.Should().Be(_customerId);
        });
    }

    [Fact]
    public async Task UpdateAddress_WithValidData_ShouldReturn200()
    {
        var addressId = await SeedAddressAsync("Old Label", false);

        var command = new UpdateCustomerAddressCommand(
            Id: addressId,
            Label: "Updated Label",
            AddressLine1: "456 New Street",
            AddressLine2: "Floor 2",
            City: "Giza",
            StateProvince: "Giza",
            PostalCode: "12345",
            Country: "Egypt");

        var response = await CustomerClient.PutAsJsonAsync(
            $"/api/customer/addresses/{_customerId}", command);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<CustomerAddressDto>>();
        result!.Data!.Label.Should().Be("Updated Label");
        result.Data.City.Should().Be("Giza");
        result.Data.AddressLine2.Should().Be("Floor 2");

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var updated = await db.CustomerAddresses.FirstAsync(a => a.Id == addressId);
            updated.Label.Should().Be("Updated Label");
            updated.City.Should().Be("Giza");
        });
    }

    [Fact]
    public async Task DeleteAddress_WithValidId_ShouldReturn200()
    {
        var addressId = await SeedAddressAsync("To Delete", false);

        var response = await CustomerClient.DeleteAsync($"/api/customer/addresses/{addressId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify the address has been removed from the database
        await Factory.ExecuteDbContextAsync(async db =>
        {
            var address = await db.CustomerAddresses
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(a => a.Id == addressId);
            address.Should().BeNull();
        });
    }

    [Fact]
    public async Task DeleteAddress_DefaultAddress_ShouldReturn422()
    {
        var defaultAddressId = await SeedAddressAsync("Default Home", true);

        var response = await CustomerClient.DeleteAsync($"/api/customer/addresses/{defaultAddressId}");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var address = await db.CustomerAddresses.FirstAsync(a => a.Id == defaultAddressId);
            address.IsDeleted.Should().BeFalse();
            address.IsDefault.Should().BeTrue();
        });
    }

    [Fact]
    public async Task SetDefaultAddress_ShouldUpdateDefaultFlag()
    {
        var previousDefaultId = await SeedAddressAsync("Previous Default", true);
        var newDefaultId = await SeedAddressAsync("New Default Candidate", false);

        var response = await CustomerClient.PatchAsync(
            $"/api/customer/addresses/{newDefaultId}/default", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var newDefault = await db.CustomerAddresses.FirstAsync(a => a.Id == newDefaultId);
            newDefault.IsDefault.Should().BeTrue();

            var oldDefault = await db.CustomerAddresses.FirstAsync(a => a.Id == previousDefaultId);
            oldDefault.IsDefault.Should().BeFalse();
        });
    }

    [Fact]
    public async Task CustomerCannotAccess_AnotherCustomersAddress_ShouldReturn403()
    {
        var anotherCustomerId = Guid.NewGuid();
        var anotherCustomerClient = Factory.CreateAuthenticatedClient(
            anotherCustomerId.ToString(),
            "Customer",
            "intruder@customer.com");

        var addressId = await SeedAddressAsync("Protected Address", false);

        var command = new UpdateCustomerAddressCommand(
            Id: addressId,
            Label: "Hijacked",
            AddressLine1: "1 Hacker Lane",
            AddressLine2: null,
            City: "Cairo",
            StateProvince: "Cairo",
            PostalCode: "11511",
            Country: "Egypt");

        var response = await anotherCustomerClient.PutAsJsonAsync(
            $"/api/customer/addresses/{_customerId}", command);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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
    public async Task GetAddresses_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var anonymousClient = Factory.CreateAnonymousClient();

        var response = await anonymousClient.GetAsync("/api/customer/addresses");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SetDefaultAddress_AlreadyDefault_ReturnsOk_WithNoChange()
    {
        var addressId = await SeedAddressAsync("Already Default", true);

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
    public async Task GetAddresses_ReturnsList_WhenMultipleAddressesExist()
    {
        await SeedAddressAsync("Home", true);
        await SeedAddressAsync("Work", false);
        await SeedAddressAsync("Vacation", false);

        var response = await CustomerClient.GetAsync("/api/customer/addresses");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<CustomerAddressDto>>>();
        result!.Data.Should().HaveCount(3);
    }

    [Fact]
    public async Task CreateAddress_WithIsDefaultTrue_UnsetsPreviousDefault()
    {
        var firstAddressId = await SeedAddressAsync("First Home", true);

        var command = new CreateCustomerAddressCommand(
            Label: "New Home",
            AddressLine1: "789 New Road",
            AddressLine2: null,
            City: "Alexandria",
            StateProvince: "Alexandria",
            PostalCode: "21500",
            Country: "Egypt",
            IsDefault: true);

        var response = await CustomerClient.PostAsJsonAsync("/api/customer/addresses", command);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var previousDefault = await db.CustomerAddresses.FirstAsync(a => a.Id == firstAddressId);
            previousDefault.IsDefault.Should().BeFalse();

            var defaultCount = await db.CustomerAddresses
                .CountAsync(a => a.CustomerId == _customerId && a.IsDefault && !a.IsDeleted);
            defaultCount.Should().Be(1);
        });
    }

    [Fact]
    public async Task DeleteAddress_NonExistent_ReturnsNotFound()
    {
        var response = await CustomerClient.DeleteAsync($"/api/customer/addresses/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
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