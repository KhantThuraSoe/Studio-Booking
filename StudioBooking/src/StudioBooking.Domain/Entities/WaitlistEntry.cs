using StudioBooking.Domain.Enums;

namespace StudioBooking.Domain.Entities;

public class WaitlistEntry
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int TimetableScheduleId { get; set; }
    public int PackageId { get; set; }
    public WaitlistStatus Status { get; set; }
    public DateTime JoinedAt { get; set; }
    public DateTime? PromotedAt { get; set; }

    public User User { get; set; } = null!;
    public TimetableSchedule TimetableSchedule { get; set; } = null!;
    public Package Package { get; set; } = null!;
}
