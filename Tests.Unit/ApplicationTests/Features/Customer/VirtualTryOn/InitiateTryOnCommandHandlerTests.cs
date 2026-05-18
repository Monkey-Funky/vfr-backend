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
    private readonly InitiateTryOnCommandHandler _sut;

    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid RetailerId = Guid.NewGuid();

    public InitiateTryOnCommandHandlerTests()
    {
        _sut = new InitiateTryOnCommandHandler(
            _contextMock.Object,
            _currentUserServiceMock.Object,
            _tryOnServiceMock.Object);

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

    private static TryOnResultDto CompletedResult() =>
        new(SessionStatus.Completed, "https://cdn.example.com/result.jpg", "M", 0.95m, 5);

    private static TryOnResultDto FailedResult() =>
        new(SessionStatus.Failed, null, null, null, null);

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

        var act = () => _sut.Handle(new InitiateTryOnCommand(Guid.NewGuid(), TryOnSessionType.Overlay2D, null), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ProductNotActive_ThrowsNotFoundException()
    {
        var productId = Guid.NewGuid();
        var product = Domain.Entities.Retailer.Product.Create(RetailerId, "Shirt", null, null, null, null, "EGP", null, ProductStatus.Draft);
        typeof(Domain.Common.BaseEntity).GetProperty(nameof(Domain.Common.BaseEntity.Id))!.SetValue(product, productId);

        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Domain.Entities.Retailer.Product> { product });

        var act = () => _sut.Handle(new InitiateTryOnCommand(productId, TryOnSessionType.Overlay2D, null), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_AvatarProvidedButNotFound_ThrowsNotFoundException()
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
        var avatar = CreateAvatar(Guid.NewGuid(), avatarId);

        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Domain.Entities.Retailer.Product> { product });
        _contextMock.Setup(x => x.Avatars).ReturnsDbSet(new List<Domain.Entities.Customer.Avatar> { avatar });
        _contextMock.Setup(x => x.VirtualTryOnSessions).ReturnsDbSet(new List<Domain.Entities.Customer.VirtualTryOnSession>());

        var act = () => _sut.Handle(new InitiateTryOnCommand(productId, TryOnSessionType.Overlay2D, avatarId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ValidRequest_WithoutAvatar_ReturnsTryOnResult()
    {
        var productId = Guid.NewGuid();
        var product = CreateActiveProduct(productId);

        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Domain.Entities.Retailer.Product> { product });
        _contextMock.Setup(x => x.VirtualTryOnSessions).ReturnsDbSet(new List<Domain.Entities.Customer.VirtualTryOnSession>());
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _tryOnServiceMock.Setup(x => x.ProcessTryOnAsync(CustomerId, productId, TryOnSessionType.Overlay2D, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompletedResult());

        var result = await _sut.Handle(new InitiateTryOnCommand(productId, TryOnSessionType.Overlay2D, null), CancellationToken.None);

        result.Should().NotBeNull();
        result.Status.Should().Be(SessionStatus.Completed);
        result.ResultImageUrl.Should().Be("https://cdn.example.com/result.jpg");
    }

    [Fact]
    public async Task Handle_ValidRequest_WithAvatar_ReturnsTryOnResult()
    {
        var productId = Guid.NewGuid();
        var avatarId = Guid.NewGuid();
        var product = CreateActiveProduct(productId);
        var avatar = CreateAvatar(CustomerId, avatarId);

        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Domain.Entities.Retailer.Product> { product });
        _contextMock.Setup(x => x.Avatars).ReturnsDbSet(new List<Domain.Entities.Customer.Avatar> { avatar });
        _contextMock.Setup(x => x.VirtualTryOnSessions).ReturnsDbSet(new List<Domain.Entities.Customer.VirtualTryOnSession>());
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _tryOnServiceMock.Setup(x => x.ProcessTryOnAsync(CustomerId, productId, TryOnSessionType.Model3D, avatar, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompletedResult());

        var result = await _sut.Handle(new InitiateTryOnCommand(productId, TryOnSessionType.Model3D, avatarId), CancellationToken.None);

        result.Status.Should().Be(SessionStatus.Completed);
    }

    [Fact]
    public async Task Handle_ServiceReturnsFailedResult_MarksSessionAsFailed()
    {
        var productId = Guid.NewGuid();
        var product = CreateActiveProduct(productId);

        Domain.Entities.Customer.VirtualTryOnSession? capturedSession = null;
        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Domain.Entities.Retailer.Product> { product });
        _contextMock.Setup(x => x.VirtualTryOnSessions).ReturnsDbSet(new List<Domain.Entities.Customer.VirtualTryOnSession>());
        _contextMock.Setup(x => x.VirtualTryOnSessions.Add(It.IsAny<Domain.Entities.Customer.VirtualTryOnSession>()))
            .Callback<Domain.Entities.Customer.VirtualTryOnSession>(s => capturedSession = s);
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _tryOnServiceMock.Setup(x => x.ProcessTryOnAsync(CustomerId, productId, TryOnSessionType.Overlay2D, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FailedResult());

        var result = await _sut.Handle(new InitiateTryOnCommand(productId, TryOnSessionType.Overlay2D, null), CancellationToken.None);

        result.Status.Should().Be(SessionStatus.Failed);
        capturedSession.Should().NotBeNull();
        capturedSession!.Status.Should().Be(SessionStatus.Failed);
    }

    [Fact]
    public async Task Handle_ServiceThrowsException_MarksSessionAsFailedAndRethrows()
    {
        var productId = Guid.NewGuid();
        var product = CreateActiveProduct(productId);

        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Domain.Entities.Retailer.Product> { product });
        _contextMock.Setup(x => x.VirtualTryOnSessions).ReturnsDbSet(new List<Domain.Entities.Customer.VirtualTryOnSession>());
        _contextMock.Setup(x => x.VirtualTryOnSessions.Add(It.IsAny<Domain.Entities.Customer.VirtualTryOnSession>()));
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _tryOnServiceMock.Setup(x => x.ProcessTryOnAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<TryOnSessionType>(), It.IsAny<Domain.Entities.Customer.Avatar?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("External service error"));

        var act = () => _sut.Handle(new InitiateTryOnCommand(productId, TryOnSessionType.Overlay2D, null), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _contextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeast(2));
    }

    [Fact]
    public async Task Handle_ValidRequest_CreatesSessionWithCorrectProperties()
    {
        var productId = Guid.NewGuid();
        var product = CreateActiveProduct(productId);

        Domain.Entities.Customer.VirtualTryOnSession? capturedSession = null;
        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Domain.Entities.Retailer.Product> { product });
        _contextMock.Setup(x => x.VirtualTryOnSessions).ReturnsDbSet(new List<Domain.Entities.Customer.VirtualTryOnSession>());
        _contextMock.Setup(x => x.VirtualTryOnSessions.Add(It.IsAny<Domain.Entities.Customer.VirtualTryOnSession>()))
            .Callback<Domain.Entities.Customer.VirtualTryOnSession>(s => capturedSession = s);
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _tryOnServiceMock.Setup(x => x.ProcessTryOnAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<TryOnSessionType>(), It.IsAny<Domain.Entities.Customer.Avatar?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompletedResult());

        await _sut.Handle(new InitiateTryOnCommand(productId, TryOnSessionType.ARLiveView, null), CancellationToken.None);

        capturedSession.Should().NotBeNull();
        capturedSession!.CustomerId.Should().Be(CustomerId);
        capturedSession.ProductId.Should().Be(productId);
        capturedSession.SessionType.Should().Be(TryOnSessionType.ARLiveView);
    }
}