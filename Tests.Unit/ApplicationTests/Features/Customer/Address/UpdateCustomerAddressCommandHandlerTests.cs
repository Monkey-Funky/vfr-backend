using Application.Features.Customer.Address.Commands.UpdateAddress;
using Application.Features.Customer.Address.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace Tests.Unit.Application.Features.Customer.Address;

public sealed class UpdateCustomerAddressCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<ILogger<UpdateCustomerAddressCommandHandler>> _loggerMock = new();
    private readonly Mock<ICacheService> _cacheServiceMock = new();
    private readonly Mock<IRepository<CustomerAddress>> _addressRepoMock = new();
    private readonly UpdateCustomerAddressCommandHandler _sut;

    private static readonly Guid CustomerId = Guid.NewGuid();

    public UpdateCustomerAddressCommandHandlerTests()
    {
        _sut = new UpdateCustomerAddressCommandHandler(
            _unitOfWorkMock.Object,
            _currentUserServiceMock.Object,
            _cacheServiceMock.Object,
            _loggerMock.Object);
        // Cache miss for all GetAsync calls — Moq returns Task<T?> default (null)
        // which simulates a cache miss so the handler always exercises the DB path.
        // RemoveAsync / RemoveByPrefixAsync are stubbed to complete successfully.
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
        _unitOfWorkMock.Setup(x => x.Repository<CustomerAddress>()).Returns(_addressRepoMock.Object);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    private static UpdateCustomerAddressCommand ValidCommand(Guid addressId) =>
        new(addressId, "Home", "999 New St", "Apt 5", "Cairo", "Cairo Governorate", "11511", "Egypt");

    private CustomerAddress CreateAddress(Guid addressId, bool isDeleted = false)
    {
        var address = CustomerAddress.Create(CustomerId, "Old Label", "Old Street", null, "Old City", null, "00000", "Egypt");
        typeof(Domain.Common.BaseEntity).GetProperty(nameof(Domain.Common.BaseEntity.Id))!.SetValue(address, addressId);
        if (isDeleted) address.MarkAsDeleted();
        return address;
    }

    [Fact]
    public async Task Handle_CustomerIdIsNull_ThrowsUnauthorizedException()
    {
        _currentUserServiceMock.SetupGet(x => x.CustomerId).Returns((Guid?)null);

        var act = () => _sut.Handle(ValidCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_AddressNotFound_ThrowsNotFoundException()
    {
        _addressRepoMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerAddress?)null);

        var act = () => _sut.Handle(ValidCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_AddressSoftDeleted_ThrowsNotFoundException()
    {
        var addressId = Guid.NewGuid();
        var address = CreateAddress(addressId, isDeleted: true);

        _addressRepoMock.Setup(x => x.GetByIdAsync(addressId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(address);

        var act = () => _sut.Handle(ValidCommand(addressId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_AddressBelongsToDifferentCustomer_ThrowsNotFoundException()
    {
        var addressId = Guid.NewGuid();
        var address = CustomerAddress.Create(Guid.NewGuid(), "Work", "123 St", null, "Alexandria", null, "21500", "Egypt");
        typeof(Domain.Common.BaseEntity).GetProperty(nameof(Domain.Common.BaseEntity.Id))!.SetValue(address, addressId);

        _addressRepoMock.Setup(x => x.GetByIdAsync(addressId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(address);

        var act = () => _sut.Handle(ValidCommand(addressId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ValidRequest_UpdatesAddressFields()
    {
        var addressId = Guid.NewGuid();
        var address = CreateAddress(addressId);

        _addressRepoMock.Setup(x => x.GetByIdAsync(addressId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(address);
        _addressRepoMock.Setup(x => x.UpdateAsync(It.IsAny<CustomerAddress>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.Handle(ValidCommand(addressId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Label.Should().Be("Home");
        result.Data.AddressLine1.Should().Be("999 New St");
        result.Data.AddressLine2.Should().Be("Apt 5");
        result.Data.City.Should().Be("Cairo");
        result.Data.PostalCode.Should().Be("11511");
        result.Data.Country.Should().Be("Egypt");
    }

    [Fact]
    public async Task Handle_ValidRequest_CallsUpdateAndSaveOnce()
    {
        var addressId = Guid.NewGuid();
        var address = CreateAddress(addressId);

        _addressRepoMock.Setup(x => x.GetByIdAsync(addressId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(address);
        _addressRepoMock.Setup(x => x.UpdateAsync(It.IsAny<CustomerAddress>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _sut.Handle(ValidCommand(addressId), CancellationToken.None);

        _addressRepoMock.Verify(x => x.UpdateAsync(address, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsSuccessMessage()
    {
        var addressId = Guid.NewGuid();
        var address = CreateAddress(addressId);

        _addressRepoMock.Setup(x => x.GetByIdAsync(addressId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(address);
        _addressRepoMock.Setup(x => x.UpdateAsync(It.IsAny<CustomerAddress>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.Handle(ValidCommand(addressId), CancellationToken.None);

        result.Message.Should().Contain("updated");
    }
}