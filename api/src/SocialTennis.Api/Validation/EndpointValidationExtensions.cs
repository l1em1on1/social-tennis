using FluentValidation;

namespace SocialTennis.Api.Validation;

public static class EndpointValidationExtensions
{
    /// <summary>
    /// Validates the request body with <typeparamref name="TValidator"/> and
    /// declares the resulting 400 on the endpoint.
    /// </summary>
    /// <remarks>
    /// The two halves are deliberately inseparable. Validation lives in a filter
    /// rather than the handler's <c>Results&lt;&gt;</c> union, so the 400 is no
    /// longer inferred from the return type — without the metadata the endpoint
    /// still works while the generated TS client silently stops knowing the
    /// response exists. Binding them into one call makes that desync
    /// unreachable.
    ///
    /// Naming the validator at the call site is what makes the wiring
    /// compile-checked: a contract marked for validation with no matching
    /// validator does not build, rather than failing open at runtime.
    /// </remarks>
    public static RouteHandlerBuilder ValidatesBody<TRequest, TValidator>(
        this RouteHandlerBuilder builder)
        where TRequest : class
        where TValidator : AbstractValidator<TRequest>, new() =>
        builder
            .AddEndpointFilter<ValidationFilter<TRequest, TValidator>>()
            .ProducesValidationProblem();
}
