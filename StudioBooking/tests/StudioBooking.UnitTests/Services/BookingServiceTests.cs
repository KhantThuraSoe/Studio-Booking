using FluentAssertions;
using Moq;
using StudioBooking.Application.DTOs.Bookings;
using StudioBooking.Application.Exceptions;
using StudioBooking.Application.Interfaces;
using StudioBooking.Application.Services;
using StudioBooking.Domain.Entities;
using StudioBooking.Domain.Enums;

namespace StudioBooking.UnitTests.Services;

public class BookingServiceTests
{
    private readonly Mock<ITimetableRepository> _timetableRepository = new();
    private readonly Mock<IPackageRepository> _packageRepository = new();
    private readonly Mock<IBookingRepository> _bookingRepository = new();
    private readonly Mock<ISlotReservationService> _slotReservationService = new();
    private readonly Mock<IWaitlistPromotionService> _waitlistPromotionService = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICacheService> _cacheService = new();
    private readonly PackageValidationService _packageValidationService;
    private readonly BookingService _sut;

    public BookingServiceTests()
    {
        _packageValidationService = new PackageValidationService(_packageRepository.Object);
        _sut = new BookingService(
            _timetableRepository.Object,
            _packageRepository.Object,
            _bookingRepository.Object,
            _packageValidationService,
            _slotReservationService.Object,
            _waitlistPromotionService.Object,
            _unitOfWork.Object,
            _cacheService.Object);
    }

    [Fact]
    public async Task BookClassAsync_DeductsCredit_AndCreatesBooking()
    {
        var schedule = CreateSchedule(id: 1, maxSlots: 5, startInHours: 24);
        var package = CreatePackage(id: 10, userId: 1, businessId: 1, credits: 3);

        SetupSuccessfulBooking(schedule, package, confirmedCount: 2);

        var result = await _sut.BookClassAsync(1, new BookClassRequest(1, 10));

        result.RemainingCredits.Should().Be(2);
        result.Status.Should().Be("Confirmed");
        package.RemainingCredits.Should().Be(2);
        _bookingRepository.Verify(r => r.AddAsync(It.Is<Booking>(b =>
            b.UserId == 1 && b.TimetableScheduleId == 1 && b.Status == BookingStatus.Confirmed), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BookClassAsync_Throws_WhenScheduleFull()
    {
        var schedule = CreateSchedule(id: 1, maxSlots: 3, startInHours: 24);
        var package = CreatePackage(id: 10, userId: 1, businessId: 1, credits: 3);

        _timetableRepository.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(schedule);
        _packageRepository.Setup(r => r.GetByIdForUserAsync(10, 1, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        _bookingRepository.Setup(r => r.HasOverlappingBookingAsync(1, schedule.StartTime, schedule.EndTime, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _timetableRepository.Setup(r => r.GetConfirmedBookingCountAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(3);

        var act = () => _sut.BookClassAsync(1, new BookClassRequest(1, 10));

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.ErrorCode.Should().Be("SCHEDULE_FULL");
    }

    [Fact]
    public async Task BookClassAsync_Throws_WhenOverlappingBookingExists()
    {
        var schedule = CreateSchedule(id: 1, maxSlots: 5, startInHours: 24);
        var package = CreatePackage(id: 10, userId: 1, businessId: 1, credits: 3);

        _timetableRepository.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(schedule);
        _packageRepository.Setup(r => r.GetByIdForUserAsync(10, 1, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        _bookingRepository.Setup(r => r.HasOverlappingBookingAsync(1, schedule.StartTime, schedule.EndTime, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = () => _sut.BookClassAsync(1, new BookClassRequest(1, 10));

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.ErrorCode.Should().Be("SCHEDULE_OVERLAP");
    }

    [Fact]
    public async Task BookClassAsync_Throws_WhenSlotReservationFails()
    {
        var schedule = CreateSchedule(id: 1, maxSlots: 5, startInHours: 24);
        var package = CreatePackage(id: 10, userId: 1, businessId: 1, credits: 3);

        _timetableRepository.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(schedule);
        _packageRepository.Setup(r => r.GetByIdForUserAsync(10, 1, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        _bookingRepository.Setup(r => r.HasOverlappingBookingAsync(1, schedule.StartTime, schedule.EndTime, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _timetableRepository.Setup(r => r.GetConfirmedBookingCountAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(4);
        _slotReservationService.Setup(s => s.TryReserveSlotAsync(1, 5, 4, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var act = () => _sut.BookClassAsync(1, new BookClassRequest(1, 10));

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.ErrorCode.Should().Be("SLOT_UNAVAILABLE");
    }

    [Fact]
    public async Task CancelBookingAsync_RefundsCredit_WhenMoreThanFourHoursBeforeStart()
    {
        var schedule = CreateSchedule(id: 1, maxSlots: 5, startInHours: 10);
        var package = CreatePackage(id: 10, userId: 1, businessId: 1, credits: 2);
        var booking = new Booking
        {
            Id = 100,
            UserId = 1,
            TimetableScheduleId = 1,
            PackageId = 10,
            Status = BookingStatus.Confirmed,
            TimetableSchedule = schedule,
            Package = package
        };

        _bookingRepository.Setup(r => r.GetByIdForUserAsync(100, 1, It.IsAny<CancellationToken>())).ReturnsAsync(booking);
        _waitlistPromotionService.Setup(w => w.PromoteNextEligibleAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((null, null));

        var result = await _sut.CancelBookingAsync(1, new CancelBookingRequest(100));

        result.CreditRefunded.Should().BeTrue();
        result.RemainingCredits.Should().Be(3);
        booking.Status.Should().Be(BookingStatus.Cancelled);
        _slotReservationService.Verify(s => s.ReleaseSlotAsync(1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelBookingAsync_DoesNotRefund_WhenWithinFourHoursOfStart()
    {
        var schedule = CreateSchedule(id: 1, maxSlots: 5, startInHours: 2);
        var package = CreatePackage(id: 10, userId: 1, businessId: 1, credits: 2);
        var booking = new Booking
        {
            Id = 100,
            UserId = 1,
            TimetableScheduleId = 1,
            PackageId = 10,
            Status = BookingStatus.Confirmed,
            TimetableSchedule = schedule,
            Package = package
        };

        _bookingRepository.Setup(r => r.GetByIdForUserAsync(100, 1, It.IsAny<CancellationToken>())).ReturnsAsync(booking);
        _waitlistPromotionService.Setup(w => w.PromoteNextEligibleAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((null, null));

        var result = await _sut.CancelBookingAsync(1, new CancelBookingRequest(100));

        result.CreditRefunded.Should().BeFalse();
        result.RemainingCredits.Should().Be(2);
    }

    [Fact]
    public async Task CancelBookingAsync_PromotesWaitlistUser_WhenSlotReleased()
    {
        var schedule = CreateSchedule(id: 1, maxSlots: 5, startInHours: 10);
        var package = CreatePackage(id: 10, userId: 1, businessId: 1, credits: 2);
        var booking = new Booking
        {
            Id = 100,
            UserId = 1,
            TimetableScheduleId = 1,
            PackageId = 10,
            Status = BookingStatus.Confirmed,
            TimetableSchedule = schedule,
            Package = package
        };

        _bookingRepository.Setup(r => r.GetByIdForUserAsync(100, 1, It.IsAny<CancellationToken>())).ReturnsAsync(booking);
        _waitlistPromotionService.Setup(w => w.PromoteNextEligibleAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((55, 200));
        _timetableRepository.Setup(r => r.GetConfirmedBookingCountAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(3);

        var result = await _sut.CancelBookingAsync(1, new CancelBookingRequest(100));

        result.PromotedWaitlistEntryId.Should().Be(55);
        result.PromotedBookingId.Should().Be(200);
        _slotReservationService.Verify(s => s.SyncSlotCountAsync(1, 3, It.IsAny<CancellationToken>()), Times.Once);
    }

    private void SetupSuccessfulBooking(TimetableSchedule schedule, Package package, int confirmedCount)
    {
        _timetableRepository.Setup(r => r.GetByIdAsync(schedule.Id, It.IsAny<CancellationToken>())).ReturnsAsync(schedule);
        _packageRepository.Setup(r => r.GetByIdForUserAsync(package.Id, package.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        _bookingRepository.Setup(r => r.HasOverlappingBookingAsync(package.UserId, schedule.StartTime, schedule.EndTime, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _timetableRepository.Setup(r => r.GetConfirmedBookingCountAsync(schedule.Id, It.IsAny<CancellationToken>())).ReturnsAsync(confirmedCount);
        _slotReservationService.Setup(s => s.TryReserveSlotAsync(schedule.Id, schedule.MaxSlots, confirmedCount, It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    private static TimetableSchedule CreateSchedule(int id, int maxSlots, double startInHours)
    {
        var start = DateTime.UtcNow.AddHours(startInHours);
        return new TimetableSchedule
        {
            Id = id,
            BusinessId = 1,
            ClassName = "Test Class",
            InstructorName = "Instructor",
            StartTime = start,
            EndTime = start.AddHours(1),
            MaxSlots = maxSlots
        };
    }

    private static Package CreatePackage(int id, int userId, int businessId, int credits)
    {
        return new Package
        {
            Id = id,
            UserId = userId,
            BusinessId = businessId,
            RemainingCredits = credits,
            TotalCredits = credits,
            ExpiryDate = DateTime.UtcNow.AddDays(30)
        };
    }
}
