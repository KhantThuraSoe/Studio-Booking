using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioBooking.Domain.Entities;

namespace StudioBooking.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Email).HasMaxLength(256).IsRequired();
        builder.HasIndex(x => x.Email).IsUnique();
        builder.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
    }
}

public class BusinessConfiguration : IEntityTypeConfiguration<Business>
{
    public void Configure(EntityTypeBuilder<Business> builder)
    {
        builder.ToTable("Businesses");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
    }
}

public class PackageConfiguration : IEntityTypeConfiguration<Package>
{
    public void Configure(EntityTypeBuilder<Package> builder)
    {
        builder.ToTable("Packages");
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.User).WithMany(x => x.Packages).HasForeignKey(x => x.UserId);
        builder.HasOne(x => x.Business).WithMany(x => x.Packages).HasForeignKey(x => x.BusinessId);
    }
}

public class TimetableScheduleConfiguration : IEntityTypeConfiguration<TimetableSchedule>
{
    public void Configure(EntityTypeBuilder<TimetableSchedule> builder)
    {
        builder.ToTable("TimetableSchedules");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ClassName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.InstructorName).HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.Business).WithMany(x => x.TimetableSchedules).HasForeignKey(x => x.BusinessId);
        builder.HasIndex(x => new { x.BusinessId, x.StartTime });
    }
}

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("Bookings");
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.User).WithMany(x => x.Bookings).HasForeignKey(x => x.UserId);
        builder.HasOne(x => x.TimetableSchedule).WithMany(x => x.Bookings).HasForeignKey(x => x.TimetableScheduleId);
        builder.HasOne(x => x.Package).WithMany(x => x.Bookings).HasForeignKey(x => x.PackageId);
        builder.HasIndex(x => new { x.UserId, x.Status });
        builder.HasIndex(x => new { x.TimetableScheduleId, x.Status });
    }
}

public class WaitlistEntryConfiguration : IEntityTypeConfiguration<WaitlistEntry>
{
    public void Configure(EntityTypeBuilder<WaitlistEntry> builder)
    {
        builder.ToTable("WaitlistEntries");
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.User).WithMany(x => x.WaitlistEntries).HasForeignKey(x => x.UserId);
        builder.HasOne(x => x.TimetableSchedule).WithMany(x => x.WaitlistEntries).HasForeignKey(x => x.TimetableScheduleId);
        builder.HasOne(x => x.Package).WithMany(x => x.WaitlistEntries).HasForeignKey(x => x.PackageId);
        builder.HasIndex(x => new { x.TimetableScheduleId, x.Status, x.JoinedAt });
    }
}
