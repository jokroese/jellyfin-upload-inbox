using System;
using System.Collections.Generic;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.UploadInbox.Configuration;

/// <summary>
/// Who is allowed to upload to a target.
/// </summary>
public enum UploadAccessMode
{
    /// <summary>
    /// Any authenticated Jellyfin user may upload. (Default)
    /// </summary>
    AllUsers = 0,

    /// <summary>
    /// Only Jellyfin administrators may upload.
    /// </summary>
    AdminsOnly = 1,
}

/// <summary>
/// Upload inbox configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        Targets = new List<UploadTarget>();
    }

    /// <summary>
    /// Gets or sets the configured upload targets.
    /// </summary>
    public List<UploadTarget> Targets { get; set; }
}

/// <summary>
/// A configured upload target.
/// </summary>
public class UploadTarget
{
    /// <summary>
    /// Gets or sets the stable identifier for this target.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Gets or sets the Jellyfin library id this target points at.
    /// </summary>
    public string LibraryId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current Jellyfin library name for display.
    /// </summary>
    public string LibraryName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the selected library root path.
    /// </summary>
    public string LibraryPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets who is allowed to upload to this target.
    /// </summary>
    public UploadAccessMode AccessMode { get; set; } = UploadAccessMode.AllUsers;

    /// <summary>
    /// Gets or sets the maximum allowed file size in bytes.
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 20L * 1024 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the optional list of allowed file extensions (without dot). Empty means all extensions allowed.
    /// </summary>
    public List<string> AllowedExtensions { get; set; } = new List<string>();
}

