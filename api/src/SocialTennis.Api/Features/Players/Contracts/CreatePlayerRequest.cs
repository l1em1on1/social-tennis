using FluentValidation;
using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Players.Contracts;

/// <param name="Gender">Optional: the first sign-in asks only for a name.</param>
/// <param name="Level">Optional, 1 to 5.</param>
public record CreatePlayerRequest(string Name, PlayerGender? Gender = null, int? Level = null)
{
    public sealed class Validator : AbstractValidator<CreatePlayerRequest>
    {
        public Validator()
        {
            RuleFor(request => request.Name).PlayerName();
            RuleFor(request => request.Gender).PlayerGender();
            RuleFor(request => request.Level).PlayerLevel();
        }
    }
}
