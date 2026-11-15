using FluentValidation;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Features.Players.Contracts;

namespace SocialTennis.Api.Features.Clubs.Contracts;

/// <param name="Email">Whose Player it is: the address they will sign in with.</param>
public record AddClubPlayerRequest(string Name, string Email)
{
    public sealed class Validator : AbstractValidator<AddClubPlayerRequest>
    {
        public Validator()
        {
            RuleFor(request => request.Name).PlayerName();
            RuleFor(request => request.Email).ValidEmailAddress();
        }
    }
}
