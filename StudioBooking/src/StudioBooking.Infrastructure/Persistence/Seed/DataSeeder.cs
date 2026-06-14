using Microsoft.EntityFrameworkCore;
using StudioBooking.Domain.Entities;
using StudioBooking.Domain.Enums;
using StudioBooking.Infrastructure.Persistence;

namespace StudioBooking.Infrastructure.Persistence.Seed;

public static class DataSeeder
{
    public const string DefaultPassword = "Password123!";

    public static async Task SeedAsync(ApplicationDbContext context)
    {
        if (await context.Users.AnyAsync())
            return;

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword);
        var utcNow = DateTime.UtcNow;

        var businessA = new Business { Name = "Studio Fitness" };
        var businessB = new Business { Name = "Zen Yoga Studio" };
        context.Businesses.AddRange(businessA, businessB);
        await context.SaveChangesAsync();

        var users = Enumerable.Range(1, 10).Select(i => new User
        {
            Email = $"user{i}@studiobooking.test",
            FullName = $"Test User {i}",
            PasswordHash = passwordHash
        }).ToList();
        context.Users.AddRange(users);
        await context.SaveChangesAsync();

        var packages = new List<Package>
        {
            new() { UserId = users[0].Id, BusinessId = businessA.Id, TotalCredits = 10, RemainingCredits = 10, PurchasedAt = utcNow.AddDays(-10), ExpiryDate = utcNow.AddDays(80) },
            new() { UserId = users[1].Id, BusinessId = businessA.Id, TotalCredits = 5, RemainingCredits = 5, PurchasedAt = utcNow.AddDays(-5), ExpiryDate = utcNow.AddDays(25) },
            new() { UserId = users[2].Id, BusinessId = businessB.Id, TotalCredits = 10, RemainingCredits = 10, PurchasedAt = utcNow.AddDays(-3), ExpiryDate = utcNow.AddDays(60) },
            new() { UserId = users[3].Id, BusinessId = businessA.Id, TotalCredits = 10, RemainingCredits = 0, PurchasedAt = utcNow.AddDays(-30), ExpiryDate = utcNow.AddDays(30) },
            new() { UserId = users[4].Id, BusinessId = businessA.Id, TotalCredits = 10, RemainingCredits = 2, PurchasedAt = utcNow.AddDays(-100), ExpiryDate = utcNow.AddDays(-1) },
            new() { UserId = users[5].Id, BusinessId = businessB.Id, TotalCredits = 8, RemainingCredits = 8, PurchasedAt = utcNow.AddDays(-2), ExpiryDate = utcNow.AddDays(45) },
            new() { UserId = users[6].Id, BusinessId = businessA.Id, TotalCredits = 6, RemainingCredits = 6, PurchasedAt = utcNow.AddDays(-1), ExpiryDate = utcNow.AddDays(14) },
            new() { UserId = users[7].Id, BusinessId = businessB.Id, TotalCredits = 5, RemainingCredits = 3, PurchasedAt = utcNow.AddDays(-20), ExpiryDate = utcNow.AddDays(10) },
            new() { UserId = users[8].Id, BusinessId = businessA.Id, TotalCredits = 12, RemainingCredits = 12, PurchasedAt = utcNow, ExpiryDate = utcNow.AddDays(90) },
            new() { UserId = users[9].Id, BusinessId = businessB.Id, TotalCredits = 4, RemainingCredits = 4, PurchasedAt = utcNow, ExpiryDate = utcNow.AddDays(30) }
        };
        context.Packages.AddRange(packages);
        await context.SaveChangesAsync();

        var baseDate = utcNow.Date.AddDays(1);
        var schedules = new List<TimetableSchedule>
        {
            new() { BusinessId = businessA.Id, ClassName = "Morning Yoga", InstructorName = "John Doe", StartTime = baseDate.AddHours(10), EndTime = baseDate.AddHours(11), MaxSlots = 15 },
            new() { BusinessId = businessA.Id, ClassName = "HIIT Blast", InstructorName = "Jane Smith", StartTime = baseDate.AddHours(12), EndTime = baseDate.AddHours(13), MaxSlots = 3 },
            new() { BusinessId = businessA.Id, ClassName = "Spin Class", InstructorName = "Alex Lee", StartTime = baseDate.AddHours(14), EndTime = baseDate.AddHours(15), MaxSlots = 10 },
            new() { BusinessId = businessA.Id, ClassName = "Pilates", InstructorName = "Maria Garcia", StartTime = baseDate.AddDays(1).AddHours(9), EndTime = baseDate.AddDays(1).AddHours(10), MaxSlots = 8 },
            new() { BusinessId = businessA.Id, ClassName = "CrossFit", InstructorName = "Chris Brown", StartTime = baseDate.AddDays(1).AddHours(17), EndTime = baseDate.AddDays(1).AddHours(18), MaxSlots = 3 },
            new() { BusinessId = businessB.Id, ClassName = "Vinyasa Flow", InstructorName = "Emily Chen", StartTime = baseDate.AddHours(8), EndTime = baseDate.AddHours(9), MaxSlots = 12 },
            new() { BusinessId = businessB.Id, ClassName = "Meditation", InstructorName = "Sam Wilson", StartTime = baseDate.AddHours(18), EndTime = baseDate.AddHours(19), MaxSlots = 20 },
            new() { BusinessId = businessB.Id, ClassName = "Power Yoga", InstructorName = "Lisa Park", StartTime = baseDate.AddDays(2).AddHours(7), EndTime = baseDate.AddDays(2).AddHours(8), MaxSlots = 2 },
            new() { BusinessId = businessB.Id, ClassName = "Restorative Yoga", InstructorName = "Tom Allen", StartTime = baseDate.AddDays(-1).AddHours(16), EndTime = baseDate.AddDays(-1).AddHours(17), MaxSlots = 5 },
            new() { BusinessId = businessA.Id, ClassName = "Boxing Fundamentals", InstructorName = "Mike Tyson", StartTime = baseDate.AddDays(3).AddHours(11), EndTime = baseDate.AddDays(3).AddHours(12), MaxSlots = 6 },
            new() { BusinessId = businessB.Id, ClassName = "Hot Yoga", InstructorName = "Nina Patel", StartTime = baseDate.AddHours(20), EndTime = baseDate.AddHours(21), MaxSlots = 3 }
        };
        context.TimetableSchedules.AddRange(schedules);
        await context.SaveChangesAsync();

        var hiit = schedules[1];
        var powerYoga = schedules[7];
        var hotYoga = schedules[10];
        var restorative = schedules[8];

        AddBooking(context, users[0], packages[0], hiit, utcNow.AddHours(-2));
        AddBooking(context, users[1], packages[1], hiit, utcNow.AddHours(-2));
        AddBooking(context, users[6], packages[6], hiit, utcNow.AddHours(-2));

        AddBooking(context, users[2], packages[2], powerYoga, utcNow.AddHours(-1));
        AddBooking(context, users[5], packages[5], powerYoga, utcNow.AddHours(-1));

        AddBooking(context, users[2], packages[2], hotYoga, utcNow.AddHours(-3));
        AddBooking(context, users[5], packages[5], hotYoga, utcNow.AddHours(-3));
        AddBooking(context, users[9], packages[9], hotYoga, utcNow.AddHours(-3));

        AddBooking(context, users[0], packages[0], schedules[0], utcNow.AddHours(-4));

        context.Bookings.Add(new Booking
        {
            UserId = users[0].Id,
            TimetableScheduleId = schedules[4].Id,
            PackageId = packages[0].Id,
            Status = BookingStatus.Cancelled,
            BookedAt = utcNow.AddDays(-2),
            CancelledAt = utcNow.AddDays(-2).AddHours(1)
        });

        context.WaitlistEntries.AddRange(
            new WaitlistEntry { UserId = users[8].Id, TimetableScheduleId = hiit.Id, PackageId = packages[8].Id, Status = WaitlistStatus.Waiting, JoinedAt = utcNow.AddHours(-1) },
            new WaitlistEntry { UserId = users[9].Id, TimetableScheduleId = powerYoga.Id, PackageId = packages[9].Id, Status = WaitlistStatus.Waiting, JoinedAt = utcNow.AddMinutes(-45) },
            new WaitlistEntry { UserId = users[7].Id, TimetableScheduleId = hotYoga.Id, PackageId = packages[7].Id, Status = WaitlistStatus.Waiting, JoinedAt = utcNow.AddMinutes(-20) },
            new WaitlistEntry { UserId = users[5].Id, TimetableScheduleId = restorative.Id, PackageId = packages[5].Id, Status = WaitlistStatus.Waiting, JoinedAt = utcNow.AddDays(-2) }
        );

        await context.SaveChangesAsync();
    }

    private static void AddBooking(ApplicationDbContext context, User user, Package package, TimetableSchedule schedule, DateTime bookedAt)
    {
        context.Bookings.Add(new Booking
        {
            UserId = user.Id,
            TimetableScheduleId = schedule.Id,
            PackageId = package.Id,
            Status = BookingStatus.Confirmed,
            BookedAt = bookedAt
        });
        package.RemainingCredits -= 1;
    }
}
