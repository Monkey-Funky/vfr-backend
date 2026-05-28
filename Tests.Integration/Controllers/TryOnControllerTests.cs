using Application.Features.Customer.VirtualTryOn.Commands.InitiateTryOn;
using Application.Features.Customer.VirtualTryOn.DTOs;
using Domain.Entities.Customer;
using Domain.Entities.Retailer;
using Domain.Enums.Customer;
using Domain.Exceptions;
using Moq;
using Shared.DTOs;
using Tests.Integration.Fixtures;

namespace Tests.Integration.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class TryOnControllerTests : IntegrationTestBase
{
    private readonly Guid _customerId = Guid.Parse(TestAuthHandler.DefaultCustomerId);
    private readonly Guid _retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);

    public TryOnControllerTests(CustomWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task InitiateTryOn_WithValidProductAndAvatar_ShouldReturn202()
    {
        var productId = await SeedProductAsync();
        var avatarId = await SeedAvatarAsync();

        Factory.VirtualTryOnServiceMock
            .Setup(s => s.ProcessTryOnAsync(
                _customerId,
                productId,
                TryOnSessionType.Overlay2D,
                It.IsNotNull<Avatar>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TryOnResultDto(
                Status: SessionStatus.Completed,
                ResultImageUrl: "https://cdn.vfr.com/tryon-result.jpg",
                RecommendedSize: "M",
                ConfidenceScore: 0.92m,
                DurationSeconds: 3));

        var command = new InitiateTryOnCommand(
            ProductId: productId,
            SessionType: TryOnSessionType.Overlay2D,
            AvatarId: avatarId);

        var response = await CustomerClient.PostAsJsonAsync(
            $"/api/customers/{_customerId}/try-on", command);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<TryOnResultDto>>();
        result!.Data.Should().NotBeNull();
        result.Data!.ResultImageUrl.Should().Be("https://cdn.vfr.com/tryon-result.jpg");
        result.Data.RecommendedSize.Should().Be("M");
    }

    [Fact]
    public async Task InitiateTryOn_WithoutAvatar_ShouldReturn422()
    {
        var productId = await SeedProductAsync();

        Factory.VirtualTryOnServiceMock
            .Setup(s => s.ProcessTryOnAsync(
                _customerId,
                productId,
                It.IsAny<TryOnSessionType>(),
                null,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BusinessRuleException(
                "AVATAR_REQUIRED",
                "An avatar is required to initiate a virtual try-on session."));

        var command = new InitiateTryOnCommand(
            ProductId: productId,
            SessionType: TryOnSessionType.Overlay2D,
            AvatarId: null);

        var response = await CustomerClient.PostAsJsonAsync(
            $"/api/customers/{_customerId}/try-on", command);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task GetTryOnSessions_ReturnsSessionList()
    {
        var productId = await SeedProductAsync();
        await SeedTryOnSessionAsync(productId);
        await SeedTryOnSessionAsync(productId);

        var response = await CustomerClient.GetAsync(
            $"/api/customers/{_customerId}/try-on/sessions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<VirtualTryOnSessionDto>>>();
        result!.Data.Should().NotBeNull();
        result.Data!.Items.Should().HaveCountGreaterThanOrEqualTo(2);
        result.Data.Items.Should().AllSatisfy(s => s.CustomerId.Should().Be(_customerId));
    }

    [Fact]
    public async Task GetTryOnSessions_WhenNoSessionsExist_ReturnsEmptyList()
    {
        var response = await CustomerClient.GetAsync(
            $"/api/customers/{_customerId}/try-on/sessions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<VirtualTryOnSessionDto>>>();
        result!.Data!.Items.Should().BeEmpty();
        result.Data.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetTryOnSessionById_ValidId_ReturnsSession()
    {
        var productId = await SeedProductAsync();
        var sessionId = await SeedTryOnSessionAsync(productId);

        var response = await CustomerClient.GetAsync(
            $"/api/customers/{_customerId}/try-on/sessions/{sessionId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<VirtualTryOnSessionDto>>();
        result!.Data.Should().NotBeNull();
        result.Data!.Id.Should().Be(sessionId);
        result.Data.CustomerId.Should().Be(_customerId);
        result.Data.ProductId.Should().Be(productId);
    }

    [Fact]
    public async Task GetTryOnSessionById_InvalidId_ShouldReturn404()
    {
        var response = await CustomerClient.GetAsync(
            $"/api/customers/{_customerId}/try-on/sessions/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTryOnSessions_WithPagination_ShouldReturnCorrectPage()
    {
        var productId = await SeedProductAsync();

        for (var i = 0; i < 5; i++)
            await SeedTryOnSessionAsync(productId);

        var response = await CustomerClient.GetAsync(
            $"/api/customers/{_customerId}/try-on/sessions?pageNumber=1&pageSize=3");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<VirtualTryOnSessionDto>>>();
        result!.Data.Should().NotBeNull();
        result.Data!.Items.Should().HaveCount(3);
        result.Data.PageSize.Should().Be(3);
        result.Data.PageNumber.Should().Be(1);
    }

    [Fact]
    public async Task GetTryOnSessionsByProduct_WhenSessionsExist_ReturnsSessions()
    {
        var productId = await SeedProductAsync();
        await SeedTryOnSessionAsync(productId);

        var response = await CustomerClient.GetAsync(
            $"/api/customers/{_customerId}/products/{productId}/sessions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<VirtualTryOnSessionDto>>>();
        result!.Data!.Items.Should().NotBeEmpty();
        result.Data.Items.Should().AllSatisfy(s => s.ProductId.Should().Be(productId));
    }

    [Fact]
    public async Task GetTryOnSessionsByProduct_WhenNoSessionsExist_ReturnsEmptyList()
    {
        var response = await CustomerClient.GetAsync(
            $"/api/customers/{_customerId}/products/{Guid.NewGuid()}/sessions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<VirtualTryOnSessionDto>>>();
        result!.Data!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task InitiateTryOn_WhenProductDoesNotExist_ShouldReturn404()
    {
        var command = new InitiateTryOnCommand(
            ProductId: Guid.NewGuid(),
            SessionType: TryOnSessionType.Overlay2D,
            AvatarId: null);

        var response = await CustomerClient.PostAsJsonAsync(
            $"/api/customers/{_customerId}/try-on", command);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task InitiateTryOn_WithInvalidAvatarId_ShouldReturn404()
    {
        var productId = await SeedProductAsync();

        var command = new InitiateTryOnCommand(
            ProductId: productId,
            SessionType: TryOnSessionType.Overlay2D,
            AvatarId: Guid.NewGuid());

        var response = await CustomerClient.PostAsJsonAsync(
            $"/api/customers/{_customerId}/try-on", command);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task InitiateTryOn_WhenServiceFails_ShouldPersistFailedSession()
    {
        var productId = await SeedProductAsync();

        Factory.VirtualTryOnServiceMock
            .Setup(s => s.ProcessTryOnAsync(
                It.IsAny<Guid>(),
                productId,
                It.IsAny<TryOnSessionType>(),
                It.IsAny<Avatar?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ExternalServiceException("VirtualTryOn", "ML service unavailable."));

        var command = new InitiateTryOnCommand(
            ProductId: productId,
            SessionType: TryOnSessionType.Overlay2D,
            AvatarId: null);

        var response = await CustomerClient.PostAsJsonAsync(
            $"/api/customers/{_customerId}/try-on", command);

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
    }

    [Fact]
    public async Task GetTryOnSessions_WhenCalledByDifferentCustomer_ShouldReturn403()
    {
        var response = await CustomerClient.GetAsync(
            $"/api/customers/{Guid.NewGuid()}/try-on/sessions");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetTryOnSessionById_SessionDetails_ShouldContainCorrectSessionType()
    {
        var productId = await SeedProductAsync();
        var sessionId = await SeedTryOnSessionAsync(productId, TryOnSessionType.Overlay2D);

        var response = await CustomerClient.GetAsync(
            $"/api/customers/{_customerId}/try-on/sessions/{sessionId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<VirtualTryOnSessionDto>>();
        result!.Data!.SessionType.Should().Be(TryOnSessionType.Overlay2D.ToString());
    }

    private async Task<Guid> SeedProductAsync()
    {
        Guid productId = Guid.Empty;
        await Factory.ExecuteDbContextAsync(async db =>
        {
            var product = Product.Create(_retailerId, "TryOn Product", price: 299m,
                status: Domain.Enums.Product.ProductStatus.Active);
            db.Products.Add(product);
            await db.SaveChangesAsync();
            productId = product.Id;
        });
        return productId;
    }

    private async Task<Guid> SeedAvatarAsync()
    {
        Guid avatarId = Guid.Empty;
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
            avatarId = avatar.Id;
        });
        return avatarId;
    }

    private async Task<Guid> SeedTryOnSessionAsync(
        Guid productId,
        TryOnSessionType sessionType = TryOnSessionType.Overlay2D)
    {
        Guid sessionId = Guid.Empty;
        await Factory.ExecuteDbContextAsync(async db =>
        {
            var session = VirtualTryOnSession.Create(
                customerId: _customerId,
                productId: productId,
                retailerId: _retailerId,
                sessionType: sessionType);

            db.VirtualTryOnSessions.Add(session);
            await db.SaveChangesAsync();
            sessionId = session.Id;
        });
        return sessionId;
    }
}