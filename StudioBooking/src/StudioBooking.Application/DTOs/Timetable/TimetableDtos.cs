namespace StudioBooking.Application.DTOs.Timetable;

public record TimetableScheduleDto(
    int ScheduleId,
    string ClassName,
    string InstructorName,
    DateTime StartTime,
    DateTime EndTime,
    int AttendanceCount,
    int AvailableSlots,
    string BusinessName);
