using Application.Features.Customer.Avatar.Commands.CreateAvatar;
using Application.Features.Customer.Avatar.Commands.DeleteAvatar;
using Application.Features.Customer.Avatar.Commands.UpdateAvatarMeasurements;
using Application.Features.Customer.Avatar.DTOs;
using Domain.Entities.Customer;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs;

using Tests.Integration.Fixtures;

namespace Tests.Integration.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class AvatarControllerTests : IntegrationTestBase
{
    private readonly Guid _customerId = Guid.Parse(TestAuthHandler.DefaultCustomerId);

    public AvatarControllerTests(CustomWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task GetAvatar_ReturnsNotFound_WhenNoAvatarExists()
    {
        var response = await CustomerClient.GetAsync(
            $"/api/customers/{_customerId}/avatar");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateAvatar_ReturnsCreated_WhenDataIsValid()
    {
        var command = new CreateAvatarCommand(
            HeightCm: 175m,
            WeightKg: 70m,
            ChestCm: 95m,
            WaistCm: 80m,
            HipsCm: 97m,
            ShoulderWidthCm: 42m,
            InseamCm: 80m,
            NeckCm: 38m,
            ArmLengthCm: 60m,
            ShoeSizeEu: 42m,
            // FIX: "Athletic" is rejected by CreateAvatarCommandValidator (allowed: Rectangle,
            // Triangle, InvertedTriangle, Hourglass, Apple, Pear) — caused HTTP 422 instead of 201.
            BodyShape: "Rectangle",
            Source: "Manual");

        var response = await CustomerClient.PostAsJsonAsync(
            $"/api/customers/{_customerId}/avatar", command);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<Guid>>();
        result!.Data.Should().NotBeEmpty();

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var avatar = await db.Avatars.FirstOrDefaultAsync(a => a.CustomerId == _customerId);
            avatar.Should().NotBeNull();
            avatar!.HeightCm.Should().Be(175m);
            avatar.WeightKg.Should().Be(70m);
        });
    }

    [Fact]
    public async Task CreateAvatar_ReturnsError_WhenAvatarAlreadyExists()
    {
        await SeedAvatarAsync();

        var command = new CreateAvatarCommand(
            HeightCm: 170m,
            WeightKg: 65m,
            ChestCm: null,
            WaistCm: null,
            HipsCm: null,
            ShoulderWidthCm: null,
            InseamCm: null,
            NeckCm: null,
            ArmLengthCm: null,
            ShoeSizeEu: null,
            BodyShape: null,
            Source: "Manual");

        var response = await CustomerClient.PostAsJsonAsync(
            $"/api/customers/{_customerId}/avatar", command);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task GetAvatar_ReturnsOk_WhenAvatarExists()
    {
        await SeedAvatarAsync();

        var response = await CustomerClient.GetAsync(
            $"/api/customers/{_customerId}/avatar");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<AvatarDto>>();
        result!.Data.Should().NotBeNull();
        result.Data!.HeightCm.Should().Be(175m);
    }

    [Fact]
    public async Task GetAvatarHistory_ReturnsHistory_WhenAvatarExists()
    {
        await CreateAvatarViaApiAsync();

        var response = await CustomerClient.GetAsync(
            $"/api/customers/{_customerId}/avatar/history");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<AvatarMeasurementHistoryDto>>>();
        result!.Data.Should().NotBeNull();
        result.Data!.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task UpdateMeasurements_ReturnsNoContent_WhenAvatarExists()
    {
        var avatarId = await SeedAvatarAsync();

        var command = new UpdateAvatarMeasurementsCommand(
            AvatarId: avatarId,
            HeightCm: 176m,
            WeightKg: 72m,
            ChestCm: 96m,
            WaistCm: 82m,
            HipsCm: 98m,
            ShoulderWidthCm: 43m,
            InseamCm: 81m,
            NeckCm: 39m,
            ArmLengthCm: 61m,
            ShoeSizeEu: 42m,
            // FIX: "Athletic" is rejected by UpdateAvatarMeasurementsCommandValidator and violates
            // the DB check constraint ck_avatars_body_shape — caused HTTP 500 instead of 204.
            BodyShape: "Rectangle",
            Source: "Manual");

        var response = await CustomerClient.PatchAsJsonAsync(
            $"/api/customers/{_customerId}/avatar/measurements", command);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var avatar = await db.Avatars.FirstOrDefaultAsync(a => a.Id == avatarId);
            avatar!.HeightCm.Should().Be(176m);
            avatar.WeightKg.Should().Be(72m);
        });
    }

    [Fact]
    public async Task DeleteAvatar_ReturnsNoContent_WhenAvatarExists()
    {
        var avatarId = await SeedAvatarAsync();

        var command = new DeleteAvatarCommand(AvatarId: avatarId);

        var request = new HttpRequestMessage(HttpMethod.Delete,
            $"/api/customers/{_customerId}/avatar")
        {
            Content = JsonContent.Create(command)
        };

        var response = await CustomerClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetAvatar_ReturnsForbidden_WhenCustomerIdDoesNotMatch()
    {
        var response = await CustomerClient.GetAsync(
            $"/api/customers/{Guid.NewGuid()}/avatar");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<Guid> SeedAvatarAsync()
    {
        Guid id = Guid.Empty;
        await Factory.ExecuteDbContextAsync(async db =>
        {
            var avatar = Avatar.Create(
                customerId: _customerId,
                heightCm: 175m,
                weightKg: 70m,
                chestCm: 95m,
                waistCm: 80m,
                hipsCm: 97m);

            db.Avatars.Add(avatar);
            await db.SaveChangesAsync();
            id = avatar.Id;
        });
        return id;
    }

    private async Task CreateAvatarViaApiAsync()
    {
        var command = new CreateAvatarCommand(
            HeightCm: 175m,
            WeightKg: 70m,
            ChestCm: null,
            WaistCm: null,
            HipsCm: null,
            ShoulderWidthCm: null,
            InseamCm: null,
            NeckCm: null,
            ArmLengthCm: null,
            ShoeSizeEu: null,
            BodyShape: null,
            Source: "Manual");

        await CustomerClient.PostAsJsonAsync(
            $"/api/customers/{_customerId}/avatar", command);
    }
}