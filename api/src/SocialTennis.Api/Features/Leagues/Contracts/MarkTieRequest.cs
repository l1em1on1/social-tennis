using FluentValidation;
using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>
/// A Foursomes Player says who goes up, or down, of the Players level on games
/// in their Division (#14 part 3): however they settled it, a coin toss included.
/// </summary>
/// <param name="PlayerId">The Player saying so, one of the signed-in User's, in that Division.</param>
/// <param name="Division">The Division, 1 at the top.</param>
/// <param name="Place">Up: who goes up from first. Down: who goes down from last.</param>
/// <param name="TeamId">The Team of one of the Player who moves.</param>
public record MarkTieRequest(Guid PlayerId, int Division, TieMarkPlace Place, Guid TeamId)
{
    public sealed class Validator : AbstractValidator<MarkTieRequest>
    {
        public Validator()
        {
            RuleFor(request => request.PlayerId).NotEmpty().WithMessage("Say which Player is marking it.").OverridePropertyName("playerId");
            RuleFor(request => request.Division).GreaterThanOrEqualTo(1).WithMessage("Divisions are numbered from 1, at the top.").OverridePropertyName("division");
            RuleFor(request => request.Place).IsInEnum().WithMessage("Say whether it's who goes up or who goes down.").OverridePropertyName("place");
            RuleFor(request => request.TeamId).NotEmpty().WithMessage("Say who goes.").OverridePropertyName("teamId");
        }
    }
}
