using Microsoft.EntityFrameworkCore;
using StudioBooking.Application.Interfaces;
using StudioBooking.Domain.Entities;
using StudioBooking.Domain.Enums;
using StudioBooking.Infrastructure.Persistence;

namespace StudioBooking.Infrastructure.Persistence.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;

    public UnitOfWork(ApplicationDbContext context) => _context = context;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}

public class UserRepository : IUserRepository
{
    private readonly ApplicationDbContext _context;

    public UserRepository(ApplicationDbContext context) => _context = context;

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        _context.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    public Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken = default) =>
        await _context.Users.AddAsync(user, cancellationToken);
}

public class BusinessRepository : IBusinessRepository
{
    private readonly ApplicationDbContext _context;

    public BusinessRepository(ApplicationDbContext context) => _context = context;

    public Task<Business?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _context.Businesses.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
}

public class PackageRepository : IPackageRepository
{
    private readonly ApplicationDbContext _context;

    public PackageRepository(ApplicationDbContext context) => _context = context;

    public async Task<IReadOnlyList<Package>> GetAvailableByUserIdAsync(int userId, DateTime utcNow, CancellationToken cancellationToken = default) =>
        await _context.Packages
            .Include(p => p.Business)
            .Where(p => p.UserId == userId && p.RemainingCredits > 0)
            .OrderByDescending(p => p.ExpiryDate)
            .ToListAsync(cancellationToken);

    public Task<Package?> GetByIdForUserAsync(int packageId, int userId, CancellationToken cancellationToken = default) =>
        _context.Packages
            .Include(p => p.Business)
            .FirstOrDefaultAsync(p => p.Id == packageId && p.UserId == userId, cancellationToken);

    public async Task AddAsync(Package package, CancellationToken cancellationToken = default) =>
        await _context.Packages.AddAsync(package, cancellationToken);
}

public class TimetableRepository : ITimetableRepository
{
    private readonly ApplicationDbContext _context;

    public TimetableRepository(ApplicationDbContext context) => _context = context;

    public async Task<IReadOnlyList<TimetableSchedule>> GetSchedulesAsync(int? businessId, DateTime? date, CancellationToken cancellationToken = default)
    {
        var query = _context.TimetableSchedules
            .Include(s => s.Business)
            .Include(s => s.Bookings)
            .AsQueryable();

        if (businessId.HasValue)
            query = query.Where(s => s.BusinessId == businessId.Value);

        if (date.HasValue)
        {
            var dayStart = date.Value.Date;
            var dayEnd = dayStart.AddDays(1);
            query = query.Where(s => s.StartTime >= dayStart && s.StartTime < dayEnd);
        }

        return await query.OrderBy(s => s.StartTime).ToListAsync(cancellationToken);
    }

    public Task<TimetableSchedule?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _context.TimetableSchedules
            .Include(s => s.Business)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<int> GetConfirmedBookingCountAsync(int scheduleId, CancellationToken cancellationToken = default) =>
        _context.Bookings.CountAsync(
            b => b.TimetableScheduleId == scheduleId && b.Status == BookingStatus.Confirmed,
            cancellationToken);
}

public class BookingRepository : IBookingRepository
{
    private readonly ApplicationDbContext _context;

    public BookingRepository(ApplicationDbContext context) => _context = context;

    public Task<Booking?> GetByIdForUserAsync(int bookingId, int userId, CancellationToken cancellationToken = default) =>
        _context.Bookings
            .Include(b => b.TimetableSchedule)
            .Include(b => b.Package)
            .FirstOrDefaultAsync(b => b.Id == bookingId && b.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<Booking>> GetConfirmedByUserIdAsync(int userId, CancellationToken cancellationToken = default) =>
        await _context.Bookings
            .Include(b => b.TimetableSchedule)
            .Where(b => b.UserId == userId && b.Status == BookingStatus.Confirmed)
            .ToListAsync(cancellationToken);

    public async Task<bool> HasOverlappingBookingAsync(
        int userId,
        DateTime startTime,
        DateTime endTime,
        int? excludeBookingId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Bookings
            .Include(b => b.TimetableSchedule)
            .Where(b => b.UserId == userId
                && b.Status == BookingStatus.Confirmed
                && b.TimetableSchedule.StartTime < endTime
                && b.TimetableSchedule.EndTime > startTime);

        if (excludeBookingId.HasValue)
            query = query.Where(b => b.Id != excludeBookingId.Value);

        return await query.AnyAsync(cancellationToken);
    }

    public async Task AddAsync(Booking booking, CancellationToken cancellationToken = default) =>
        await _context.Bookings.AddAsync(booking, cancellationToken);
}

public class WaitlistRepository : IWaitlistRepository
{
    private readonly ApplicationDbContext _context;

    public WaitlistRepository(ApplicationDbContext context) => _context = context;

    public Task<WaitlistEntry?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _context.WaitlistEntries.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

    public Task<WaitlistEntry?> GetActiveByUserAndScheduleAsync(int userId, int scheduleId, CancellationToken cancellationToken = default) =>
        _context.WaitlistEntries.FirstOrDefaultAsync(
            w => w.UserId == userId
                && w.TimetableScheduleId == scheduleId
                && w.Status == WaitlistStatus.Waiting,
            cancellationToken);

    public async Task<IReadOnlyList<WaitlistEntry>> GetWaitingByScheduleAsync(int scheduleId, CancellationToken cancellationToken = default) =>
        await _context.WaitlistEntries
            .Include(w => w.TimetableSchedule)
            .Where(w => w.TimetableScheduleId == scheduleId && w.Status == WaitlistStatus.Waiting)
            .OrderBy(w => w.JoinedAt)
            .ToListAsync(cancellationToken);

    public async Task<int> GetQueuePositionAsync(int scheduleId, int waitlistEntryId, CancellationToken cancellationToken = default)
    {
        var waitingIds = await _context.WaitlistEntries
            .Where(w => w.TimetableScheduleId == scheduleId && w.Status == WaitlistStatus.Waiting)
            .OrderBy(w => w.JoinedAt)
            .Select(w => w.Id)
            .ToListAsync(cancellationToken);

        var index = waitingIds.IndexOf(waitlistEntryId);
        return index >= 0 ? index + 1 : 0;
    }

    public async Task<IReadOnlyList<WaitlistEntry>> GetExpiredWaitingEntriesAsync(DateTime utcNow, CancellationToken cancellationToken = default) =>
        await _context.WaitlistEntries
            .Include(w => w.TimetableSchedule)
            .Where(w => w.Status == WaitlistStatus.Waiting && w.TimetableSchedule.EndTime <= utcNow)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(WaitlistEntry entry, CancellationToken cancellationToken = default) =>
        await _context.WaitlistEntries.AddAsync(entry, cancellationToken);
}
