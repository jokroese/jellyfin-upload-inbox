using System;
using System.Linq;
using Jellyfin.Plugin.UploadInbox.Configuration;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.UploadInbox.Services;

/// <summary>
/// Evaluates whether a user is allowed to upload to a target.
/// </summary>
public class UploadAuthoriser
{
    private readonly IUserManager _userManager;

    public UploadAuthoriser(IUserManager userManager)
    {
        _userManager = userManager;
    }

    public bool TryEnsureAllowed(PluginConfiguration configuration, Guid userId, string targetId, out UploadTarget? target)
    {
        target = configuration.Targets.FirstOrDefault(t => string.Equals(t.Id, targetId, StringComparison.Ordinal));
        if (target is null)
        {
            return false;
        }

        switch (target.AccessMode)
        {
            case UploadAccessMode.AllUsers:
                return true;

            case UploadAccessMode.AdminsOnly:
            {
                if (userId == Guid.Empty)
                {
                    return false;
                }

                var user = _userManager.GetUserById(userId);
                if (user is null)
                {
                    return false;
                }

                // IUserManager returns a DB entity type; convert to UserDto to read policy flags.
                // Policy.IsAdministrator is the canonical "admin" bit exposed by Jellyfin's API model.
                var dto = _userManager.GetUserDto(user);
                return dto.Policy?.IsAdministrator == true;
            }

            default:
                // Future-proof: deny unknown modes.
                return false;
        }
    }
}

