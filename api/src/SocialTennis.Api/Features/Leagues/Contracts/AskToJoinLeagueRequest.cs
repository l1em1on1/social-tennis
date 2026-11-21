using FluentValidation;

namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <param name="PlayerId">Which of the signed-in User's Players is asking.</param>
/// <param name="PartnerId">
/// In a doubles League, the Club member they ask to join with (#6); omitted to
/// ask alone. The partner needn't agree in the app: they see the request and
/// can take it back.
/// </param>
public record AskToJoinLeagueRequest(Guid PlayerId, Guid? PartnerId = null)
{
    public sealed class Validator : AbstractValidator<AskToJoinLeagueRequest>
    {
        public Validator() =>
            RuleFor(request => request.PartnerId)
                .Must((request, partnerId) => partnerId != request.PlayerId)
                .WithMessage("A partner is another Player.")
                .OverridePropertyName("partnerId");
    }
}
