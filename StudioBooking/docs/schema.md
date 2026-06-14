# Database Schema

## Tables

### Users

| Column | Type | Notes |
|--------|------|-------|
| Id | INT | PK, auto-increment |
| Email | VARCHAR(256) | Unique |
| FullName | VARCHAR(200) | |
| PasswordHash | VARCHAR(512) | BCrypt hash |

### Businesses

| Column | Type | Notes |
|--------|------|-------|
| Id | INT | PK |
| Name | VARCHAR(200) | e.g. "Studio Fitness", "Zen Yoga Studio" |

### Packages

| Column | Type | Notes |
|--------|------|-------|
| Id | INT | PK |
| UserId | INT | FK → Users |
| BusinessId | INT | FK → Businesses |
| TotalCredits | INT | Original purchase amount |
| RemainingCredits | INT | Decremented on book/promotion |
| ExpiryDate | DATETIME | UTC |
| PurchasedAt | DATETIME | UTC |

### TimetableSchedules

| Column | Type | Notes |
|--------|------|-------|
| Id | INT | PK |
| BusinessId | INT | FK → Businesses |
| ClassName | VARCHAR(200) | |
| InstructorName | VARCHAR(200) | |
| StartTime | DATETIME | UTC |
| EndTime | DATETIME | UTC |
| MaxSlots | INT | Capacity |

**Computed (not stored):**

- `AttendanceCount` = COUNT(Bookings WHERE Status = Confirmed)
- `AvailableSlots` = MaxSlots − AttendanceCount

### Bookings

| Column | Type | Notes |
|--------|------|-------|
| Id | INT | PK |
| UserId | INT | FK → Users |
| TimetableScheduleId | INT | FK → TimetableSchedules |
| PackageId | INT | FK → Packages |
| Status | INT | 1 = Confirmed, 2 = Cancelled |
| BookedAt | DATETIME | UTC |
| CancelledAt | DATETIME | Nullable |

### WaitlistEntries

| Column | Type | Notes |
|--------|------|-------|
| Id | INT | PK |
| UserId | INT | FK → Users |
| TimetableScheduleId | INT | FK → TimetableSchedules |
| PackageId | INT | FK → Packages |
| Status | INT | 1 = Waiting, 2 = Promoted, 3 = Expired, 4 = Cancelled |
| JoinedAt | DATETIME | FIFO ordering |
| PromotedAt | DATETIME | Nullable; set when promoted to booking |

**Expiry (Hangfire):** `WaitlistCleanupJob` sets `Waiting` → `Expired` when `TimetableSchedules.EndTime <= UTC now`. No credit deducted.

**Not cached:** waitlist rows are read/written in MySQL only; Redis caches packages and timetable (attendance/slots), not waitlist.

### Hangfire (job storage)

Hangfire creates its own tables in `studio_booking` (e.g. `Job`, `Set`, `Hash`, `List`). Requires `Allow User Variables=true` on the MySQL connection string.

## Indexes

- `Users.Email` — unique login lookup
- `TimetableSchedules(BusinessId, StartTime)` — timetable filtering
- `Bookings(UserId, Status)` — overlap detection
- `Bookings(TimetableScheduleId, Status)` — attendance count
- `WaitlistEntries(TimetableScheduleId, Status, JoinedAt)` — FIFO promotion

## Seed Data Summary

| Scenario | How to test |
|----------|-------------|
| 10 users | user1–user10@studiobooking.test / Password123! |
| 2 businesses | Studio Fitness (Id 1), Zen Yoga Studio (Id 2) |
| 11 schedules | Mixed dates and capacities |
| Full HIIT class | 3/3 booked + user9 on waitlist |
| Full Hot Yoga | 3/3 booked + user8 on waitlist |
| Expired package | user5@studiobooking.test (package 5, expiry yesterday, 2 credits left) |
| Zero credits | user4@studiobooking.test (package 4, 0 credits, not expired) |
| Ended class waitlist | user6 on Restorative Yoga (yesterday) — expires via Hangfire cleanup |

## Useful verification queries

```sql
USE studio_booking;

-- Full classes
SELECT s.Id, s.ClassName, s.MaxSlots, COUNT(bk.Id) AS Booked
FROM TimetableSchedules s
LEFT JOIN Bookings bk ON bk.TimetableScheduleId = s.Id AND bk.Status = 1
GROUP BY s.Id, s.ClassName, s.MaxSlots
HAVING COUNT(bk.Id) >= s.MaxSlots;

-- Waitlist FIFO
SELECT w.Id, u.Email, s.ClassName, w.JoinedAt, w.Status
FROM WaitlistEntries w
JOIN Users u ON u.Id = w.UserId
JOIN TimetableSchedules s ON s.Id = w.TimetableScheduleId
WHERE w.Status = 1
ORDER BY s.ClassName, w.JoinedAt;

-- After Hangfire waitlist-cleanup (expired entries)
SELECT w.Id, u.Email, s.ClassName, s.EndTime, w.Status
FROM WaitlistEntries w
JOIN Users u ON u.Id = w.UserId
JOIN TimetableSchedules s ON s.Id = w.TimetableScheduleId
WHERE w.Status = 3
ORDER BY w.Id;
```
