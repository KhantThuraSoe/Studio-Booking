namespace StudioBooking.Domain.Entities;

public class Package
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int BusinessId { get; set; }
    public int TotalCredits { get; set; }
    public int RemainingCredits { get; set; }
    public DateTime ExpiryDate { get; set; }
    public DateTime PurchasedAt { get; set; }

    public User User { get; set; } = null!;
    public Business Business { get; set; } = null!;
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<WaitlistEntry> WaitlistEntries { get; set; } = new List<WaitlistEntry>();

    public bool IsExpired(DateTime utcNow) => ExpiryDate < utcNow;

    public bool HasAvailableCredits => RemainingCredits > 0;
}
