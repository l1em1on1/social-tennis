using SocialTennis.Api.Domain;

namespace SocialTennis.Api.UnitTests;

/// <summary>The ladder's movement at the end of a Stage (#10, ADR-0031).</summary>
public class RelegationTests
{
    private static readonly Guid[] T = [.. Enumerable.Range(1, 12).Select(n => new Guid($"00000000-0000-0000-0000-{n:D12}"))];

    private static IReadOnlyList<IReadOnlyList<Guid>> Divisions(params int[][] teams) =>
        [.. teams.Select(division => (IReadOnlyList<Guid>)[.. division.Select(n => T[n - 1])])];

    private static int[][] Numbers(IReadOnlyList<IReadOnlyList<Guid>> divisions) =>
        [.. divisions.Select(division => division.Select(team => Array.IndexOf(T, team) + 1).ToArray())];

    [Fact]
    public void Each_Divisions_first_goes_up_and_last_goes_down_the_ends_of_the_ladder_staying_put()
    {
        var next = Relegation.Next(Divisions([1, 2, 3], [4, 5, 6], [7, 8, 9]), []);

        // 4 up to the bottom of Division 1, 3 down to the top of Division 2; likewise 7 and 6.
        Assert.Equal([[1, 2, 4], [3, 5, 7], [6, 8, 9]], Numbers(next));
    }

    [Fact]
    public void Every_Division_keeps_its_size_however_uneven()
    {
        var next = Relegation.Next(Divisions([1, 2, 3], [4, 5, 6, 7], [8, 9, 10, 11, 12]), []);

        Assert.Equal([3, 4, 5], next.Select(d => d.Count));
        Assert.Equal([[1, 2, 4], [3, 5, 6, 8], [7, 9, 10, 11, 12]], Numbers(next));
    }

    [Fact]
    public void A_middle_Division_of_two_loses_both_its_Teams()
    {
        var next = Relegation.Next(Divisions([1, 2], [3, 4], [5, 6]), []);

        Assert.Equal([[1, 3], [2, 5], [4, 6]], Numbers(next));
    }

    [Fact]
    public void A_ladder_of_one_Division_moves_nobody()
    {
        Assert.Equal([[1, 2, 3, 4]], Numbers(Relegation.Next(Divisions([1, 2, 3, 4]), [])));
    }

    [Fact]
    public void Teams_joining_start_at_the_bottom_of_the_bottom_Division_after_the_moves()
    {
        var next = Relegation.Next(Divisions([1, 2], [3, 4]), [T[9], T[10]]);

        Assert.Equal([[1, 3], [2, 4, 10, 11]], Numbers(next));
    }
}
