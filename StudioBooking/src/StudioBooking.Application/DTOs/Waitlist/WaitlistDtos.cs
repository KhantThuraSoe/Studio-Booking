namespace StudioBooking.Application.DTOs.Waitlist;

public record JoinWaitlistRequest(int ScheduleId, int PackageId);

public record WaitlistResponse(
    int WaitlistEntryId,
    int ScheduleId,
    int PackageId,
    string Status,
    DateTime JoinedAt,
    int QueuePosition);
