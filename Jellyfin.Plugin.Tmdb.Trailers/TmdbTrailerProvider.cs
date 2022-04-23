using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Data.Entities;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Tmdb.Trailers
{
    /// <summary>
    /// Intro TmdbTrailer Provider.
    /// </summary>
    public class TmdbTrailerProvider : IIntroProvider
    {
        private readonly TmdbManager _tmdbManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="TmdbTrailerProvider"/> class.
        /// </summary>
        /// <param name="tmdbManager">Instance of the <see cref="TmdbManager"/>.</param>
        public TmdbTrailerProvider(TmdbManager tmdbManager)
        {
            _tmdbManager = tmdbManager;
        }

        /// <inheritdoc/>
        public string Name { get; } = "Intros";

        /// <inheritdoc/>
        public Task<IEnumerable<IntroInfo>> GetIntros(BaseItem item, User user)
        {
            return Task.FromResult(_tmdbManager.Get());
        }

        /// <inheritdoc/>
        public IEnumerable<string> GetAllIntroFiles()
        {
            // not implemented on server
            return Enumerable.Empty<string>();
        }
    }
}