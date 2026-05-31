using Application.Features.Customer.Address.Commands.DeleteAddress;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace Tests.Unit.Application.Features.Customer.Address;

public sealed class DeleteCustomerAddressCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<ILogger<DeleteCustomerAddressCommandHandler>> _loggerMock = new();
    private readonly Mock<ICacheService> _cacheServiceMock = new();
    private readonly Mock<IRepository<CustomerAddress>> _addressRepoMock = new();
    private readonly DeleteCustomerAddressCommandHandler _sut;

    private static readonly Guid CustomerId = Guid.NewGuid();

    public DeleteCustomerAddressCommandHandlerTests()
    {
        _sut = new DeleteCustomerAddressCommandHandler(
            _unitOfWorkMock.Object,
            _currentUserServiceMock.Object,
            _cacheServiceMock.Object,
            _loggerMock.Object);

        _currentUserServiceMock.SetupGet(x => x.CustomerId).Returns(CustomerId);
        _unitOfWorkMock.Setup(x => x.Repository<CustomerAddress>()).Returns(_addressRepoMock.Object);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    private CustomerAddress CreateAddress(Guid addressId, bool isDefault = false, bool isDeleted = false, Guid? customerId = null)
    {
        var address = CustomerAddress.Create(customerId ?? CustomerId, "Home", "123 Street", null, "Cairo", null, "11511", "Egypt", isDefault);
        typeof(Domain.Common.BaseEntity).GetProperty(nameof(Domain.Common.BaseEntity.Id))!.SetValue(address, addressId);
        if (isDeleted) address.MarkAsDeleted();
        return address;
    }

    [Fact]
    public async Task Handle_CustomerIdIsNull_ThrowsUnauthorizedException()
    {
        _currentUserServiceMock.SetupGet(x => x.CustomerId).Returns((Guid?)null);

        var act = () => _sut.Handle(new DeleteCustomerAddressCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_AddressNotFound_ThrowsNotFoundException()
    {
        _addressRepoMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerAddress?)null);

        var act = () => _sut.Handle(new DeleteCustomerAddressCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_AddressSoftDeleted_ThrowsNotFoundException()
    {
        var addressId = Guid.NewGuid();
        var address = CreateAddress(addressId, isDeleted: true);

        _addressRepoMock.Setup(x => x.GetByIdAsync(addressId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(address);

        var act = () => _sut.Handle(new DeleteCustomerAddressCommand(addressId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_AddressBelongsToDifferentCustomer_ThrowsNotFoundException()
    {
        var addressId = Guid.NewGuid();
        var address = CreateAddress(addressId, customerId: Guid.NewGuid());

        _addressRepoMock.Setup(x => x.GetByIdAsync(addressId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(address);

        var act = () => _sut.Handle(new DeleteCustomerAddressCommand(addressId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_DefaultAddress_ThrowsBusinessRuleException()
    {
        var addressId = Guid.NewGuid();
        var address = CreateAddress(addressId, isDefault: true);

        _addressRepoMock.Setup(x => x.GetByIdAsync(addressId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(address);

        var act = () => _sut.Handle(new DeleteCustomerAddressCommand(addressId), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("CANNOT_DELETE_DEFAULT_ADDRESS");
    }

    [Fact]
    public async Task Handle_ValidNonDefaultAddress_DeletesAddress()
    {
        var addressId = Guid.NewGuid();
        var address = CreateAddress(addressId, isDefault: false);

        _addressRepoMock.Setup(x => x.GetByIdAsync(addressId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(address);
        _addressRepoMock.Setup(x => x.DeleteAsync(It.IsAny<CustomerAddress>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.Handle(new DeleteCustomerAddressCommand(addressId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeTrue();
        _addressRepoMock.Verify(x => x.DeleteAsync(address, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsSuccessMessage()
    {
        var addressId = Guid.NewGuid();
        var address = CreateAddress(addressId);

        _addressRepoMock.Setup(x => x.GetByIdAsync(addressId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(address);
        _addressRepoMock.Setup(x => x.DeleteAsync(It.IsAny<CustomerAddress>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.Handle(new DeleteCustomerAddressCommand(addressId), CancellationToken.None);

        result.Message.Should().Contain("deleted");
    }
}