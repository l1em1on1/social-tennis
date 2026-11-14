using FluentValidation;
using SocialTennis.Api.Authentication;

namespace SocialTennis.Api.Features.Clubs.Contracts;

public record InviteToClubRequest(string Email)
{
    public sealed class Validator : AbstractValidator<InviteToClubRequest>
    {
        public Validator() => RuleFor(request => request.Email).ValidEmailAddress();
    }
}
