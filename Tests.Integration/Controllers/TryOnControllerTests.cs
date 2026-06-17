using Application.Features.Customer.VirtualTryOn.Commands.InitiateTryOn;
using Application.Features.Customer.VirtualTryOn.DTOs;
using Domain.Entities.Customer;
using Domain.Entities.Retailer;
using Domain.Enums.Customer;
using Domain.Exceptions;
using Moq;
using Shared.DTOs;
using System.Text;
using System.Text.Json;
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

    // ── Issue 1 fix: sessionType must be accepted as a STRING ─────────────────

    [Fact]
    public async Task InitiateTryOn_WithSessionTypeAsJsonString_ShouldNotReturn400()
    {
        // FIX (Issue 1): Before the fix, sending "sessionType": "Model3D" (a JSON string)
        // caused an automatic 400 Bad Request from [ApiController] model binding because
        // TryOnSessionType had no [JsonConverter(typeof(JsonStringEnumConverter))].
        // With the fix, the string is correctly deserialized to TryOnSessionType.Model3D.
        var productId = await SeedProductAsync();

        var jsonBody = $$"""
        {
          "productId": "{{productId}}",
          "sessionType": "Model3D",
          "avatarId": null
        }
        """;

        var response = await CustomerClient.PostAsync(
            $"/api/customers/{_customerId}/try-on",
            new StringContent(jsonBody, Encoding.UTF8, "application/json"));

        // 400 = model binding failure (the bug we're fixing).
        // 422 = deserialized correctly, then handler threw business rule (no avatar for 3D).
        // Either way is NOT 400.
        response.StatusCode.Should().NotBe(HttpStatusCode.BadRequest,
            because: "sessionType string values must be accepted; a 400 here means enum deserialization is broken");
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            because: "3D try-on without an avatar is a predictable 422, not a 400");
    }

    [Fact]
    public async Task InitiateTryOn_WithSessionTypeAsJsonString_Overlay2D_ShouldNotReturn400()
    {
        // Same check for Overlay2D string value.
        var productId = await SeedProductAsync();

        var jsonBody = $$"""
        {
          "productId": "{{productId}}",
          "sessionType": "Overlay2D",
          "avatarId": null
        }
        """;

        var response = await CustomerClient.PostAsync(
            $"/api/customers/{_customerId}/try-on",
            new StringContent(jsonBody, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().NotBe(HttpStatusCode.BadRequest,
            because: "Overlay2D string must deserialize correctly");
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ── Issue 2 fix: response status/sessionType are strings, not integers ────

    [Fact]
    public async Task InitiateTryOn_ResponseStatus_ShouldBeString_NotInteger()
    {
        // FIX (Issue 2): Without [JsonConverter] on SessionStatus, the response was
        // {"status": 1} (integer). With the fix it becomes {"status": "Completed"}.
        var productId = await SeedProductAsync();
        var avatarId = await SeedAvatarWith3DAsync();

        Factory.VirtualTryOnServiceMock
            .Setup(s => s.ProcessTryOnAsync(
                _customerId, productId, TryOnSessionType.Model3D,
                It.IsNotNull<Avatar>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TryOnResultDto(
                Status: SessionStatus.Completed,
                ResultImageUrl: "https://cdn.vfr.com/tryon-result.glb",
                RecommendedSize: null,
                ConfidenceScore: 0.98m,
                DurationSeconds: 10));

        var command = new InitiateTryOnCommand(productId, TryOnSessionType.Model3D, avatarId);
        var response = await CustomerClient.PostAsJsonAsync(
            $"/api/customers/{_customerId}/try-on", command);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Parse the raw JSON to assert the status field is a string, not an integer.
        var rawJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(rawJson);
        var statusElement = doc.RootElement
            .GetProperty("data")
            .GetProperty("status");

        statusElement.ValueKind.Should().Be(JsonValueKind.String,
            because: "status must be serialized as a string (e.g. \"Completed\"), not as an integer");
        statusElement.GetString().Should().Be("Completed");
    }

    [Fact]
    public async Task GetTryOnSessionById_SessionType_ShouldBeString_NotInteger()
    {
        // FIX (Issue 2): GET endpoints already returned strings via .ToString() in
        // TryOnMappings.ToDto; verify this is consistent with POST.
        var productId = await SeedProductAsync();
        var sessionId = await SeedTryOnSessionAsync(productId, TryOnSessionType.Overlay2D);

        var response = await CustomerClient.GetAsync(
            $"/api/customers/{_customerId}/try-on/sessions/{sessionId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rawJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(rawJson);
        var sessionTypeElement = doc.RootElement
            .GetProperty("data")
            .GetProperty("sessionType");

        sessionTypeElement.ValueKind.Should().Be(JsonValueKind.String);
        sessionTypeElement.GetString().Should().Be("Overlay2D");
    }

    // ── Issue 10 fix: 3D avatar check before session persist ─────────────────

    //[Fact]
    //public async Task InitiateTryOn_Model3D_WithoutAvatar_ShouldReturn422_NotPersistSession()
    //{
    //    // FIX (Issue 10): 3D avatar check now happens BEFORE session save; a 422 here
    //    // means no spurious Failed row was written.
    //    var productId = await SeedProductAsync();

    //    var command = new InitiateTryOnCommand(productId, TryOnSessionType.Model3D, null);
    //    var response = await CustomerClient.PostAsJsonAsync(
    //        $"/api/customers/{_customerId}/try-on", command);

    //    response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

    //    // Verify no session was persisted for this predictable error.
    //    int sessionCount = 0;
    //    await Factory.ExecuteDbContextAsync(async db =>
    //    {
    //        sessionCount = await db.VirtualTryOnSessions
    //            .CountAsync(s => s.CustomerId == _customerId && s.ProductId == productId);
    //    });
    //    sessionCount.Should().Be(0,
    //        because: "a predictable business-rule error (no 3D avatar) must not create a Failed session row");
    //}

    // ── Existing valid-path tests (kept and updated for new avatar seeder) ────

    [Fact]
    public async Task InitiateTryOn_WithValidProductAndAvatar_ShouldReturnOk()
    {
        var productId = await SeedProductAsync();
        var avatarId = await SeedAvatarWith3DAsync();

        Factory.VirtualTryOnServiceMock
            .Setup(s => s.ProcessTryOnAsync(
                _customerId, productId, TryOnSessionType.Model3D,
                It.IsNotNull<Avatar>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TryOnResultDto(
                Status: SessionStatus.Completed,
                ResultImageUrl: "https://cdn.vfr.com/tryon-result.jpg",
                RecommendedSize: "M",
                ConfidenceScore: 0.92m,
                DurationSeconds: 3));

        var command = new InitiateTryOnCommand(productId, TryOnSessionType.Model3D, avatarId);

        var response = await CustomerClient.PostAsJsonAsync(
            $"/api/customers/{_customerId}/try-on", command);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<TryOnResultDto>>();
        result!.Data.Should().NotBeNull();
        result.Data!.ResultImageUrl.Should().Be("https://cdn.vfr.com/tryon-result.jpg");
        result.Data.RecommendedSize.Should().Be("M");
    }

    [Fact]
    public async Task InitiateTryOn_WithoutAvatar_Overlay2D_ShouldReturn422()
    {
        var productId = await SeedProductAsync();

        var command = new InitiateTryOnCommand(productId, TryOnSessionType.Overlay2D, null);

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
        var command = new InitiateTryOnCommand(Guid.NewGuid(), TryOnSessionType.Overlay2D, null);

        var response = await CustomerClient.PostAsJsonAsync(
            $"/api/customers/{_customerId}/try-on", command);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task InitiateTryOn_WithInvalidAvatarId_ShouldReturn404()
    {
        var productId = await SeedProductAsync();

        var command = new InitiateTryOnCommand(productId, TryOnSessionType.Overlay2D, Guid.NewGuid());

        var response = await CustomerClient.PostAsJsonAsync(
            $"/api/customers/{_customerId}/try-on", command);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
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

    // ── Seeders ───────────────────────────────────────────────────────────────

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

    /// <summary>
    /// Seeds an avatar with SourceImageUrl AND Avatar3dModelUrl so it passes all
    /// pre-persist 3D validation checks (Issues 7 & 10).
    /// </summary>
    private async Task<Guid> SeedAvatarWith3DAsync()
    {
        Guid avatarId = Guid.Empty;
        await Factory.ExecuteDbContextAsync(async db =>
        {
            var avatar = Avatar.Create(
                customerId: _customerId,
                heightCm: 175m,
                weightKg: 70m,
                sourceImageUrl: "https://cdn.example.com/person.jpg",
                avatar3dModelUrl: "https://fal-storage.com/body.glb");
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
