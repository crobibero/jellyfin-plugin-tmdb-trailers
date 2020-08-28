using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Tmdb.Trailers.Config;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.MediaInfo;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TMDbLib.Client;
using TMDbLib.Objects.General;
using TMDbLib.Objects.Search;
using YouTubeFetcher.Core.Factories;
using YouTubeFetcher.Core.Services.Interfaces;

namespace Jellyfin.Plugin.Tmdb.Trailers
{
    /// <summary>
    /// Trailers Channel.
    /// </summary>
    public class Channel : IChannel, IDisposable
    {
        // tmdb always returns 20 items.
        private const int PageSize = 20;

        private readonly ILogger<Channel> _logger;
        private readonly IMemoryCache _memoryCache;

        private readonly TMDbClient _client;
        private readonly PluginConfiguration _configuration;
        private readonly IYouTubeService _youTubeService;

        /// <summary>
        /// Initializes a new instance of the <see cref="Channel"/> class.
        /// </summary>
        /// <param name="logger">Instance of the <see cref="ILogger{Channel}"/> interface.</param>
        /// <param name="memoryCache">Instance of the <see cref="IMemoryCache"/> interface.</param>
        public Channel(ILogger<Channel> logger, IMemoryCache memoryCache)
        {
            _logger = logger;
            _memoryCache = memoryCache;

            _configuration = TmdbTrailerPlugin.Instance.Configuration;
            _client = new TMDbClient(_configuration.ApiKey);
            _youTubeService = new YouTubeServiceFactory().Create();
        }

        /// <inheritdoc />
        public string Name => TmdbTrailerPlugin.Instance.Name;

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
                ContentTypes = new List<ChannelMediaContentType>
                {
                    ChannelMediaContentType.MovieExtra
                },
                MediaTypes = new List<ChannelMediaType>
                {
                    ChannelMediaType.Video
                },
                MaxPageSize = PageSize
            };
        }

        /// <inheritdoc />
        public bool IsEnabledFor(string userId)
        {
            // Enabled for all users.
            return true;
        }

        /// <inheritdoc />
        public async Task<ChannelItemResult> GetChannelItems(InternalChannelItemQuery query, CancellationToken cancellationToken)
        {
            // Initial entry
            if (string.IsNullOrEmpty(query.FolderId))
            {
                return GetChannelTypes();
            }

            if (_memoryCache.TryGetValue(query.FolderId, out ChannelItemResult cachedValue))
            {
                _logger.LogDebug("Function={function} FolderId={folderId} Cache Hit", nameof(GetChannelItems), query.FolderId);
                return cachedValue;
            }

            ChannelItemResult result = null;

            // Get upcoming movies.
            if (query.FolderId.Equals("upcoming", StringComparison.OrdinalIgnoreCase))
            {
                result = await GetUpcomingMoviesAsync(query, cancellationToken).ConfigureAwait(false);
            }

            // Get now playing movies.
            else if (query.FolderId.Equals("nowplaying", StringComparison.OrdinalIgnoreCase))
            {
                result = await GetNowPlayingMoviesAsync(query, cancellationToken).ConfigureAwait(false);
            }

            // Get popular movies.
            else if (query.FolderId.Equals("popular", StringComparison.OrdinalIgnoreCase))
            {
                result = await GetPopularMoviesAsync(query, cancellationToken).ConfigureAwait(false);
            }

            // Get top rated movies.
            else if (query.FolderId.Equals("toprated", StringComparison.OrdinalIgnoreCase))
            {
                result = await GetTopRatedMoviesAsync(query, cancellationToken).ConfigureAwait(false);
            }

            // Get video streams for item.
            else if (int.TryParse(query.FolderId, out var movieId))
            {
                result = await GetMovieStreamsAsync(movieId, cancellationToken).ConfigureAwait(false);
            }

            if (result != null)
            {
                _memoryCache.Set(query.FolderId, result);
            }

            return result ?? new ChannelItemResult();
        }

        /// <inheritdoc />
        public Task<DynamicImageResponse> GetChannelImage(ImageType type, CancellationToken cancellationToken)
        {
            _logger.LogDebug(nameof(GetChannelImage));
            if (type == ImageType.Thumb)
            {
                var name = GetType().Namespace + ".Images.tmdb-thumb.png";
                var response = new DynamicImageResponse
                {
                    Format = ImageFormat.Png,
                    HasImage = true,
                    Stream = GetType().Assembly.GetManifestResourceStream(name)
                };

                return Task.FromResult(response);
            }

            return Task.FromResult<DynamicImageResponse>(null);
        }

        /// <inheritdoc />
        public IEnumerable<ImageType> GetSupportedChannelImages()
        {
            yield return ImageType.Thumb;
        }

        /// <summary>
        /// Dispose.
        /// </summary>
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
                _client?.Dispose();
            }
        }

        /// <summary>
        /// Calculate page size from start index.
        /// </summary>
        /// <param name="startIndex">Start index.</param>
        /// <returns>The page number.</returns>
        private static int GetPageNumber(int? startIndex)
        {
            var start = startIndex ?? 0;

            return (int)Math.Floor(start / (double)PageSize);
        }

        /// <summary>
        /// Gets the original image url.
        /// </summary>
        /// <param name="imagePath">The image resource path.</param>
        /// <returns>The full image path.</returns>
        private static string GetImageUrl(string imagePath)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "https://image.tmdb.org/t/p/original/{0}",
                imagePath.TrimStart('/'));
        }

        /// <summary>
        /// Get types of trailers.
        /// </summary>
        /// <returns><see cref="ChannelItemResult"/> containing the types of trailers.</returns>
        private ChannelItemResult GetChannelTypes()
        {
            _logger.LogDebug("Get Channel Types");
            return new ChannelItemResult
            {
                Items = new List<ChannelItemInfo>
                {
                    new ChannelItemInfo
                    {
                        Id = "upcoming",
                        FolderType = ChannelFolderType.Container,
                        Name = "Upcoming",
                        Type = ChannelItemType.Folder,
                        MediaType = ChannelMediaType.Video
                    },
                    new ChannelItemInfo
                    {
                        Id = "nowplaying",
                        FolderType = ChannelFolderType.Container,
                        Name = "Now Playing",
                        Type = ChannelItemType.Folder,
                        MediaType = ChannelMediaType.Video
                    },
                    new ChannelItemInfo
                    {
                        Id = "popular",
                        FolderType = ChannelFolderType.Container,
                        Name = "Popular",
                        Type = ChannelItemType.Folder,
                        MediaType = ChannelMediaType.Video
                    },
                    new ChannelItemInfo
                    {
                        Id = "toprated",
                        FolderType = ChannelFolderType.Container,
                        Name = "Top Rated",
                        Type = ChannelItemType.Folder,
                        MediaType = ChannelMediaType.Video
                    }
                },
                TotalRecordCount = 4
            };
        }

        /// <summary>
        /// Get playback url from site and key.
        /// </summary>
        /// <param name="site">Site to play from.</param>
        /// <param name="key">Video key.</param>
        /// <returns>Video playback url.</returns>
        private async Task<(string Url, int Bitrate)?> GetPlaybackUrlAsync(string site, string key)
        {
            if (site.Equals("youtube", StringComparison.OrdinalIgnoreCase))
            {
                var streamingData = await _youTubeService.GetStreamingDataAsync(key).ConfigureAwait(false);

                // Invalid video.
                if (streamingData == null)
                {
                    return null;
                }

                var maxBitrate = _configuration.MaxBitrate ?? int.MaxValue;
                var format = streamingData.Value.Formats
                    .Where(o => maxBitrate > o.Bitrate)
                    .OrderByDescending(o => o.Bitrate)
                    .FirstOrDefault();

                var streamUrl = await _youTubeService.GetStreamUrlAsync(key, format).ConfigureAwait(false);
                _logger.LogDebug("{function} Site={site} Key={key} Bitrate={bitrate} StreamUrl={url}", nameof(GetPlaybackUrlAsync), site, key, format.Bitrate, streamUrl);
                return (streamUrl, format.Bitrate);
            }

            if (site.Equals("vimeo", StringComparison.OrdinalIgnoreCase))
            {
                // TODO
                return null;
            }

            return null;
        }

        /// <summary>
        /// Create channel item result from search result.
        /// </summary>
        /// <param name="movies">Search container of movies.</param>
        /// <returns>The channel item result.</returns>
        private ChannelItemResult GetChannelItemResult(SearchContainer<SearchMovie> movies)
        {
            var channelItems = new List<ChannelItemInfo>();
            foreach (var item in movies.Results)
            {
                var posterUrl = GetImageUrl(item.PosterPath);
                _memoryCache.Set($"{item.Id}-poster", posterUrl, TimeSpan.FromDays(1));
                channelItems.Add(new ChannelItemInfo
                {
                    Id = item.Id.ToString(CultureInfo.InvariantCulture),
                    Name = item.Title,
                    FolderType = ChannelFolderType.Container,
                    Type = ChannelItemType.Folder,
                    MediaType = ChannelMediaType.Video,
                    ImageUrl = posterUrl
                });
            }

            return new ChannelItemResult
            {
                Items = channelItems,
                TotalRecordCount = movies.TotalResults
            };
        }

        /// <summary>
        /// Get upcoming movies.
        /// </summary>
        /// <param name="query">Channel query.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The upcoming movies.</returns>
        private async Task<ChannelItemResult> GetUpcomingMoviesAsync(InternalChannelItemQuery query, CancellationToken cancellationToken)
        {
            _logger.LogDebug(nameof(GetUpcomingMoviesAsync));
            var pageNumber = GetPageNumber(query.StartIndex);
            var response = await _client.GetMovieUpcomingListAsync(
                    _configuration.Language,
                    pageNumber,
                    _configuration.Region,
                    cancellationToken)
                .ConfigureAwait(false);

            return GetChannelItemResult(response);
        }

        /// <summary>
        /// Get now playing movies.
        /// </summary>
        /// <param name="query">Channel query.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The now playing movies.</returns>
        private async Task<ChannelItemResult> GetNowPlayingMoviesAsync(InternalChannelItemQuery query, CancellationToken cancellationToken)
        {
            _logger.LogDebug(nameof(GetNowPlayingMoviesAsync));
            var pageNumber = GetPageNumber(query.StartIndex);
            var response = await _client.GetMovieNowPlayingListAsync(
                    _configuration.Language,
                    pageNumber,
                    _configuration.Region,
                    cancellationToken)
                .ConfigureAwait(false);

            return GetChannelItemResult(response);
        }

        /// <summary>
        /// Get popular movies.
        /// </summary>
        /// <param name="query">Channel query.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The popular movies.</returns>
        private async Task<ChannelItemResult> GetPopularMoviesAsync(InternalChannelItemQuery query, CancellationToken cancellationToken)
        {
            _logger.LogDebug(nameof(GetPopularMoviesAsync));
            var pageNumber = GetPageNumber(query.StartIndex);
            var response = await _client.GetMoviePopularListAsync(
                    _configuration.Language,
                    pageNumber,
                    _configuration.Region,
                    cancellationToken)
                .ConfigureAwait(false);

            return GetChannelItemResult(response);
        }

        /// <summary>
        /// Get top rated movies.
        /// </summary>
        /// <param name="query">Channel query.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The top rated movies.</returns>
        private async Task<ChannelItemResult> GetTopRatedMoviesAsync(InternalChannelItemQuery query, CancellationToken cancellationToken)
        {
            _logger.LogDebug(nameof(GetTopRatedMoviesAsync));
            var pageNumber = GetPageNumber(query.StartIndex);
            var response = await _client.GetMovieTopRatedListAsync(
                    _configuration.Language,
                    pageNumber,
                    _configuration.Region,
                    cancellationToken)
                .ConfigureAwait(false);

            return GetChannelItemResult(response);
        }

        /// <summary>
        /// Get available movie streams.
        /// </summary>
        /// <param name="id">Movie id.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The movie streams.</returns>
        private async Task<ChannelItemResult> GetMovieStreamsAsync(int id, CancellationToken cancellationToken)
        {
            _memoryCache.TryGetValue($"{id}-poster", out string posterUrl);
            var response = await _client.GetMovieVideosAsync(id, cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("{function} Response={@response}", nameof(GetMovieStreamsAsync), response);

            var streamTasks = new List<Task<ChannelItemInfo>>(response.Results.Count);
            streamTasks.AddRange(response.Results.Select(GetChannelItemInfoAsync));

            await Task.WhenAll(streamTasks).ConfigureAwait(false);
            var channelItems = new List<ChannelItemInfo>(response.Results.Count);
            foreach (var task in streamTasks)
            {
                var channelItemInfo = await task.ConfigureAwait(false);
                if (channelItemInfo == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(posterUrl))
                {
                    channelItemInfo.ImageUrl = posterUrl;
                }

                channelItems.Add(channelItemInfo);
            }

            return new ChannelItemResult
            {
                Items = channelItems,
                TotalRecordCount = response.Results.Count
            };
        }

        /// <summary>
        /// Get stream information from video item.
        /// </summary>
        /// <param name="item">Video item.</param>
        /// <returns>Stream information.</returns>
        private async Task<ChannelItemInfo> GetChannelItemInfoAsync(Video item)
        {
            var response = await GetPlaybackUrlAsync(item.Site, item.Key).ConfigureAwait(false);
            if (response == null)
            {
                return null;
            }

            return new ChannelItemInfo
            {
                Id = item.Id,
                Name = item.Name,
                OriginalTitle = item.Name,
                Type = ChannelItemType.Media,
                MediaType = ChannelMediaType.Video,
                MediaSources = new List<MediaSourceInfo>
                {
                    new MediaSourceInfo
                    {
                        Name = item.Name,
                        Path = response.Value.Url,
                        Bitrate = response.Value.Bitrate,
                        Protocol = MediaProtocol.Http,
                        Id = item.Id,
                        IsRemote = true
                    }
                }
            };
        }
    }
}