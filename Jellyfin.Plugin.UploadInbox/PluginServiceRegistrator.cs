using Jellyfin.Plugin.UploadInbox.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.UploadInbox;

/// <summary>
/// Registers plugin services with Jellyfin's DI container.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<FilePathPolicy>();
        serviceCollection.AddSingleton<UploadAuthoriser>();
        serviceCollection.AddSingleton<LibraryTargetResolver>();
        serviceCollection.AddSingleton<UploadSessionStore>();
    }
}

