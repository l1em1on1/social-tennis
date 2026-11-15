using Microsoft.Extensions.Options;

namespace SocialTennis.Api.Email;

/// <summary>
/// Checks the Email section against the environment it runs in, at startup
/// (<see cref="OptionsBuilderExtensions.ValidateOnStart{TOptions}"/>), so the
/// failure names the exact setting to fix.
/// </summary>
public sealed class EmailOptionsValidator(IHostEnvironment environment) : IValidateOptions<EmailOptions>
{
    public ValidateOptionsResult Validate(string? name, EmailOptions options) =>
        options.Problem(environment.IsDevelopment()) is { } problem
            ? ValidateOptionsResult.Fail(problem)
            : ValidateOptionsResult.Success;
}
