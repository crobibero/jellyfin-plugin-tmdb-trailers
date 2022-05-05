using System.Collections.Generic;
using System.Threading.Tasks;
using Jellyfin.Data.Entities;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.Tmdb.Trailers.Providers;

/// <summary>
/// Trailer intro provider.
/// </summary>
public class TrailerIntroProvider : IIntroProvider
{
    private readonly TmdbManager _tmdbManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrailerIntroProvider"/> class.
    /// </summary>
    /// <param name="tmdbManager">Instance of the <see cref="TmdbManager"/>.</param>
    public TrailerIntroProvider(TmdbManager tmdbManager)
    {
        _tmdbManager = tmdbManager;
    }

    /// <inheritdoc />
    public string Name => TmdbTrailerPlugin.Instance.Name;

    /// <inheritdoc />
    public Task<IEnumerable<IntroInfo>> GetIntros(BaseItem item, User user)
    {
        return Task.FromResult(_tmdbManager.GetIntros());
    }
}
