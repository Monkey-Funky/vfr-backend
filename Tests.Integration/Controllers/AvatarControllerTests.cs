using Application.Features.Customer.Avatar.Commands.CreateAvatar;
using Application.Features.Customer.Avatar.Commands.DeleteAvatar;
using Application.Features.Customer.Avatar.Commands.UpdateAvatarMeasurements;
using Application.Features.Customer.Avatar.DTOs;
using Domain.Entities.Customer;
using Microsoft.EntityFrameworkCore;
using Moq;
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
    public async Task CreateAvatar_WithValidMeasurements_ShouldReturn201()
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
            avatar.ChestCm.Should().Be(95m);
            avatar.WaistCm.Should().Be(80m);
            avatar.HipsCm.Should().Be(97m);
        });
    }

    [Fact]
    public async Task CreateAvatar_WhenAvatarAlreadyExists_ShouldReturn409()
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
    public async Task UpdateAvatarMeasurements_WithValidData_ShouldReturn200()
    {
        var avatarId = await SeedAvatarAsync();

        var command = new UpdateAvatarMeasurementsCommand(
            AvatarId: avatarId,
            HeightCm: 178m,
            WeightKg: 74m,
            ChestCm: 98m,
            WaistCm: 83m,
            HipsCm: 100m,
            ShoulderWidthCm: 44m,
            InseamCm: 82m,
            NeckCm: 39m,
            ArmLengthCm: 62m,
            ShoeSizeEu: 43m,
            BodyShape: "Hourglass",
            Source: "Manual");

        var response = await CustomerClient.PatchAsJsonAsync(
            $"/api/customers/{_customerId}/avatar/measurements", command);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var avatar = await db.Avatars.FirstOrDefaultAsync(a => a.Id == avatarId);
            avatar.Should().NotBeNull();
            avatar!.HeightCm.Should().Be(178m);
            avatar.WeightKg.Should().Be(74m);
            avatar.WaistCm.Should().Be(83m);
        });
    }

    [Fact]
    public async Task GetAvatar_WhenNoAvatarExists_ShouldReturn404()
    {
        var response = await CustomerClient.GetAsync(
            $"/api/customers/{_customerId}/avatar");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAvatar_WhenAvatarExists_ShouldReturn200()
    {
        await SeedAvatarAsync();

        var response = await CustomerClient.GetAsync(
            $"/api/customers/{_customerId}/avatar");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<AvatarDto>>();
        result!.Data.Should().NotBeNull();
        result.Data!.HeightCm.Should().Be(175m);
        result.Data.WeightKg.Should().Be(70m);
    }

    [Fact]
    public async Task GetSizeRecommendation_WithAvatar_ShouldReturn200()
    {
        await SeedAvatarAsync();

        var productId = Guid.NewGuid();

        Factory.SizeRecommendationServiceMock
            .Setup(s => s.RecommendSizeAsync(
                It.IsAny<Domain.Entities.Customer.Avatar>(),
                productId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Application.Features.Customer.Avatar.DTOs.SizeRecommendationDto(
                ProductId: productId,
                RecommendedSize: "M",
                ConfidenceScore: 0.92m,
                Justification: "Based on chest and waist measurements."));

        var response = await CustomerClient.GetAsync(
            $"/api/customers/{_customerId}/avatar/size-recommendation/{productId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content
            .ReadFromJsonAsync<ApiResponse<Application.Features.Customer.Avatar.DTOs.SizeRecommendationDto>>();
        result!.Data.Should().NotBeNull();
        result.Data!.RecommendedSize.Should().Be("M");
    }

    [Fact]
    public async Task GetSizeRecommendation_WithoutAvatar_ShouldReturn404()
    {
        var productId = Guid.NewGuid();

        var response = await CustomerClient.GetAsync(
            $"/api/customers/{_customerId}/avatar/size-recommendation/{productId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAvatarMeasurementHistory_ShouldReturnHistory()
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

        var response = await CustomerClient.GetAsync(
            $"/api/customers/{_customerId}/avatar/history");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<AvatarMeasurementHistoryDto>>>();
        result!.Data.Should().NotBeNull();
        result.Data!.Items.Should().NotBeEmpty();
        result.Data.Items[0].Source.Should().Be("Manual");
    }

    [Fact]
    public async Task GetAvatarMeasurementHistory_AfterMultipleUpdates_ShouldReturnAllSnapshots()
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

        await CustomerClient.PostAsJsonAsync($"/api/customers/{_customerId}/avatar", command);

        var getAvatarResponse = await CustomerClient.GetAsync($"/api/customers/{_customerId}/avatar");
        var avatarResult = await getAvatarResponse.Content.ReadFromJsonAsync<ApiResponse<AvatarDto>>();
        var avatarId = avatarResult!.Data!.Id;

        var updateCommand = new UpdateAvatarMeasurementsCommand(
            AvatarId: avatarId,
            HeightCm: 176m,
            WeightKg: 72m,
            ChestCm: null,
            WaistCm: null,
            HipsCm: null,
            ShoulderWidthCm: null,
            InseamCm: null,
            NeckCm: null,
            ArmLengthCm: null,
            ShoeSizeEu: null,
            BodyShape: null,
            Source: "BodyScan");

        await CustomerClient.PatchAsJsonAsync($"/api/customers/{_customerId}/avatar/measurements", updateCommand);

        var response = await CustomerClient.GetAsync($"/api/customers/{_customerId}/avatar/history");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<AvatarMeasurementHistoryDto>>>();
        result!.Data!.Items.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task DeleteAvatar_ShouldReturn200()
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

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var avatar = await db.Avatars
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(a => a.Id == avatarId);
            avatar.Should().NotBeNull();
            avatar!.IsDeleted.Should().BeTrue();
        });
    }

    [Fact]
    public async Task CreateAvatar_WithInvalidBodyShape_ShouldReturn422()
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
            BodyShape: "InvalidShape",
            Source: "Manual");

        var response = await CustomerClient.PostAsJsonAsync(
            $"/api/customers/{_customerId}/avatar", command);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CreateAvatar_WithZeroHeight_ShouldReturn422()
    {
        var command = new CreateAvatarCommand(
            HeightCm: 0m,
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

        var response = await CustomerClient.PostAsJsonAsync(
            $"/api/customers/{_customerId}/avatar", command);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CreateAvatar_WithInvalidSource_ShouldReturn422()
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
            Source: "Unknown");

        var response = await CustomerClient.PostAsJsonAsync(
            $"/api/customers/{_customerId}/avatar", command);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task UpdateAvatarMeasurements_WithNonExistentAvatar_ShouldReturn404()
    {
        var command = new UpdateAvatarMeasurementsCommand(
            AvatarId: Guid.NewGuid(),
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

        var response = await CustomerClient.PatchAsJsonAsync(
            $"/api/customers/{_customerId}/avatar/measurements", command);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteAvatar_WhenAvatarDoesNotExist_ShouldReturn404()
    {
        var command = new DeleteAvatarCommand(AvatarId: Guid.NewGuid());

        var request = new HttpRequestMessage(HttpMethod.Delete,
            $"/api/customers/{_customerId}/avatar")
        {
            Content = JsonContent.Create(command)
        };

        var response = await CustomerClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAvatar_WithDifferentCustomerId_ShouldReturn403()
    {
        var response = await CustomerClient.GetAsync(
            $"/api/customers/{Guid.NewGuid()}/avatar");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAvatarMeasurementHistory_WithPagination_ShouldReturnCorrectPage()
    {
        await CreateAvatarViaApiAsync();

        var response = await CustomerClient.GetAsync(
            $"/api/customers/{_customerId}/avatar/history?pageNumber=1&pageSize=5");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<AvatarMeasurementHistoryDto>>>();
        result!.Data.Should().NotBeNull();
        result.Data!.PageNumber.Should().Be(1);
        result.Data.PageSize.Should().Be(5);
    }

    [Fact]
    public async Task CreateAvatar_WithAllOptionalMeasurements_ShouldReturn201AndPersistAll()
    {
        var command = new CreateAvatarCommand(
            HeightCm: 180m,
            WeightKg: 80m,
            ChestCm: 100m,
            WaistCm: 85m,
            HipsCm: 102m,
            ShoulderWidthCm: 46m,
            InseamCm: 84m,
            NeckCm: 40m,
            ArmLengthCm: 63m,
            ShoeSizeEu: 44m,
            BodyShape: "Hourglass",
            Source: "BodyScan");

        var response = await CustomerClient.PostAsJsonAsync(
            $"/api/customers/{_customerId}/avatar", command);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var avatar = await db.Avatars.FirstOrDefaultAsync(a => a.CustomerId == _customerId);
            avatar.Should().NotBeNull();
            avatar!.ChestCm.Should().Be(100m);
            avatar.WaistCm.Should().Be(85m);
            avatar.HipsCm.Should().Be(102m);
            avatar.ShoulderWidthCm.Should().Be(46m);
            avatar.InseamCm.Should().Be(84m);
            avatar.NeckCm.Should().Be(40m);
            avatar.ArmLengthCm.Should().Be(63m);
            avatar.ShoeSizeEu.Should().Be(44m);
            avatar.BodyShape.Should().Be("Hourglass");
        });
    }

    private async Task<Guid> SeedAvatarAsync()
    {
        var command = new CreateAvatarCommand(
            HeightCm: 175m,
            WeightKg: 70m,
            ChestCm: 95m,
            WaistCm: 80m,
            HipsCm: 97m,
            ShoulderWidthCm: null,
            InseamCm: null,
            NeckCm: null,
            ArmLengthCm: null,
            ShoeSizeEu: null,
            BodyShape: null,
            Source: "Manual");

        var response = await CustomerClient.PostAsJsonAsync(
            $"/api/customers/{_customerId}/avatar", command);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<Guid>>();
        return result!.Data;
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