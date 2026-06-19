using Application.Features.Customer.VirtualTryOn.Commands.InitiateTryOn;
using Application.Features.Customer.VirtualTryOn.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Application.Interfaces.Services.Customer;
using Domain.Entities.Customer;
using Domain.Enums.Customer;
using Domain.Enums.Product;
using Moq.EntityFrameworkCore;

namespace Tests.Unit.Application.Features.Customer.VirtualTryOn;

public sealed class InitiateTryOnCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _contextMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IVirtualTryOnService> _tryOnServiceMock = new();
    private readonly Mock<IVirtualTryOn2DService> _tryOn2DServiceMock = new();
    private readonly Mock<ICacheService> _cacheServiceMock = new();
    private readonly Mock<IAiGenerationCacheService> _aiCacheMock = new();
    private readonly InitiateTryOnCommandHandler _sut;

    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid RetailerId = Guid.NewGuid();

    public InitiateTryOnCommandHandlerTests()
    {
        // Default: no cache entries, quota not exceeded
        _aiCacheMock.Setup(x => x.GetByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AiGenerationCache?)null);
        _aiCacheMock.Setup(x => x.IsAvatarQuotaExceededAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _aiCacheMock.Setup(x => x.IsTryOnQuotaExceededAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _aiCacheMock.Setup(x => x.TryCreateProcessingAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AiGenerationCache?)null);
        _aiCacheMock.Setup(x => x.MarkCompletedAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _aiCacheMock.Setup(x => x.MarkFailedAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _aiCacheMock.SetupGet(x => x.PipelineVersion).Returns("test-v1");
        _aiCacheMock.SetupGet(x => x.FalAiBodyModelId).Returns("fal-ai/sam-3/3d-body");
        _aiCacheMock.SetupGet(x => x.FalAiObjectsModelId).Returns("fal-ai/sam-3/3d-objects");
        _aiCacheMock.SetupGet(x => x.FalAiAlignModelId).Returns("fal-ai/sam-3/3d-align");
        _aiCacheMock.SetupGet(x => x.TryOn2DModelId).Returns("fal-ai/fashn/tryon");
        _aiCacheMock.SetupGet(x => x.FailedRetryWindowHours).Returns(1);
        _aiCacheMock.Setup(x => x.ComputeTryOn3DHash(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<double>(), It.IsAny<string>(),
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("testhash3d");
        _aiCacheMock.Setup(x => x.ComputeTryOn2DHash(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("testhash2d");

        _sut = new InitiateTryOnCommandHandler(
            _contextMock.Object,
            _currentUserServiceMock.Object,
            _tryOnServiceMock.Object,
            _tryOn2DServiceMock.Object,
            _cacheServiceMock.Object,
            _aiCacheMock.Object);

        _cacheServiceMock
            .Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _cacheServiceMock
            .Setup(x => x.RemoveByPrefixAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _cacheServiceMock
            .Setup(x => x.RemoveByPatternAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _currentUserServiceMock.SetupGet(x => x.CustomerId).Returns(CustomerId);
    }

    private static Domain.Entities.Retailer.Product CreateActiveProduct(Guid productId)
    {
        var product = Domain.Entities.Retailer.Product.Create(RetailerId, "Test Product", null, null, null, 99.99m, "EGP", null, ProductStatus.Active);
        typeof(Domain.Common.BaseEntity).GetProperty(nameof(Domain.Common.BaseEntity.Id))!.SetValue(product, productId);
        return product;
    }

    private static Domain.Entities.Customer.Avatar CreateAvatar(Guid customerId, Guid avatarId)
    {
        var avatar = Domain.Entities.Customer.Avatar.Create(customerId, 175m, 70m);
        typeof(Domain.Common.BaseEntity).GetProperty(nameof(Domain.Common.BaseEntity.Id))!.SetValue(avatar, avatarId);
        return avatar;
    }

    private static Domain.Entities.Customer.Avatar CreateAvatarWith3D(Guid customerId, Guid avatarId)
    {
        var avatar = Domain.Entities.Customer.Avatar.Create(
            customerId, 175m, 70m,
            sourceImageUrl: "https://cdn.example.com/person.jpg",
            avatar3dModelUrl: "https://fal-storage.com/body.glb");
        typeof(Domain.Common.BaseEntity).GetProperty(nameof(Domain.Common.BaseEntity.Id))!.SetValue(avatar, avatarId);
        return avatar;
    }

    private static TryOnResultDto CompletedResult() =>
        new(SessionStatus.Completed, "https://cdn.example.com/result.jpg", "M", 0.95m, 5);

    private static TryOnResultDto FailedResult() =>
        new(SessionStatus.Failed, null, null, null, null);

    // ── Basic validation ──────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_CustomerIdIsNull_ThrowsUnauthorizedException()
    {
        _currentUserServiceMock.SetupGet(x => x.CustomerId).Returns((Guid?)null);

        var act = () => _sut.Handle(new InitiateTryOnCommand(Guid.NewGuid(), TryOnSessionType.Overlay2D, null), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_ProductNotFound_ThrowsNotFoundException()
    {
        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Domain.Entities.Retailer.Product>());
        _contextMock.Setup(x => x.VirtualTryOnSessions).ReturnsDbSet(new List<Domain.Entities.Customer.VirtualTryOnSession>());

        var act = () => _sut.Handle(new InitiateTryOnCommand(Guid.NewGuid(), TryOnSessionType.Overlay2D, null), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_AvatarNotFound_ThrowsNotFoundException()
    {
        var productId = Guid.NewGuid();
        var product = CreateActiveProduct(productId);

        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Domain.Entities.Retailer.Product> { product });
        _contextMock.Setup(x => x.Avatars).ReturnsDbSet(new List<Domain.Entities.Customer.Avatar>());
        _contextMock.Setup(x => x.VirtualTryOnSessions).ReturnsDbSet(new List<Domain.Entities.Customer.VirtualTryOnSession>());

        var act = () => _sut.Handle(new InitiateTryOnCommand(productId, TryOnSessionType.Overlay2D, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_AvatarBelongsToDifferentCustomer_ThrowsNotFoundException()
    {
        var productId = Guid.NewGuid();
        var avatarId = Guid.NewGuid();
        var product = CreateActiveProduct(productId);
        var avatar = CreateAvatar(Guid.NewGuid(), avatarId); // different customer

        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Domain.Entities.Retailer.Product> { product });
        _contextMock.Setup(x => x.Avatars).ReturnsDbSet(new List<Domain.Entities.Customer.Avatar> { avatar });
        _contextMock.Setup(x => x.VirtualTryOnSessions).ReturnsDbSet(new List<Domain.Entities.Customer.VirtualTryOnSession>());

        var act = () => _sut.Handle(new InitiateTryOnCommand(productId, TryOnSessionType.Overlay2D, avatarId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── Issue 12 fix: ProductStatus.Active constant ───────────────────────────

    [Fact]
    public async Task Handle_InactiveProduct_ThrowsNotFoundException()
    {
        var productId = Guid.NewGuid();
        var product = Domain.Entities.Retailer.Product.Create(RetailerId, "Inactive", null, null, null, 10m, "EGP", null, ProductStatus.Inactive);
        typeof(Domain.Common.BaseEntity).GetProperty(nameof(Domain.Common.BaseEntity.Id))!.SetValue(product, productId);

        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Domain.Entities.Retailer.Product> { product });
        _contextMock.Setup(x => x.VirtualTryOnSessions).ReturnsDbSet(new List<Domain.Entities.Customer.VirtualTryOnSession>());

        var act = () => _sut.Handle(new InitiateTryOnCommand(productId, TryOnSessionType.Model3D, null), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── Overlay2D path ────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_Overlay2D_WithoutAvatar_ThrowsBusinessRuleException_BeforeSessionSave()
    {
        var productId = Guid.NewGuid();
        var product = CreateActiveProduct(productId);

        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Domain.Entities.Retailer.Product> { product });
        _contextMock.Setup(x => x.VirtualTryOnSessions).ReturnsDbSet(new List<Domain.Entities.Customer.VirtualTryOnSession>());

        var act = () => _sut.Handle(new InitiateTryOnCommand(productId, TryOnSessionType.Overlay2D, null), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .Where(e => e.Code == "TryOn2DAvatarRequired");

        // No session should be persisted for a predictable business-rule error.
        _contextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Overlay2D_AvatarWithoutSourceImage_ThrowsBusinessRuleException_BeforeSessionSave()
    {
        var productId = Guid.NewGuid();
        var avatarId = Guid.NewGuid();
        var product = CreateActiveProduct(productId);
        var avatar = CreateAvatar(CustomerId, avatarId); // no SourceImageUrl

        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Domain.Entities.Retailer.Product> { product });
        _contextMock.Setup(x => x.Avatars).ReturnsDbSet(new List<Domain.Entities.Customer.Avatar> { avatar });
        _contextMock.Setup(x => x.VirtualTryOnSessions).ReturnsDbSet(new List<Domain.Entities.Customer.VirtualTryOnSession>());

        var act = () => _sut.Handle(new InitiateTryOnCommand(productId, TryOnSessionType.Overlay2D, avatarId), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .Where(e => e.Code == "TryOn2DSourceImageMissing");

        _contextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Issue 10 fix: 3D validation before session persist ────────────────────

    [Fact]
    public async Task Handle_Model3D_WithoutAvatar_ThrowsBusinessRuleException_BeforeSessionSave()
    {
        var productId = Guid.NewGuid();
        var product = CreateActiveProduct(productId);

        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Domain.Entities.Retailer.Product> { product });
        _contextMock.Setup(x => x.VirtualTryOnSessions).ReturnsDbSet(new List<Domain.Entities.Customer.VirtualTryOnSession>());

        var act = () => _sut.Handle(new InitiateTryOnCommand(productId, TryOnSessionType.Model3D, null), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .Where(e => e.Code == "TryOn3DAvatarRequired");

        // Crucial: SaveChangesAsync must NOT have been called — no spurious Failed row.
        _contextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Model3D_AvatarWithout3DModel_ThrowsBusinessRuleException_BeforeSessionSave()
    {
        var productId = Guid.NewGuid();
        var avatarId = Guid.NewGuid();
        var product = CreateActiveProduct(productId);
        var avatar = CreateAvatar(CustomerId, avatarId); // no Avatar3dModelUrl

        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Domain.Entities.Retailer.Product> { product });
        _contextMock.Setup(x => x.Avatars).ReturnsDbSet(new List<Domain.Entities.Customer.Avatar> { avatar });
        _contextMock.Setup(x => x.VirtualTryOnSessions).ReturnsDbSet(new List<Domain.Entities.Customer.VirtualTryOnSession>());

        var act = () => _sut.Handle(new InitiateTryOnCommand(productId, TryOnSessionType.Model3D, avatarId), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .Where(e => e.Code == "TryOn3DAvatarRequired");

        _contextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Model3D_AvatarWith3DModelButNoSourceImage_ThrowsBusinessRuleException_BeforeSessionSave()
    {
        var productId = Guid.NewGuid();
        var avatarId = Guid.NewGuid();
        var product = CreateActiveProduct(productId);

        var avatar = Domain.Entities.Customer.Avatar.Create(
            CustomerId, 175m, 70m, avatar3dModelUrl: "https://fal.run/body.glb");
        typeof(Domain.Common.BaseEntity).GetProperty(nameof(Domain.Common.BaseEntity.Id))!.SetValue(avatar, avatarId);

        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Domain.Entities.Retailer.Product> { product });
        _contextMock.Setup(x => x.Avatars).ReturnsDbSet(new List<Domain.Entities.Customer.Avatar> { avatar });
        _contextMock.Setup(x => x.VirtualTryOnSessions).ReturnsDbSet(new List<Domain.Entities.Customer.VirtualTryOnSession>());

        var act = () => _sut.Handle(new InitiateTryOnCommand(productId, TryOnSessionType.Model3D, avatarId), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .Where(e => e.Code == "TryOn3DSourceImageMissing");

        _contextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Model3D_ValidAvatarWith3DAndSourceImage_CallsServiceAndPersistsSession()
    {
        var productId = Guid.NewGuid();
        var avatarId = Guid.NewGuid();
        var product = CreateActiveProduct(productId);
        var avatar = CreateAvatarWith3D(CustomerId, avatarId);

        var productImage = Domain.Entities.Retailer.ProductImage.Create(productId, "https://cdn.example.com/product.jpg", 0);

        Domain.Entities.Customer.VirtualTryOnSession? capturedSession = null;
        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Domain.Entities.Retailer.Product> { product });
        _contextMock.Setup(x => x.Avatars).ReturnsDbSet(new List<Domain.Entities.Customer.Avatar> { avatar });
        _contextMock.Setup(x => x.ProductImages).ReturnsDbSet(new List<Domain.Entities.Retailer.ProductImage> { productImage });

        _contextMock.Setup(x => x.VirtualTryOnSessions).ReturnsDbSet(new List<Domain.Entities.Customer.VirtualTryOnSession>());
        _contextMock.Setup(x => x.VirtualTryOnSessions.Add(It.IsAny<Domain.Entities.Customer.VirtualTryOnSession>()))
            .Callback<Domain.Entities.Customer.VirtualTryOnSession>(s => capturedSession = s);
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _tryOnServiceMock.Setup(x => x.ProcessTryOnAsync(CustomerId, productId, TryOnSessionType.Model3D, avatar, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompletedResult());

        var result = await _sut.Handle(new InitiateTryOnCommand(productId, TryOnSessionType.Model3D, avatarId), CancellationToken.None);

        result.Status.Should().Be(SessionStatus.Completed);
        capturedSession.Should().NotBeNull();
        capturedSession!.SessionType.Should().Be(TryOnSessionType.Model3D);
    }

    [Fact]
    public async Task Handle_ServiceReturnsFailedResult_MarksSessionAsFailed()
    {
        var productId = Guid.NewGuid();
        var avatarId = Guid.NewGuid();
        var product = CreateActiveProduct(productId);
        var avatar = CreateAvatarWith3D(CustomerId, avatarId);

        var productImage = Domain.Entities.Retailer.ProductImage.Create(productId, "https://cdn.example.com/product.jpg", 0);

        Domain.Entities.Customer.VirtualTryOnSession? capturedSession = null;
        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Domain.Entities.Retailer.Product> { product });
        _contextMock.Setup(x => x.Avatars).ReturnsDbSet(new List<Domain.Entities.Customer.Avatar> { avatar });
        _contextMock.Setup(x => x.ProductImages).ReturnsDbSet(new List<Domain.Entities.Retailer.ProductImage> { productImage });

        _contextMock.Setup(x => x.VirtualTryOnSessions).ReturnsDbSet(new List<Domain.Entities.Customer.VirtualTryOnSession>());
        _contextMock.Setup(x => x.VirtualTryOnSessions.Add(It.IsAny<Domain.Entities.Customer.VirtualTryOnSession>()))
            .Callback<Domain.Entities.Customer.VirtualTryOnSession>(s => capturedSession = s);
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _tryOnServiceMock.Setup(x => x.ProcessTryOnAsync(CustomerId, productId, TryOnSessionType.Model3D, avatar, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FailedResult());

        var result = await _sut.Handle(new InitiateTryOnCommand(productId, TryOnSessionType.Model3D, avatarId), CancellationToken.None);

        result.Status.Should().Be(SessionStatus.Failed);
        capturedSession.Should().NotBeNull();
        capturedSession!.Status.Should().Be(SessionStatus.Failed);
    }

    [Fact]
    public async Task Handle_ServiceThrowsException_MarksSessionAsFailedAndRethrows()
    {
        var productId = Guid.NewGuid();
        var avatarId = Guid.NewGuid();
        var product = CreateActiveProduct(productId);
        var avatar = CreateAvatarWith3D(CustomerId, avatarId);

        var productImage = Domain.Entities.Retailer.ProductImage.Create(productId, "https://cdn.example.com/product.jpg", 0);

        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Domain.Entities.Retailer.Product> { product });
        _contextMock.Setup(x => x.Avatars).ReturnsDbSet(new List<Domain.Entities.Customer.Avatar> { avatar });
        _contextMock.Setup(x => x.ProductImages).ReturnsDbSet(new List<Domain.Entities.Retailer.ProductImage> { productImage });

        _contextMock.Setup(x => x.VirtualTryOnSessions).ReturnsDbSet(new List<Domain.Entities.Customer.VirtualTryOnSession>());
        _contextMock.Setup(x => x.VirtualTryOnSessions.Add(It.IsAny<Domain.Entities.Customer.VirtualTryOnSession>()));
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _tryOnServiceMock.Setup(x => x.ProcessTryOnAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<TryOnSessionType>(), It.IsAny<Domain.Entities.Customer.Avatar?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("External service error"));

        var act = () => _sut.Handle(new InitiateTryOnCommand(productId, TryOnSessionType.Model3D, avatarId), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _contextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeast(2));
    }

    [Fact]
    public async Task Handle_ValidRequest_CreatesSessionWithCorrectProperties()
    {
        var productId = Guid.NewGuid();
        var avatarId = Guid.NewGuid();
        var product = CreateActiveProduct(productId);
        var avatar = CreateAvatarWith3D(CustomerId, avatarId);

        var productImage = Domain.Entities.Retailer.ProductImage.Create(productId, "https://cdn.example.com/product.jpg", 0);

        Domain.Entities.Customer.VirtualTryOnSession? capturedSession = null;
        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Domain.Entities.Retailer.Product> { product });
        _contextMock.Setup(x => x.Avatars).ReturnsDbSet(new List<Domain.Entities.Customer.Avatar> { avatar });
        _contextMock.Setup(x => x.ProductImages).ReturnsDbSet(new List<Domain.Entities.Retailer.ProductImage> { productImage });

        _contextMock.Setup(x => x.VirtualTryOnSessions).ReturnsDbSet(new List<Domain.Entities.Customer.VirtualTryOnSession>());
        _contextMock.Setup(x => x.VirtualTryOnSessions.Add(It.IsAny<Domain.Entities.Customer.VirtualTryOnSession>()))
            .Callback<Domain.Entities.Customer.VirtualTryOnSession>(s => capturedSession = s);
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _tryOnServiceMock.Setup(x => x.ProcessTryOnAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<TryOnSessionType>(), It.IsAny<Domain.Entities.Customer.Avatar?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompletedResult());

        await _sut.Handle(new InitiateTryOnCommand(productId, TryOnSessionType.ARLiveView, avatarId), CancellationToken.None);

        capturedSession.Should().NotBeNull();
        capturedSession!.CustomerId.Should().Be(CustomerId);
        capturedSession.ProductId.Should().Be(productId);
        capturedSession.SessionType.Should().Be(TryOnSessionType.ARLiveView);
    }

    // ── AI generation cache tests ─────────────────────────────────────────────

    [Fact]
    public async Task Handle_CacheHitProcessing_ThrowsBusinessRuleException()
    {
        var productId = Guid.NewGuid();
        var avatarId = Guid.NewGuid();
        var product = CreateActiveProduct(productId);
        var avatar = CreateAvatarWith3D(CustomerId, avatarId);
        var productImage = Domain.Entities.Retailer.ProductImage.Create(productId, "https://cdn.example.com/product.jpg", 0);

        var processingEntry = AiGenerationCache.CreateProcessing(CustomerId, "testhash3d", AiGenerationType.TryOn3D, "FalAi", "model", "v1", "{}");

        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Domain.Entities.Retailer.Product> { product });
        _contextMock.Setup(x => x.Avatars).ReturnsDbSet(new List<Domain.Entities.Customer.Avatar> { avatar });
        _contextMock.Setup(x => x.ProductImages).ReturnsDbSet(new List<Domain.Entities.Retailer.ProductImage> { productImage });

        _contextMock.Setup(x => x.VirtualTryOnSessions).ReturnsDbSet(new List<Domain.Entities.Customer.VirtualTryOnSession>());

        _aiCacheMock.Setup(x => x.GetByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(processingEntry);

        var act = () => _sut.Handle(new InitiateTryOnCommand(productId, TryOnSessionType.Model3D, avatarId), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .Where(e => e.Code == "AI_GENERATION_IN_PROGRESS");
    }

    [Fact]
    public async Task Handle_QuotaExceeded_ThrowsBusinessRuleException()
    {
        var productId = Guid.NewGuid();
        var avatarId = Guid.NewGuid();
        var product = CreateActiveProduct(productId);
        var avatar = CreateAvatarWith3D(CustomerId, avatarId);
        var productImage = Domain.Entities.Retailer.ProductImage.Create(productId, "https://cdn.example.com/product.jpg", 0);

        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Domain.Entities.Retailer.Product> { product });
        _contextMock.Setup(x => x.Avatars).ReturnsDbSet(new List<Domain.Entities.Customer.Avatar> { avatar });
        _contextMock.Setup(x => x.ProductImages).ReturnsDbSet(new List<Domain.Entities.Retailer.ProductImage> { productImage });

        _contextMock.Setup(x => x.VirtualTryOnSessions).ReturnsDbSet(new List<Domain.Entities.Customer.VirtualTryOnSession>());

        _aiCacheMock.Setup(x => x.IsTryOnQuotaExceededAsync(CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = () => _sut.Handle(new InitiateTryOnCommand(productId, TryOnSessionType.Model3D, avatarId), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .Where(e => e.Code == "AI_GENERATION_QUOTA_EXCEEDED");

        _contextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
