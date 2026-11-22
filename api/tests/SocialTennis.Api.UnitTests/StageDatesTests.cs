using System.Globalization;
using SocialTennis.Api.Domain;

namespace SocialTennis.Api.UnitTests;

/// <summary>The first Stage's dates come from the League alone (#7, ADR-0016).</summary>
public class StageDatesTests
{
    private static readonly DateOnly FarEnd = new(2030, 1, 1);

    [Fact]
    public void A_month_long_Stage_from_the_first_ends_on_the_last_day_of_the_month() =>
        Assert.Equal(
            (new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31)),
            StageDates.First(new DateOnly(2026, 12, 1), FarEnd, new StageLength(1, StageLengthUnit.Months)));

    [Fact]
    public void Two_weeks_from_a_Monday_end_on_the_Sunday_a_fortnight_later() =>
        Assert.Equal(
            (new DateOnly(2027, 4, 5), new DateOnly(2027, 4, 18)),
            StageDates.First(new DateOnly(2027, 4, 5), FarEnd, new StageLength(2, StageLengthUnit.Weeks)));

    [Fact]
    public void A_month_from_the_31st_ends_the_day_before_the_clamped_date_a_month_on() =>
        // 31 Jan plus a month is 28 Feb (AddMonths clamps), so the Stage runs to 27 Feb.
        Assert.Equal(
            (new DateOnly(2027, 1, 31), new DateOnly(2027, 2, 27)),
            StageDates.First(new DateOnly(2027, 1, 31), FarEnd, new StageLength(1, StageLengthUnit.Months)));

    [Fact]
    public void A_Stage_ends_no_later_than_the_League() =>
        Assert.Equal(
            (new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 20)),
            StageDates.First(new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 20), new StageLength(3, StageLengthUnit.Months)));

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Theory]
    // 30 November in London is GMT: the deadline passes at midnight UTC.
    [InlineData("2026-11-30T23:59:00Z", false)]
    [InlineData("2026-12-01T00:00:00Z", true)]
    // 31 July is on summer time: it passes at 23:00 UTC.
    [InlineData("2026-07-31T22:59:00Z", false)]
    [InlineData("2026-07-31T23:00:00Z", true)]
    public void A_Stages_deadline_passes_at_the_end_of_its_last_day_where_the_Club_is(string now, bool passed)
    {
        var end = DateTimeOffset.Parse(now, CultureInfo.InvariantCulture).Month == 7 ? new DateOnly(2026, 7, 31) : new DateOnly(2026, 11, 30);

        Assert.Equal(passed, StageDates.DeadlinePassed(end, "Europe/London", new FixedClock(DateTimeOffset.Parse(now, CultureInfo.InvariantCulture))));
    }
}
