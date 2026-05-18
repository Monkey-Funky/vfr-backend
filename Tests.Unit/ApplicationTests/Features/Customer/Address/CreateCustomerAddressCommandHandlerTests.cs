using Application.Features.Customer.Address.Commands.CreateAddress;
using Application.Features.Customer.Address.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace Tests.Unit.Application.Features.Customer.Address;

public sealed class CreateCustomerAddressCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<ILogger<CreateCustomerAddressCommandHandler>> _loggerMock = new();
    private readonly Mock<IRepository<CustomerAddress>> _addressRepoMock = new();
    private readonly CreateCustomerAddressCommandHandler _sut;

    private static readonly Guid CustomerId = Guid.NewGuid();

    public CreateCustomerAddressCommandHandlerTests()
    {
        _sut = new CreateCustomerAddressCommandHandler(
            _unitOfWorkMock.Object,
            _currentUserServiceMock.Object,
            _loggerMock.Object);

        _currentUserServiceMock.SetupGet(x => x.CustomerId).Returns(CustomerId);
        _unitOfWorkMock.Setup(x => x.Repository<CustomerAddress>()).Returns(_addressRepoMock.Object);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _unitOfWorkMock.Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task> op, CancellationToken ct) => op(ct));
    }

    private static CreateCustomerAddressCommand ValidCommand(bool isDefault = false) =>
        new("Home", "123 Main St", null, "Cairo", "Cairo Governorate", "11511", "Egypt", isDefault);

    [Fact]
    public async Task Handle_CustomerIdIsNull_ThrowsUnauthorizedException()
    {
        _currentUserServiceMock.SetupGet(x => x.CustomerId).Returns((Guid?)null);

        var act = () => _sut.Handle(ValidCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_AtMaxAddressLimit_ThrowsBusinessRuleException()
    {
        _addressRepoMock.Setup(x => x.CountAsync(It.IsAny<System.Linq.Expressions.Expression<Func<CustomerAddress, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);

        var act = () => _sut.Handle(ValidCommand(), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("ADDRESS_LIMIT_REACHED");
    }

    [Fact]
    public async Task Handle_ValidNonDefaultAddress_CreatesAndReturnsAddress()
    {
        _addressRepoMock.Setup(x => x.CountAsync(It.IsAny<System.Linq.Expressions.Expression<Func<CustomerAddress, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        _addressRepoMock.Setup(x => x.AddAsync(It.IsAny<CustomerAddress>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerAddress a, CancellationToken _) => a);

        var result = await _sut.Handle(ValidCommand(isDefault: false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Label.Should().Be("Home");
        result.Data.City.Should().Be("Cairo");
        _addressRepoMock.Verify(x => x.AddAsync(It.IsAny<CustomerAddress>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidDefaultAddress_UnsetsPreviousDefaultAndSetsNewOne()
    {
        var previousDefault = CustomerAddress.Create(CustomerId, "Work", "456 Office Rd", null, "Alexandria", null, "21500", "Egypt", isDefault: true);

        _addressRepoMock.Setup(x => x.CountAsync(It.IsAny<System.Linq.Expressions.Expression<Func<CustomerAddress, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _addressRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<CustomerAddress, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(previousDefault);
        _addressRepoMock.Setup(x => x.UpdateAsync(It.IsAny<CustomerAddress>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _addressRepoMock.Setup(x => x.AddAsync(It.IsAny<CustomerAddress>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerAddress a, CancellationToken _) => a);

        var result = await _sut.Handle(ValidCommand(isDefault: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        previousDefault.IsDefault.Should().BeFalse();
        _addressRepoMock.Verify(x => x.UpdateAsync(previousDefault, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidDefaultAddressWithNoPreviousDefault_CreatesAddress()
    {
        _addressRepoMock.Setup(x => x.CountAsync(It.IsAny<System.Linq.Expressions.Expression<Func<CustomerAddress, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _addressRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<CustomerAddress, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerAddress?)null);
        _addressRepoMock.Setup(x => x.AddAsync(It.IsAny<CustomerAddress>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerAddress a, CancellationToken _) => a);

        var result = await _sut.Handle(ValidCommand(isDefault: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsSuccessMessageContainingCreated()
    {
        _addressRepoMock.Setup(x => x.CountAsync(It.IsAny<System.Linq.Expressions.Expression<Func<CustomerAddress, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _addressRepoMock.Setup(x => x.AddAsync(It.IsAny<CustomerAddress>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerAddress a, CancellationToken _) => a);

        var result = await _sut.Handle(ValidCommand(), CancellationToken.None);

        result.Message.Should().Contain("created");
    }

    [Fact]
    public async Task Handle_AddressWithOptionalFields_MapsCorrectly()
    {
        var command = new CreateCustomerAddressCommand("Work", "789 Corp Ave", "Floor 3", "Giza", "Giza Governorate", "12611", "Egypt", false);

        _addressRepoMock.Setup(x => x.CountAsync(It.IsAny<System.Linq.Expressions.Expression<Func<CustomerAddress, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _addressRepoMock.Setup(x => x.AddAsync(It.IsAny<CustomerAddress>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerAddress a, CancellationToken _) => a);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.AddressLine2.Should().Be("Floor 3");
        result.Data.StateProvince.Should().Be("Giza Governorate");
    }
}