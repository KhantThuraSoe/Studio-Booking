namespace StudioBooking.Application.DTOs.Bookings;

public record BookClassRequest(int ScheduleId, int PackageId);

public record BookingResponse(
    int BookingId,
    int ScheduleId,
    int PackageId,
    string Status,
    DateTime BookedAt,
    int RemainingCredits);

public record CancelBookingRequest(int BookingId);

public record CancelBookingResponse(
    int BookingId,
    bool CreditRefunded,
    int RemainingCredits,
    int? PromotedWaitlistEntryId,
    int? PromotedBookingId);
