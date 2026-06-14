using StudioBooking.Domain.Enums;

namespace StudioBooking.Domain.Entities;

public class Booking
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int TimetableScheduleId { get; set; }
    public int PackageId { get; set; }
    public BookingStatus Status { get; set; }
    public DateTime BookedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    public User User { get; set; } = null!;
    public TimetableSchedule TimetableSchedule { get; set; } = null!;
    public Package Package { get; set; } = null!;
}
