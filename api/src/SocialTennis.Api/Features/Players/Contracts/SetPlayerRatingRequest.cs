using FluentValidation;
using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Players.Contracts;

public record SetPlayerRatingRequest(int Rating)
{
    public sealed class Validator : AbstractValidator<SetPlayerRatingRequest>
    {
        public Validator() =>
            RuleFor(request => request.Rating)
                .InclusiveBetween(Player.MinRating, Player.MaxRating)
                .WithMessage($"A rating is between {Player.MinRating} and {Player.MaxRating}.")
                .OverridePropertyName("rating");
    }
}
