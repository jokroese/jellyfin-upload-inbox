using System;
using System.Linq;
using Jellyfin.Plugin.UploadInbox.Configuration;

namespace Jellyfin.Plugin.UploadInbox.Services;

/// <summary>
/// Evaluates whether a user is allowed to upload to a target.
/// </summary>
public class UploadAuthoriser
{
    public bool TryEnsureAllowed(PluginConfiguration configuration, Guid userId, string targetId, out UploadTarget? target)
    {
        target = configuration.Targets.FirstOrDefault(t => string.Equals(t.Id, targetId, StringComparison.Ordinal));
        if (target is null)
        {
            return false;
        }

        if (target.AllowedUserIds is null || target.AllowedUserIds.Count == 0)
        {
            return false;
        }

        return target.AllowedUserIds.Contains(userId);
    }
}

