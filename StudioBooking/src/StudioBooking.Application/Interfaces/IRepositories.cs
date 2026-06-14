using StudioBooking.Domain.Entities;

namespace StudioBooking.Application.Interfaces;

// Repository contracts for persistence access.

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken cancellationToken = default);
}

public interface IBusinessRepository
{
    Task<Business?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
}

public interface IPackageRepository
{
    Task<IReadOnlyList<Package>> GetAvailableByUserIdAsync(int userId, DateTime utcNow, CancellationToken cancellationToken = default);
    Task<Package?> GetByIdForUserAsync(int packageId, int userId, CancellationToken cancellationToken = default);
    Task AddAsync(Package package, CancellationToken cancellationToken = default);
}

public interface ITimetableRepository
{
    Task<IReadOnlyList<TimetableSchedule>> GetSchedulesAsync(int? businessId, DateTime? date, CancellationToken cancellationToken = default);
    Task<TimetableSchedule?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<int> GetConfirmedBookingCountAsync(int scheduleId, CancellationToken cancellationToken = default);
}

public interface IBookingRepository
{
    Task<Booking?> GetByIdForUserAsync(int bookingId, int userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Booking>> GetConfirmedByUserIdAsync(int userId, CancellationToken cancellationToken = default);
    Task<bool> HasOverlappingBookingAsync(int userId, DateTime startTime, DateTime endTime, int? excludeBookingId = null, CancellationToken cancellationToken = default);
    Task AddAsync(Booking booking, CancellationToken cancellationToken = default);
}

public interface IWaitlistRepository
{
    Task<WaitlistEntry?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<WaitlistEntry?> GetActiveByUserAndScheduleAsync(int userId, int scheduleId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WaitlistEntry>> GetWaitingByScheduleAsync(int scheduleId, CancellationToken cancellationToken = default);
    Task<int> GetQueuePositionAsync(int scheduleId, int waitlistEntryId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WaitlistEntry>> GetExpiredWaitingEntriesAsync(DateTime utcNow, CancellationToken cancellationToken = default);
    Task AddAsync(WaitlistEntry entry, CancellationToken cancellationToken = default);
}
