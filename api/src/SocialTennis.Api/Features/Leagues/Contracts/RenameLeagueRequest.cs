using FluentValidation;

namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>A League's new name (#153). Nothing else about the League changes.</summary>
public record RenameLeagueRequest(string Name)
{
    public sealed class Validator : AbstractValidator<RenameLeagueRequest>
    {
        public Validator() => RuleFor(request => request.Name).LeagueName();
    }
}
