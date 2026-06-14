namespace StudioBooking.Domain.Entities;

public class TimetableSchedule
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string InstructorName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int MaxSlots { get; set; }

    public Business Business { get; set; } = null!;
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<WaitlistEntry> WaitlistEntries { get; set; } = new List<WaitlistEntry>();

    public bool OverlapsWith(DateTime otherStart, DateTime otherEnd) =>
        StartTime < otherEnd && EndTime > otherStart;
}
