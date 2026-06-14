# Studio Booking Engine

A simplified studio booking platform backend API

Customers book fitness class schedules using business-scoped credit packages. Supports booking, cancellation with refund rules, FIFO waitlists, Redis-backed concurrency, and JWT authentication.

## Tech Stack

- .NET 8
- MySQL 8 + Entity Framework Core (Pomelo)
- Redis (caching + concurrency slot reservation)
- Hangfire (recurring waitlist cleanup, MySQL storage)
- JWT authentication
- Swagger UI
- Clean Architecture (Domain → Application → Infrastructure → API)

## Prerequisites

Install:

1. **.NET 8 SDK**
2. **MySQL 8** — `localhost:3306`
3. **Redis** — `localhost:6379`

## Database Setup

Create the MySQL database:

```sql
CREATE DATABASE IF NOT EXISTS studio_booking
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;
```

Update connection strings in `src/StudioBooking.Api/appsettings.Development.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Port=3306;Database=studio_booking;User=root;Password=YOUR_PASSWORD;Allow User Variables=true;",
  "Redis": "localhost:6379"
},
"Hangfire": {
  "WaitlistCleanupIntervalMinutes": 5
}
```

**MySQL note:** `Allow User Variables=true` is required for Hangfire.MySqlStorage (dashboard and recurring jobs). It is appended automatically in code if omitted.

Optional: override the cleanup interval in `appsettings.Development.json` (e.g. `1` while debugging).

## Run the API

```bash
cd StudioBooking
dotnet restore
dotnet run --project src/StudioBooking.Api/StudioBooking.Api.csproj
```

On startup the app will:

1. Apply EF Core migrations
2. Seed sample data (first run only)

Swagger UI: **http://localhost:5104/swagger**  
Hangfire dashboard (Development): **http://localhost:5104/hangfire** — view/trigger the `waitlist-cleanup` recurring job

## Authentication

Protected endpoints require JWT. Login first:

```http
POST /api/auth/login
{
  "email": "user1@studiobooking.test",
  "password": "Password123!"
}
```

In Swagger: click **Authorize** → enter `Bearer {your-token}` → **Authorize**.

### Seeded test users

All users share password `Password123!`. Credits below are **after** seed bookings run.

| Email | Business | Credits left | Expiry | Notes |
|-------|----------|--------------|--------|-------|
| user1@studiobooking.test | Studio Fitness | 8 | Active | Booked HIIT + Morning Yoga |
| user2@studiobooking.test | Studio Fitness | 4 | Active | Booked HIIT |
| user3@studiobooking.test | Zen Yoga Studio | 8 | Active | Booked Power Yoga + Hot Yoga |
| user4@studiobooking.test | Studio Fitness | **0** | Active | **Zero credits** — omitted from `GET /api/packages` |
| user5@studiobooking.test | Studio Fitness | 2 | **Expired** | **Expired package** — still listed with `isExpired: true` |
| user6@studiobooking.test | Zen Yoga Studio | 6 | Active | Booked Power Yoga + Hot Yoga |
| user7@studiobooking.test | Studio Fitness | 5 | Active | Booked HIIT |
| user8@studiobooking.test | Zen Yoga Studio | 3 | Active | On Hot Yoga waitlist |
| user9@studiobooking.test | Studio Fitness | 12 | Active | On HIIT waitlist |
| user10@studiobooking.test | Zen Yoga Studio | 3 | Active | Booked Hot Yoga; on Power Yoga waitlist |

**Edge-case testing:**

| Scenario | Login as | PackageId | Expected error |
|----------|----------|-----------|----------------|
| Zero credits | user4 | 4 | `INSUFFICIENT_CREDITS` |
| Expired package | user5 | 5 | `PACKAGE_EXPIRED` |
| Wrong business | user1 | 1 | Book Zen Yoga schedule → `BUSINESS_MISMATCH` |

## API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/auth/login` | Obtain JWT (anonymous) |
| GET | `/api/packages` | List packages with remaining credits |
| POST | `/api/packages/purchase` | Mock package purchase |
| GET | `/api/timetable?businessId=&date=` | List schedules with attendance/slots |
| POST | `/api/bookings` | Book a class |
| POST | `/api/bookings/cancel` | Cancel + refund logic |
| POST | `/api/waitlist` | Join waitlist when class is full |

### Example timetable response

```json
[
  {
    "scheduleId": 1,
    "className": "Morning Yoga",
    "instructorName": "John Doe",
    "startTime": "2026-06-14T10:00:00",
    "endTime": "2026-06-14T11:00:00",
    "attendanceCount": 1,
    "availableSlots": 14,
    "businessName": "Studio Fitness"
  }
]
```

## Architecture

```
StudioBooking/
├── src/
│   ├── StudioBooking.Domain/          # Entities, enums
│   ├── StudioBooking.Application/     # Use cases, DTOs, interfaces
│   ├── StudioBooking.Infrastructure/  # EF Core, Redis, JWT, Hangfire, seeding
│   └── StudioBooking.Api/             # Controllers, Swagger, middleware
└── docs/
    ├── schema.md                    # Database schema and table definitions
    ├── studio-booking-erd.png       # ERD Diagram in png
    └── README.md                    # Documentation
```

**Dependency rule:** Api → Application → Domain ← Infrastructure

| Layer | Responsibility |
|-------|----------------|
| Domain | Entities, enums, domain rules — no framework dependencies |
| Application | Booking/package/waitlist orchestration, validation, DTOs |
| Infrastructure | MySQL (EF Core), Redis, JWT, Hangfire jobs, migrations, seed data |
| Api | HTTP controllers, auth, exception mapping, Swagger |

## Business Rules Implemented

### Packages
- Expired packages rejected at book/waitlist time
- 1 credit deducted immediately on successful booking
- Package business must match schedule business
- Remaining credits tracked on `Packages` table

### Bookings
- Overlapping schedule detection per user
- Slot cap enforced (DB + Redis)
- Full schedules return `SCHEDULE_FULL` — use `/api/waitlist`

### Cancellation
- \> 4 hours before start → 1 credit refunded
- ≤ 4 hours before start → no refund
- Slot release triggers FIFO waitlist promotion

### Waitlist
- FIFO by `JoinedAt`
- No credit reserved on join — deducted only on promotion
- Ineligible users (expired/zero credits/overlap) skipped to next in queue (marked `Expired` during promotion)
- Ended classes: Hangfire `WaitlistCleanupJob` sets still-`Waiting` entries to `Expired` (no charge, no booking)
- Waitlist rows are **not cached** — stored in MySQL only; timetable cache does not include waitlist counts

### Waitlist expiry (what happens)
When a `Waiting` entry’s class `EndTime` has passed:

1. Hangfire runs `WaitlistCleanupJob` on the configured interval (default 5 min)
2. Matching rows: `Status` → `Expired` (3)
3. **No** credit deducted, **no** booking created, row kept for history
4. User can join waitlist again for a future class if eligible

Debug: trigger **Recurring jobs → waitlist-cleanup → Trigger now** in `/hangfire`, or set `WaitlistCleanupIntervalMinutes` to `1`. Seeded case: user6 on past **Restorative Yoga** waitlist.

## Concurrency Strategy

**Problem:** Multiple users booking the last slot simultaneously must not overbook.

**Approach:** Redis per-schedule slot counter + short-lived distributed lock

1. Acquire `lock:schedule:{id}` (SET NX, 10s TTL)
2. Run Lua script atomically:
   - Initialize counter from DB count if missing
   - If count ≥ maxSlots → reject
   - Else INCR counter → accept
3. Re-check DB count before persisting
4. Save booking + deduct credit in MySQL transaction
5. On cancel → DECR counter; sync from DB after waitlist promotion

### Tradeoffs

| Approach | Pros | Cons |
|----------|------|------|
| **Redis atomic counter (chosen)** | Fast; handles burst concurrency | Counter can drift from DB; mitigated by re-check + sync |
| DB `SELECT FOR UPDATE` only | Strong consistency | Row lock contention under load |
| Optimistic concurrency token | No Redis dependency | Retries under contention; poor UX at last slot |

**Redis unavailable:** Booking fails fast (`SLOT_UNAVAILABLE`) rather than silently overbooking. Production would add circuit breaker + fallback to DB pessimistic locking.

Implementation: `src/StudioBooking.Infrastructure/Redis/RedisSlotReservationService.cs`

## Caching Strategy

Waitlist entries are **not** stored in Redis. Only packages and timetable responses are cached.

| Key pattern | TTL | Contents | Invalidated on |
|-------------|-----|----------|----------------|
| `packages:user:{id}` | 2 min | Packages + remaining credits | Purchase, book, cancel, refund, promotion |
| `timetable:{businessId}:{date}` | 2 min | Schedules, attendance, available slots (no waitlist data) | Book, cancel, waitlist join, promotion |
| `slots:{scheduleId}` | — | Redis slot counter (concurrency, not cache) | Book, cancel, promotion sync |

**Not invalidated:** waitlist expiry (Hangfire cleanup) — safe because waitlist is not exposed in cached API responses.

## Assumptions

1. **JWT auth** — Login with seeded credentials; no registration endpoint
2. **Waitlist credits** — Deduct on promotion only, not on join
3. **Separate waitlist endpoint** — `POST /api/bookings` rejects full schedules; clients call `POST /api/waitlist`
4. **Promotion skip** — If next FIFO user is ineligible, skip to next eligible entry
5. **UTC timestamps** — All dates stored and compared in UTC
6. **Package selection** — Client sends explicit `packageId` on book/waitlist
7. **Purchase mock** — No payment gateway; `validityDays` sets expiry
8. **Background cleanup** — Hangfire recurring job (`WaitlistCleanupJob`), interval from `Hangfire:WaitlistCleanupIntervalMinutes` (default 5)

## Scaling in Production

- **API:** Horizontal scaling behind load balancer; stateless JWT
- **MySQL:** Read replicas for timetable queries; primary for writes
- **Redis:** Redis Cluster for slot counters and cache
- **Concurrency:** dedicated booking queue per schedule
- **Waitlist promotion:** Message queue (eg.RabbitMQ) on cancellation events
- **Background jobs:** Hangfire dashboard at `/hangfire` (Development) for job monitoring
- **Observability:** Structured logging, rejection metrics, Redis/DB drift alerts

## Database Schema

See [docs/schema.md](docs/schema.md) for table definitions and SQL queries.  
**Draw.io ERD:** [docs/studio-booking-erd.drawio](docs/studio-booking-erd.drawio)
**Static ERD:** [docs/studio-booking-erd.png](docs/studio-booking-erd.png)
## EF Migrations

```bash
dotnet tool install dotnet-ef --version 8.0.11 --tool-path .tools
$env:PATH = ".tools;" + $env:PATH
dotnet ef migrations add MigrationName --project src/StudioBooking.Infrastructure --startup-project src/StudioBooking.Api
dotnet ef database update --project src/StudioBooking.Infrastructure --startup-project src/StudioBooking.Api
```

## Manual Test Scenarios

1. **Book class** — user1 → GET timetable → POST booking
2. **Zero credits** — user4 → `INSUFFICIENT_CREDITS`
3. **Expired package** — user5 → `PACKAGE_EXPIRED`
4. **Wrong business** — user1 + Business B schedule → `BUSINESS_MISMATCH`
5. **Full schedule** — POST booking on HIIT → `SCHEDULE_FULL` → POST waitlist
6. **Cancel + promote** — Cancel HIIT booking → waitlist user promoted
7. **Late cancel** — Cancel within 4h of start → no refund
8. **Concurrent booking** — Parallel requests on schedule with 1 slot left
9. **Waitlist expiry** — Login as user6; confirm Restorative Yoga waitlist row (`Status = 1`); trigger `waitlist-cleanup` in `/hangfire`; row becomes `Status = 3`, credits unchanged

## Unit Tests

```bash
dotnet test tests/StudioBooking.UnitTests/StudioBooking.UnitTests.csproj
```

Coverage includes package validation, booking/cancel refund rules, waitlist FIFO promotion, schedule overlap detection, and slot reservation failures.
