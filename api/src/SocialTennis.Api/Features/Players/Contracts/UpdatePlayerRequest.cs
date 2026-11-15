using FluentValidation;
using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Players.Contracts;

/// <summary>
/// Everything the holder may change, as a whole: a field left out is cleared.
/// There is no Rating here on purpose: only a Manager sets it (ADR-0007).
/// </summary>
public record UpdatePlayerRequest(string Name, PlayerGender? Gender = null, int? Level = null)
{
    public sealed class Validator : AbstractValidator<UpdatePlayerRequest>
    {
        public Validator()
        {
            RuleFor(request => request.Name).PlayerName();
            RuleFor(request => request.Gender).PlayerGender();
            RuleFor(request => request.Level).PlayerLevel();
        }
    }
}
