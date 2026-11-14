using FluentValidation;

namespace SocialTennis.Api.Features.Players.Contracts;

public record CreatePlayerRequest(string Name)
{
    public sealed class Validator : AbstractValidator<CreatePlayerRequest>
    {
        public Validator() => RuleFor(request => request.Name).PlayerName();
    }
}
