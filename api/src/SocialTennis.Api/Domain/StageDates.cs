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
    /// The Stage after one ending on <paramref name="previousEnd"/>: from the
    /// next day, for one Stage length, ending no later than the League's end.
    /// A League that started on the 5th goes on starting on the 5th, however
    /// late a Stage closes (owner, 2026-11-22); the Manager can change the
    /// dates at close. None once the League's end date leaves no day for one:
    /// that Stage was its last.
    /// </summary>
    /// <remarks>Months clamp as in <see cref="First"/>, so a Stage from the 31st drifts to the 28th after February.</remarks>
    public static (DateOnly Start, DateOnly End)? Next(DateOnly previousEnd, DateOnly leagueEnd, StageLength length)
    {
        var start = previousEnd.AddDays(1);
        return start > leagueEnd ? null : First(start, leagueEnd, length);
    }

    /// <summary>
    /// Whether a Stage ending on <paramref name="endDate"/> is past its
    /// deadline: the end of that day where the Club is (ADR-0016). The one
    /// place that says so. It locks nothing: a Stage stays open until it closes.
    /// </summary>
    public static bool DeadlinePassed(DateOnly endDate, string timeZone, TimeProvider clock) =>
        ClubCalendar.Today(timeZone, clock) > endDate;
}
