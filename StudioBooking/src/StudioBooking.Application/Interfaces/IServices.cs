namespace StudioBooking.Application.Interfaces;

public interface IAuthService
{
    Task<(bool Success, string? Token, string? Error)> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
}

public interface ITokenService
{
    string GenerateToken(int userId, string email, string fullName);
}

public interface IPackageService
{
    Task<IReadOnlyList<DTOs.Packages.PackageDto>> GetAvailablePackagesAsync(int userId, CancellationToken cancellationToken = default);
    Task<DTOs.Packages.PurchasePackageResponse> PurchasePackageAsync(int userId, DTOs.Packages.PurchasePackageRequest request, CancellationToken cancellationToken = default);
}

public interface ITimetableService
{
    Task<IReadOnlyList<DTOs.Timetable.TimetableScheduleDto>> GetSchedulesAsync(int? businessId, DateTime? date, CancellationToken cancellationToken = default);
}

public interface IBookingService
{
    Task<DTOs.Bookings.BookingResponse> BookClassAsync(int userId, DTOs.Bookings.BookClassRequest request, CancellationToken cancellationToken = default);
    Task<DTOs.Bookings.CancelBookingResponse> CancelBookingAsync(int userId, DTOs.Bookings.CancelBookingRequest request, CancellationToken cancellationToken = default);
}

public interface IWaitlistService
{
    Task<DTOs.Waitlist.WaitlistResponse> JoinWaitlistAsync(int userId, DTOs.Waitlist.JoinWaitlistRequest request, CancellationToken cancellationToken = default);
    Task ExpireEndedWaitlistsAsync(CancellationToken cancellationToken = default);
}

public interface ISlotReservationService
{
    Task<bool> TryReserveSlotAsync(int scheduleId, int maxSlots, int currentDbCount, CancellationToken cancellationToken = default);
    Task ReleaseSlotAsync(int scheduleId, CancellationToken cancellationToken = default);
    Task SyncSlotCountAsync(int scheduleId, int confirmedCount, CancellationToken cancellationToken = default);
}

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default);
}

public interface IPackageValidationService
{
    Task ValidatePackageForScheduleAsync(int userId, int packageId, int scheduleBusinessId, DateTime utcNow, CancellationToken cancellationToken = default);
}

public interface IWaitlistPromotionService
{
    Task<(int? WaitlistEntryId, int? BookingId)> PromoteNextEligibleAsync(int scheduleId, CancellationToken cancellationToken = default);
}
