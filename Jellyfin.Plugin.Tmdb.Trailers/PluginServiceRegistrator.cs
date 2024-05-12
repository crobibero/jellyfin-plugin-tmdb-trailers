using Jellyfin.Plugin.Tmdb.Trailers.Channels;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.Tmdb.Trailers;

/// <inheritdoc />
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<TmdbManager>();
        serviceCollection.AddSingleton<IChannel, TmdbExtrasChannel>();
        serviceCollection.AddSingleton<IChannel, TmdbTrailerChannel>();
    }
}
