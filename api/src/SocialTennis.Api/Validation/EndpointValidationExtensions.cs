namespace SocialTennis.Api.Validation;

public static class EndpointValidationExtensions
{
    /// <summary>
    /// Validates the request body against <typeparamref name="T"/>'s own rules
    /// and declares the resulting 400 on the endpoint.
    /// </summary>
    /// <remarks>
    /// The two halves are deliberately inseparable. Validation moved out of the
    /// handler's <c>Results&lt;&gt;</c> union into a filter, so the 400 is no
    /// longer inferred from the return type — without the metadata the endpoint
    /// still works while the generated TS client silently stops knowing the
    /// response exists. Binding them into one call makes that desync
    /// unreachable.
    /// </remarks>
    public static RouteHandlerBuilder ValidatesBody<T>(this RouteHandlerBuilder builder)
        where T : IValidatable =>
        builder
            .AddEndpointFilter<ValidationFilter<T>>()
            .ProducesValidationProblem();
}
