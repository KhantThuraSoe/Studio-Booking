using FluentAssertions;
using StudioBooking.Domain.Entities;

namespace StudioBooking.UnitTests.Domain;

public class PackageTests
{
    [Fact]
    public void IsExpired_ReturnsTrue_WhenExpiryDateIsInThePast()
    {
        var package = new Package { ExpiryDate = DateTime.UtcNow.AddDays(-1) };

        package.IsExpired(DateTime.UtcNow).Should().BeTrue();
    }

    [Fact]
    public void IsExpired_ReturnsFalse_WhenExpiryDateIsInTheFuture()
    {
        var package = new Package { ExpiryDate = DateTime.UtcNow.AddDays(1) };

        package.IsExpired(DateTime.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void HasAvailableCredits_ReturnsFalse_WhenRemainingCreditsIsZero()
    {
        var package = new Package { RemainingCredits = 0 };

        package.HasAvailableCredits.Should().BeFalse();
    }
}

public class TimetableScheduleTests
{
    [Theory]
    [InlineData("2026-06-14 10:00", "2026-06-14 11:00", "2026-06-14 10:30", "2026-06-14 11:30", true)]
    [InlineData("2026-06-14 10:00", "2026-06-14 11:00", "2026-06-14 11:00", "2026-06-14 12:00", false)]
    [InlineData("2026-06-14 10:00", "2026-06-14 11:00", "2026-06-14 08:00", "2026-06-14 09:00", false)]
    public void OverlapsWith_DetectsTimeRangeOverlap(
        string startA, string endA, string startB, string endB, bool expected)
    {
        var schedule = new TimetableSchedule
        {
            StartTime = DateTime.Parse(startA),
            EndTime = DateTime.Parse(endA)
        };

        schedule.OverlapsWith(DateTime.Parse(startB), DateTime.Parse(endB)).Should().Be(expected);
    }
}
