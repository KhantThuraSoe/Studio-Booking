using FluentAssertions;
using Moq;
using StudioBooking.Application.Exceptions;
using StudioBooking.Application.Interfaces;
using StudioBooking.Application.Services;
using StudioBooking.Domain.Entities;

namespace StudioBooking.UnitTests.Services;

public class PackageValidationServiceTests
{
    private readonly Mock<IPackageRepository> _packageRepository = new();
    private readonly PackageValidationService _sut;

    public PackageValidationServiceTests()
    {
        _sut = new PackageValidationService(_packageRepository.Object);
    }

    [Fact]
    public async Task ValidatePackageForScheduleAsync_Throws_WhenPackageExpired()
    {
        var package = new Package
        {
            Id = 1,
            UserId = 1,
            BusinessId = 1,
            RemainingCredits = 5,
            ExpiryDate = DateTime.UtcNow.AddDays(-1)
        };
        _packageRepository.Setup(r => r.GetByIdForUserAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);

        var act = () => _sut.ValidatePackageForScheduleAsync(1, 1, 1, DateTime.UtcNow);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.ErrorCode.Should().Be("PACKAGE_EXPIRED");
    }

    [Fact]
    public async Task ValidatePackageForScheduleAsync_Throws_WhenInsufficientCredits()
    {
        var package = new Package
        {
            Id = 1,
            UserId = 1,
            BusinessId = 1,
            RemainingCredits = 0,
            ExpiryDate = DateTime.UtcNow.AddDays(30)
        };
        _packageRepository.Setup(r => r.GetByIdForUserAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);

        var act = () => _sut.ValidatePackageForScheduleAsync(1, 1, 1, DateTime.UtcNow);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.ErrorCode.Should().Be("INSUFFICIENT_CREDITS");
    }

    [Fact]
    public async Task ValidatePackageForScheduleAsync_Throws_WhenBusinessMismatch()
    {
        var package = new Package
        {
            Id = 1,
            UserId = 1,
            BusinessId = 1,
            RemainingCredits = 5,
            ExpiryDate = DateTime.UtcNow.AddDays(30)
        };
        _packageRepository.Setup(r => r.GetByIdForUserAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);

        var act = () => _sut.ValidatePackageForScheduleAsync(1, 1, 2, DateTime.UtcNow);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.ErrorCode.Should().Be("BUSINESS_MISMATCH");
    }
}
