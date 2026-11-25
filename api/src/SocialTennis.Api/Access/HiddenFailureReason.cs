using Microsoft.AspNetCore.Authorization;

namespace SocialTennis.Api.Access;

/// <summary>Why a manager policy failed, when the caller may not know the thing exists.</summary>
public sealed class HiddenFailureReason(IAuthorizationHandler handler)
    : AuthorizationFailureReason(handler, "The caller can't see what the route names.");
