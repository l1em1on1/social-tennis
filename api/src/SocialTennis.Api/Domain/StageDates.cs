namespace SocialTennis.Api.Domain;

/// <summary>When a League's Stages run (ADR-0016: calendar dates, no clock).</summary>
public static class StageDates
{
    /// <summary>
    /// The first Stage: from the League's start date, for one Stage length,
    /// ending no later than the League's end date. It comes from the League
    /// alone, so starting the League late doesn't move it (#7).
    /// </summary>
    /// <remarks>
    /// A Stage of n months ends the day before the same day n months on: 1 Dec
    /// to 31 Dec. From the 31st, a shorter month clamps (.NET's AddMonths), so
    /// 31 Jan runs to 27 Feb and the next Stage would start on 28 Feb.
    /// </remarks>
    public static (DateOnly Start, DateOnly End) First(DateOnly leagueStart, DateOnly leagueEnd, StageLength length)
    {
        var next = length.Unit == StageLengthUnit.Weeks
            ? leagueStart.AddDays(7 * length.Count)
            : leagueStart.AddMonths(length.Count);
        var end = next.AddDays(-1);
        return (leagueStart, end > leagueEnd ? leagueEnd : end);
    }

    /// <summary>
    /// Whether a Stage ending on <paramref name="endDate"/> is past its
    /// deadline: the end of that day where the Club is (ADR-0016). The one
    /// place that says so. It locks nothing: a Stage stays open until it closes.
    /// </summary>
    public static bool DeadlinePassed(DateOnly endDate, string timeZone, TimeProvider clock) =>
        ClubCalendar.Today(timeZone, clock) > endDate;
}
