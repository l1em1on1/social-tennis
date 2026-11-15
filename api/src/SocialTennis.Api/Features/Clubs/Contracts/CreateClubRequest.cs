using FluentValidation;

namespace SocialTennis.Api.Features.Clubs.Contracts;

public record CreateClubRequest(string Name)
{
    public sealed class Validator : AbstractValidator<CreateClubRequest>
    {
        public Validator() => RuleFor(request => request.Name).ClubName();
    }
}
