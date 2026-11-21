using FluentValidation;

namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>
/// The start the Manager confirmed (#7): the ladder as they ordered it, top
/// first, and what they saw would happen to everyone else. If the League has
/// changed since, the start is refused rather than leave out or remove someone
/// the Manager didn't see.
/// </summary>
/// <param name="Ladder">The Teams to place, top first.</param>
/// <param name="LeftOut">The Teams the Manager left out: they stay in the League, not placed.</param>
/// <param name="Removed">The doubles Players without a partner, whom the start removes.</param>
public record StartLeagueRequest(IReadOnlyList<Guid> Ladder, IReadOnlyList<Guid> LeftOut, IReadOnlyList<Guid> Removed)
{
    public sealed class Validator : AbstractValidator<StartLeagueRequest>
    {
        public Validator()
        {
            RuleFor(request => request.Ladder)
                .Cascade(CascadeMode.Stop)
                .NotNull()
                .WithMessage("The ladder is required.")
                .Must(ladder => ladder.Count >= 2)
                .WithMessage("A ladder needs at least two Competitors.")
                .OverridePropertyName("ladder");
            RuleFor(request => request.LeftOut)
                .NotNull()
                .WithMessage("Say who is left out, even if nobody is.")
                .OverridePropertyName("leftOut");
            RuleFor(request => request.Removed)
                .Cascade(CascadeMode.Stop)
                .NotNull()
                .WithMessage("Say who is removed, even if nobody is.")
                .Must(removed => removed.Distinct().Count() == removed.Count)
                .WithMessage("Each Player is listed once.")
                .OverridePropertyName("removed");
            RuleFor(request => request)
                .Must(request => request.Ladder.Concat(request.LeftOut).Distinct().Count() == request.Ladder.Count + request.LeftOut.Count)
                .When(request => request.Ladder is not null && request.LeftOut is not null)
                .WithMessage("Each Team is placed once, or left out.")
                .OverridePropertyName("ladder");
        }
    }
}
