using FluentValidation;

namespace SocialTennis.Api.Features.Players.Contracts;

public record RenamePlayerRequest(string Name)
{
    public sealed class Validator : AbstractValidator<RenamePlayerRequest>
    {
        public Validator() => RuleFor(request => request.Name).PlayerName();
    }
}
