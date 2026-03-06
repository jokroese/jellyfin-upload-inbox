using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Jellyfin.Plugin.UploadInbox.Configuration;

namespace Jellyfin.Plugin.UploadInbox;

/// <summary>
/// The main plugin.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Application paths.</param>
    /// <param name="xmlSerializer">XML serializer.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <inheritdoc />
    public override string Name => "Upload Inbox";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("b3ff3bcd-9b77-4a5e-9c22-3c5236757d12");

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        var ns = GetType().Namespace ?? "Jellyfin.Plugin.UploadInbox";

        return new[]
        {
            new PluginPageInfo
            {
                Name = "UploadInboxConfig",
                EmbeddedResourcePath = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}.Pages.config.html",
                    ns),
            },
            // Controller script referenced by config.html via data-controller="__plugin/UploadInbox/config.js"
            // Jellyfin will request: /web/configurationpage?name=UploadInbox/config.js
            new PluginPageInfo
            {
                Name = "UploadInbox/config.js",
                EmbeddedResourcePath = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}.Pages.config.js",
                    ns),
            },
            // Controller script referenced by upload.html via data-controller="__plugin/UploadInbox/upload.js"
            // Jellyfin will request: /web/configurationpage?name=UploadInbox/upload.js
            new PluginPageInfo
            {
                Name = "UploadInbox/upload.js",
                EmbeddedResourcePath = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}.Pages.upload.js",
                    ns),
            },
            new PluginPageInfo
            {
                Name = "UploadInbox",
                EmbeddedResourcePath = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}.Pages.upload.html",
                    ns),
                EnableInMainMenu = true,
                DisplayName = "Upload Inbox",
                MenuSection = "server",
                MenuIcon = "upload",
            },
        };
    }
}

