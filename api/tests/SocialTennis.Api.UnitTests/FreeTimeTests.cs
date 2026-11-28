using SocialTennis.Api.Domain;

namespace SocialTennis.Api.UnitTests;

/// <summary>What counts on a date, and the time everyone in a Game shares (#276).</summary>
public class FreeTimeTests
{
    // 2 Dec 2026 is a Wednesday.
    private static readonly DateOnly Wednesday = new(2026, 12, 2);

    private static TimeOnly At(int hour, int minute = 0) => new(hour, minute);

    private static FreeWindow Window(int from, int to) => new(At(from), At(to));

    private static PlayerFreeTime Usually(params (Weekday Day, FreeWindow Window)[] week) =>
        new(new Dictionary<DateOnly, FreeWindow?>(), week.ToDictionary(d => d.Day, d => d.Window));

    [Fact]
    public void The_usual_week_stands_for_a_date_the_Player_hasnt_set()
    {
        var time = Usually((Weekday.Wednesday, Window(18, 22)), (Weekday.Saturday, FreeWindow.AnyTime));

        Assert.Equal(Window(18, 22), time.On(Wednesday));
        Assert.Equal(FreeWindow.AnyTime, time.On(Wednesday.AddDays(3)));
        Assert.Null(time.On(Wednesday.AddDays(1)));
        Assert.True(time.Given);
    }

    [Fact]
    public void A_dates_own_setting_wins_over_the_usual_week()
    {
        var time = new PlayerFreeTime(
            new Dictionary<DateOnly, FreeWindow?> { [Wednesday] = null, [Wednesday.AddDays(1)] = Window(9, 12) },
            new Dictionary<Weekday, FreeWindow> { [Weekday.Wednesday] = FreeWindow.AnyTime });

        Assert.Null(time.On(Wednesday));
        Assert.Equal(Window(9, 12), time.On(Wednesday.AddDays(1)));
        // The next Wednesday follows the usual week again.
        Assert.Equal(FreeWindow.AnyTime, time.On(Wednesday.AddDays(7)));
    }

    [Fact]
    public void Nothing_said_is_not_given_and_never_free()
    {
        var time = Usually();

        Assert.False(time.Given);
        Assert.Null(time.On(Wednesday));
    }

    [Fact]
    public void Windows_overlap_where_both_are_free()
    {
        Assert.Equal(Window(19, 21), Window(18, 21).Overlap(Window(19, 22)));
        Assert.Equal(Window(18, 21), FreeWindow.AnyTime.Overlap(Window(18, 21)));
        Assert.Equal(Window(18, 21), Window(18, 21).Overlap(FreeWindow.AnyTime));
        Assert.Equal(FreeWindow.AnyTime, FreeWindow.AnyTime.Overlap(FreeWindow.AnyTime));
    }

    [Fact]
    public void Windows_that_only_touch_or_miss_dont_overlap()
    {
        Assert.Null(Window(18, 20).Overlap(Window(20, 22)));
        Assert.Null(Window(9, 12).Overlap(Window(18, 22)));
    }

    [Fact]
    public void Shared_days_are_those_every_Player_is_free_with_the_time_they_all_share()
    {
        var evenings = Usually((Weekday.Wednesday, Window(18, 22)), (Weekday.Saturday, FreeWindow.AnyTime));
        var late = Usually((Weekday.Wednesday, Window(19, 23)), (Weekday.Thursday, FreeWindow.AnyTime), (Weekday.Saturday, Window(10, 12)));
        var anyWednesday = Usually((Weekday.Wednesday, FreeWindow.AnyTime), (Weekday.Saturday, Window(11, 18)));

        var shared = FreeTime.Shared([evenings, late, anyWednesday], Wednesday, Wednesday.AddDays(6));

        Assert.Equal(
            [(Wednesday, Window(19, 22)), (Wednesday.AddDays(3), Window(11, 12))],
            shared);
    }

    [Fact]
    public void A_day_whose_windows_dont_overlap_isnt_shared()
    {
        var mornings = Usually((Weekday.Wednesday, Window(9, 12)));
        var evenings = Usually((Weekday.Wednesday, Window(18, 22)));

        Assert.Empty(FreeTime.Shared([mornings, evenings], Wednesday, Wednesday));
    }

    [Fact]
    public void Shared_days_run_from_the_first_day_to_the_last_only()
    {
        var always = Usually([.. Enum.GetValues<Weekday>().Select(day => (day, FreeWindow.AnyTime))]);

        var shared = FreeTime.Shared([always, always], Wednesday, Wednesday.AddDays(2));

        Assert.Equal([Wednesday, Wednesday.AddDays(1), Wednesday.AddDays(2)], shared.Select(day => day.Date));
        Assert.Empty(FreeTime.Shared([], Wednesday, Wednesday.AddDays(2)));
    }

    [Fact]
    public void Weekdays_run_Monday_to_Sunday()
    {
        Assert.Equal(Weekday.Wednesday, Weekdays.Of(Wednesday));
        Assert.Equal(Weekday.Sunday, Weekdays.Of(new DateOnly(2026, 12, 6)));
        Assert.Equal(Weekday.Monday, Weekdays.Of(new DateOnly(2026, 12, 7)));
    }
}
