using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.UploadInbox.Configuration;
using Jellyfin.Plugin.UploadInbox.Models;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.UploadInbox.Services;

/// <summary>
/// Resolves configured upload targets against current Jellyfin library roots.
/// </summary>
public class LibraryTargetResolver
{
    private readonly ILibraryManager _libraryManager;

    public LibraryTargetResolver(ILibraryManager libraryManager)
    {
        _libraryManager = libraryManager;
    }

    public IReadOnlyList<LibraryRootInfo> GetAvailableLibraryRoots()
    {
        return _libraryManager
            .GetVirtualFolders()
            .Where(v => !string.IsNullOrWhiteSpace(v.ItemId) && v.Locations is not null)
            .SelectMany(v => v.Locations.Select(path => new LibraryRootInfo
            {
                LibraryId = v.ItemId,
                LibraryName = v.Name ?? string.Empty,
                LibraryPath = path ?? string.Empty,
            }))
            .Where(v => !string.IsNullOrWhiteSpace(v.LibraryPath))
            .ToList();
    }

    public bool TryResolveTarget(
        UploadTarget target,
        out LibraryRootInfo? resolved,
        out string? error)
    {
        resolved = null;
        error = null;

        if (string.IsNullOrWhiteSpace(target.LibraryId))
        {
            error = "No Jellyfin library has been selected for this upload target.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(target.LibraryPath))
        {
            error = "No Jellyfin library folder has been selected for this upload target.";
            return false;
        }

        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        resolved = GetAvailableLibraryRoots()
            .FirstOrDefault(x =>
                string.Equals(x.LibraryId, target.LibraryId, StringComparison.Ordinal) &&
                string.Equals(x.LibraryPath, target.LibraryPath, comparison));

        if (resolved is null)
        {
            error = "The selected library folder is no longer configured in Jellyfin. Update the plugin settings and choose a current library root.";
            return false;
        }

        return true;
    }
}
