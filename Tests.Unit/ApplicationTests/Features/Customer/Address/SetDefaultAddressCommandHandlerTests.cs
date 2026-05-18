using Application.Features.Customer.Address.Commands.SetDefaultAddress;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace Tests.Unit.Application.Features.Customer.Address;

public sealed class SetDefaultAddressCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<ILogger<SetDefaultAddressCommandHandler>> _loggerMock = new();
    private readonly Mock<IRepository<CustomerAddress>> _addressRepoMock = new();
    private readonly SetDefaultAddressCommandHandler _sut;

    private static readonly Guid CustomerId = Guid.NewGuid();

    public SetDefaultAddressCommandHandlerTests()
    {
        _sut = new SetDefaultAddressCommandHandler(
            _unitOfWorkMock.Object,
            _currentUserServiceMock.Object,
            _loggerMock.Object);

        _currentUserServiceMock.SetupGet(x => x.CustomerId).Returns(CustomerId);
        _unitOfWorkMock.Setup(x => x.Repository<CustomerAddress>()).Returns(_addressRepoMock.Object);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _unitOfWorkMock.Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task> op, CancellationToken ct) => op(ct));
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

        var act = () => _sut.Handle(new SetDefaultAddressCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_AddressNotFound_ThrowsNotFoundException()
    {
        _addressRepoMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerAddress?)null);

        var act = () => _sut.Handle(new SetDefaultAddressCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_AddressSoftDeleted_ThrowsNotFoundException()
    {
        var addressId = Guid.NewGuid();
        var address = CreateAddress(addressId, isDeleted: true);

        _addressRepoMock.Setup(x => x.GetByIdAsync(addressId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(address);

        var act = () => _sut.Handle(new SetDefaultAddressCommand(addressId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_AddressBelongsToDifferentCustomer_ThrowsNotFoundException()
    {
        var addressId = Guid.NewGuid();
        var address = CreateAddress(addressId, customerId: Guid.NewGuid());

        _addressRepoMock.Setup(x => x.GetByIdAsync(addressId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(address);

        var act = () => _sut.Handle(new SetDefaultAddressCommand(addressId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_AddressAlreadyDefault_ReturnsSuccessWithoutTransaction()
    {
        var addressId = Guid.NewGuid();
        var address = CreateAddress(addressId, isDefault: true);

        _addressRepoMock.Setup(x => x.GetByIdAsync(addressId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(address);

        var result = await _sut.Handle(new SetDefaultAddressCommand(addressId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeTrue();
        _unitOfWorkMock.Verify(x => x.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ValidRequest_UnsetsPreviousDefaultAndSetsNewDefault()
    {
        var newDefaultId = Guid.NewGuid();
        var newDefault = CreateAddress(newDefaultId, isDefault: false);
        var previousDefault = CreateAddress(Guid.NewGuid(), isDefault: true);

        _addressRepoMock.Setup(x => x.GetByIdAsync(newDefaultId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(newDefault);
        _addressRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<CustomerAddress, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(previousDefault);
        _addressRepoMock.Setup(x => x.UpdateAsync(It.IsAny<CustomerAddress>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.Handle(new SetDefaultAddressCommand(newDefaultId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        previousDefault.IsDefault.Should().BeFalse();
        newDefault.IsDefault.Should().BeTrue();
        _addressRepoMock.Verify(x => x.UpdateAsync(previousDefault, It.IsAny<CancellationToken>()), Times.Once);
        _addressRepoMock.Verify(x => x.UpdateAsync(newDefault, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidRequestNoPreviousDefault_SetsNewDefault()
    {
        var newDefaultId = Guid.NewGuid();
        var newDefault = CreateAddress(newDefaultId, isDefault: false);

        _addressRepoMock.Setup(x => x.GetByIdAsync(newDefaultId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(newDefault);
        _addressRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<CustomerAddress, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerAddress?)null);
        _addressRepoMock.Setup(x => x.UpdateAsync(It.IsAny<CustomerAddress>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.Handle(new SetDefaultAddressCommand(newDefaultId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        newDefault.IsDefault.Should().BeTrue();
        _addressRepoMock.Verify(x => x.UpdateAsync(newDefault, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsSuccessMessage()
    {
        var addressId = Guid.NewGuid();
        var address = CreateAddress(addressId);

        _addressRepoMock.Setup(x => x.GetByIdAsync(addressId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(address);
        _addressRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<CustomerAddress, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerAddress?)null);
        _addressRepoMock.Setup(x => x.UpdateAsync(It.IsAny<CustomerAddress>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.Handle(new SetDefaultAddressCommand(addressId), CancellationToken.None);

        result.Message.Should().Contain("default");
    }

    [Fact]
    public async Task Handle_ValidRequest_ExecutesInTransaction()
    {
        var addressId = Guid.NewGuid();
        var address = CreateAddress(addressId);

        _addressRepoMock.Setup(x => x.GetByIdAsync(addressId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(address);
        _addressRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<CustomerAddress, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerAddress?)null);
        _addressRepoMock.Setup(x => x.UpdateAsync(It.IsAny<CustomerAddress>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _sut.Handle(new SetDefaultAddressCommand(addressId), CancellationToken.None);

        _unitOfWorkMock.Verify(x => x.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}