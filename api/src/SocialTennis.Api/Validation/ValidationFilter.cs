namespace SocialTennis.Api.Validation;

/// <summary>
/// Runs <see cref="IValidatable.Validate"/> on the bound request before the
/// handler sees it, short-circuiting with a 400 ValidationProblem. One generic
/// filter serves every contract — attach it via
/// <see cref="EndpointValidationExtensions.ValidatesBody{T}"/>, never directly,
/// so the OpenAPI declaration can't be forgotten.
/// </summary>
public sealed class ValidationFilter<T> : IEndpointFilter
    where T : IValidatable
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next) =>
        context.Arguments.OfType<T>().FirstOrDefault()?.Validate() is { } errors
            ? TypedResults.ValidationProblem(errors)
            : await next(context);
}
