namespace StudioBooking.Domain.Entities;

public class Business
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<Package> Packages { get; set; } = new List<Package>();
    public ICollection<TimetableSchedule> TimetableSchedules { get; set; } = new List<TimetableSchedule>();
}
