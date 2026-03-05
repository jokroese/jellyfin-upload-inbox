using System;
using System.Linq;
using System.Security.Claims;

namespace Jellyfin.Plugin.UploadInbox.Extensions;

/// <summary>
/// Extensions for working with Jellyfin authentication claims.
/// </summary>
internal static class ClaimsPrincipalExtensions
{
    private const string UserIdClaimType = "Jellyfin-UserId";

    /// <summary>
    /// Gets the current Jellyfin user id from the claims principal.
    /// </summary>
    /// <param name="principal">The authenticated principal.</param>
    /// <returns>The user id, or <see cref="Guid.Empty"/> if not found.</returns>
    public static Guid GetJellyfinUserId(this ClaimsPrincipal principal)
    {
        if (principal is null)
        {
            throw new ArgumentNullException(nameof(principal));
        }

        var value = principal.Claims
            .FirstOrDefault(c => string.Equals(c.Type, UserIdClaimType, StringComparison.OrdinalIgnoreCase))
            ?.Value;

        return Guid.TryParse(value, out var id)
            ? id
            : Guid.Empty;
    }
}

