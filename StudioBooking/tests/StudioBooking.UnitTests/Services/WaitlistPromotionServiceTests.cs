using FluentAssertions;
using Moq;
using StudioBooking.Application.Interfaces;
using StudioBooking.Application.Services;
using StudioBooking.Domain.Entities;
using StudioBooking.Domain.Enums;

namespace StudioBooking.UnitTests.Services;

public class WaitlistPromotionServiceTests
{
    private readonly Mock<IWaitlistRepository> _waitlistRepository = new();
    private readonly Mock<IPackageRepository> _packageRepository = new();
    private readonly Mock<IBookingRepository> _bookingRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICacheService> _cacheService = new();
    private readonly WaitlistPromotionService _sut;

    public WaitlistPromotionServiceTests()
    {
        _sut = new WaitlistPromotionService(
            _waitlistRepository.Object,
            _packageRepository.Object,
            _bookingRepository.Object,
            _unitOfWork.Object,
            _cacheService.Object);
    }

    [Fact]
    public async Task PromoteNextEligibleAsync_PromotesFirstEligibleUser_InFifoOrder()
    {
        var schedule = new TimetableSchedule
        {
            Id = 1,
            StartTime = DateTime.UtcNow.AddDays(1),
            EndTime = DateTime.UtcNow.AddDays(1).AddHours(1)
        };

        var expiredEntry = new WaitlistEntry
        {
            Id = 1,
            UserId = 10,
            PackageId = 100,
            TimetableScheduleId = 1,
            Status = WaitlistStatus.Waiting,
            JoinedAt = DateTime.UtcNow.AddHours(-2),
            TimetableSchedule = schedule
        };

        var eligibleEntry = new WaitlistEntry
        {
            Id = 2,
            UserId = 20,
            PackageId = 200,
            TimetableScheduleId = 1,
            Status = WaitlistStatus.Waiting,
            JoinedAt = DateTime.UtcNow.AddHours(-1),
            TimetableSchedule = schedule
        };

        var eligiblePackage = new Package
        {
            Id = 200,
            UserId = 20,
            RemainingCredits = 3,
            ExpiryDate = DateTime.UtcNow.AddDays(30)
        };

        _waitlistRepository.Setup(r => r.GetWaitingByScheduleAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WaitlistEntry> { expiredEntry, eligibleEntry });
        _packageRepository.Setup(r => r.GetByIdForUserAsync(100, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Package { Id = 100, UserId = 10, RemainingCredits = 0, ExpiryDate = DateTime.UtcNow.AddDays(30) });
        _packageRepository.Setup(r => r.GetByIdForUserAsync(200, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(eligiblePackage);
        _bookingRepository.Setup(r => r.HasOverlappingBookingAsync(20, schedule.StartTime, schedule.EndTime, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var (waitlistEntryId, bookingId) = await _sut.PromoteNextEligibleAsync(1);

        waitlistEntryId.Should().Be(2);
        eligibleEntry.Status.Should().Be(WaitlistStatus.Promoted);
        eligibleEntry.PromotedAt.Should().NotBeNull();
        expiredEntry.Status.Should().Be(WaitlistStatus.Expired);
        eligiblePackage.RemainingCredits.Should().Be(2);
        _bookingRepository.Verify(r => r.AddAsync(It.Is<Booking>(b =>
            b.UserId == 20 && b.TimetableScheduleId == 1 && b.Status == BookingStatus.Confirmed), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PromoteNextEligibleAsync_SkipsUserWithOverlappingBooking()
    {
        var schedule = new TimetableSchedule
        {
            Id = 1,
            StartTime = DateTime.UtcNow.AddDays(1),
            EndTime = DateTime.UtcNow.AddDays(1).AddHours(1)
        };

        var overlappingEntry = new WaitlistEntry
        {
            Id = 1,
            UserId = 10,
            PackageId = 100,
            TimetableScheduleId = 1,
            Status = WaitlistStatus.Waiting,
            JoinedAt = DateTime.UtcNow.AddHours(-2),
            TimetableSchedule = schedule
        };

        _waitlistRepository.Setup(r => r.GetWaitingByScheduleAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WaitlistEntry> { overlappingEntry });
        _packageRepository.Setup(r => r.GetByIdForUserAsync(100, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Package { Id = 100, UserId = 10, RemainingCredits = 5, ExpiryDate = DateTime.UtcNow.AddDays(30) });
        _bookingRepository.Setup(r => r.HasOverlappingBookingAsync(10, schedule.StartTime, schedule.EndTime, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var (waitlistEntryId, bookingId) = await _sut.PromoteNextEligibleAsync(1);

        waitlistEntryId.Should().BeNull();
        bookingId.Should().BeNull();
        overlappingEntry.Status.Should().Be(WaitlistStatus.Expired);
        _bookingRepository.Verify(r => r.AddAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
