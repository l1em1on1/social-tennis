using System.Text.Json.Serialization;

namespace SocialTennis.Api.Domain;

/// <summary>
/// A request to leave a running League (#21; CONTEXT.md, Withdrawal): about a
/// Team's place on the ladder, which a Player in it asks for and the Manager
/// decides. Nothing changes until the Manager accepts. A Manager may also
/// withdraw a Team directly, which is recorded as an accepted request that
/// nobody asked for. At most one waits per Team.
/// </summary>
public class WithdrawalRequest
{
    public Guid Id { get; set; }

    public Guid LeagueId { get; set; }

    public Guid TeamId { get; set; }

    public Team Team { get; set; } = null!;

    /// <summary>The Player who asked; none when the Manager withdrew the Team directly.</summary>
    public Guid? PlayerId { get; set; }

    public Player? Player { get; set; }

    public DateTimeOffset RequestedAt { get; set; }

    public WithdrawalStatus Status { get; set; }

    public DateTimeOffset? DecidedAt { get; set; }

    /// <summary>The Manager who decided; none once their account is deleted (#116).</summary>
    public Guid? DecidedByUserId { get; set; }
}

/// <summary>Where a withdrawal request stands. A Player cancelling one that waits deletes it.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<WithdrawalStatus>))]
public enum WithdrawalStatus
{
    Pending = 1,
    Accepted = 2,
    Declined = 3,
}
