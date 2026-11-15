using FluentValidation;

namespace SocialTennis.Api.Features.Clubs.Contracts;

public record RenameClubRequest(string Name)
{
    public sealed class Validator : AbstractValidator<RenameClubRequest>
    {
        public Validator() => RuleFor(request => request.Name).ClubName();
    }
}
