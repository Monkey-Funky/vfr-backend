using Application.Features.Customer.VirtualTryOn.Commands.InitiateTryOn;
using Application.Features.Customer.VirtualTryOn.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Application.Interfaces.Services.Customer;
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
    private readonly InitiateTryOnCommandHandler _sut;

    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid RetailerId = Guid.NewGuid();

    public InitiateTryOnCommandHandlerTests()
    {
        _sut = new InitiateTryOnCommandHandler(
            _contextMock.Object,
            _currentUserServiceMock.Object,
            _tryOnServiceMock.Object,
            _tryOn2DServiceMock.Object,
            _cacheServiceMock.Object);

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
        // Create an inactive product
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
        // FIX (Issue 10): The 3D avatar check must happen BEFORE the session row is
        // persisted so that predictable user-state errors don't pollute the sessions table.
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
        // FIX (Issue 10): Avatar exists but has no 3D model — still a pre-persist check.
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
        // FIX (Issues 7 & 10): Avatar has a 3D model but no SourceImageUrl.
        // The 3D align step REQUIRES the person's photo — we must fail before persist.
        var productId = Guid.NewGuid();
        var avatarId = Guid.NewGuid();
        var product = CreateActiveProduct(productId);

        // Avatar with 3D model but no source image
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

        Domain.Entities.Customer.VirtualTryOnSession? capturedSession = null;
        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Domain.Entities.Retailer.Product> { product });
        _contextMock.Setup(x => x.Avatars).ReturnsDbSet(new List<Domain.Entities.Customer.Avatar> { avatar });
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

        Domain.Entities.Customer.VirtualTryOnSession? capturedSession = null;
        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Domain.Entities.Retailer.Product> { product });
        _contextMock.Setup(x => x.Avatars).ReturnsDbSet(new List<Domain.Entities.Customer.Avatar> { avatar });
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

        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Domain.Entities.Retailer.Product> { product });
        _contextMock.Setup(x => x.Avatars).ReturnsDbSet(new List<Domain.Entities.Customer.Avatar> { avatar });
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

        Domain.Entities.Customer.VirtualTryOnSession? capturedSession = null;
        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Domain.Entities.Retailer.Product> { product });
        _contextMock.Setup(x => x.Avatars).ReturnsDbSet(new List<Domain.Entities.Customer.Avatar> { avatar });
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
}
