using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Tmdb.Trailers.Channels
{
    /// <summary>
    /// Trailers Channel.
    /// </summary>
    public class TmdbExtrasChannel : IChannel, IDisableMediaSourceDisplay, IDisposable
    {
        private readonly ILogger<TmdbExtrasChannel> _logger;
        private readonly TmdbManager _tmdbManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="TmdbExtrasChannel"/> class.
        /// </summary>
        /// <param name="loggerFactory">Instance of the <see cref="ILoggerFactory"/> interface.</param>
        /// <param name="memoryCache">Instance of the <see cref="IMemoryCache"/> interface.</param>
        public TmdbExtrasChannel(ILoggerFactory loggerFactory, IMemoryCache memoryCache)
        {
            _logger = loggerFactory.CreateLogger<TmdbExtrasChannel>();
            _logger.LogDebug(nameof(TmdbExtrasChannel));
            _tmdbManager = new TmdbManager(loggerFactory, memoryCache);
        }

        /// <inheritdoc />
        public string Name => "TMDb Extras";

        /// <inheritdoc />
        public string Description => TmdbTrailerPlugin.Instance.Description;

        /// <inheritdoc />
        public string DataVersion => Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

        /// <inheritdoc />
        public string HomePageUrl => "https://jellyfin.org";

        /// <inheritdoc />
        public ChannelParentalRating ParentalRating => ChannelParentalRating.GeneralAudience;

        /// <inheritdoc />
        public InternalChannelFeatures GetChannelFeatures()
        {
            _logger.LogDebug(nameof(GetChannelFeatures));
            return new InternalChannelFeatures
            {
                MediaTypes = new List<ChannelMediaType>
                {
                    ChannelMediaType.Video
                },
                ContentTypes = new List<ChannelMediaContentType>
                {
                    ChannelMediaContentType.MovieExtra
                },
                MaxPageSize = TmdbManager.PageSize,
                AutoRefreshLevels = 4
            };
        }

        /// <inheritdoc />
        public bool IsEnabledFor(string userId)
        {
            return TmdbTrailerPlugin.Instance.Configuration.EnableExtrasChannel;
        }

        /// <inheritdoc />
        public Task<ChannelItemResult> GetChannelItems(InternalChannelItemQuery query, CancellationToken cancellationToken)
        {
            _logger.LogDebug("{function} Query={@query}", nameof(GetChannelItems), query);
            return _tmdbManager.GetChannelItems(query, cancellationToken);
        }

        /// <inheritdoc />
        public Task<DynamicImageResponse> GetChannelImage(ImageType type, CancellationToken cancellationToken)
        {
            return _tmdbManager.GetChannelImage(type);
        }

        /// <inheritdoc />
        public IEnumerable<ImageType> GetSupportedChannelImages()
        {
            return _tmdbManager.GetSupportedChannelImages();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose.
        /// </summary>
        /// <param name="disposing">Dispose everything.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _tmdbManager?.Dispose();
            }
        }
    }
}