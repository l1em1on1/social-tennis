using FluentValidation;

namespace SocialTennis.Api.Validation;

/// <summary>
/// Runs <typeparamref name="TValidator"/> against the bound request before the
/// handler sees it, short-circuiting with a 400 ValidationProblem. One generic
/// filter serves every contract — attach it via
/// <see cref="EndpointValidationExtensions.ValidatesBody{TRequest, TValidator}"/>,
/// never directly, so the OpenAPI declaration can't be forgotten.
/// </summary>
/// <remarks>
/// The validator is constructed rather than resolved from DI. The <c>new()</c>
/// constraint means a validator cannot take dependencies at all, which turns
/// "validation never touches the database" from a documented rule into one the
/// compiler enforces (ADR-0011).
/// </remarks>
public sealed class ValidationFilter<TRequest, TValidator> : IEndpointFilter
    where TRequest : class
    where TValidator : AbstractValidator<TRequest>, new()
{
    // One instance per closed generic type. A FluentValidation validator is
    // immutable once its constructor has run the RuleFor chain, so sharing it
    // across requests is safe and avoids rebuilding the rules per call.
    private static readonly TValidator Validator = new();

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        if (context.Arguments.OfType<TRequest>().FirstOrDefault() is not { } request)
        {
            return await next(context);
        }

        var result = await Validator.ValidateAsync(request, context.HttpContext.RequestAborted);

        return result.IsValid
            ? await next(context)
            : TypedResults.ValidationProblem(result.ToDictionary());
    }
}
