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
    /// The TMDb manager.
    /// </summary>
    public class TmdbManager : IDisposable
    {
        /// <summary>
        /// Gets the page size.
        /// TMDb always returns 20 items.
        /// </summary>
        public const int PageSize = 20;

        private readonly ILogger<TmdbManager> _logger;
        private readonly IMemoryCache _memoryCache;

        private readonly TMDbClient _client;
        private readonly PluginConfiguration _configuration;
        private readonly IYouTubeService _youTubeService;

        /// <summary>
        /// Initializes a new instance of the <see cref="TmdbManager"/> class.
        /// </summary>
        /// <param name="loggerFactory">Instance of the <see cref="ILoggerFactory"/> interface.</param>
        /// <param name="memoryCache">Instance of the <see cref="IMemoryCache"/> interface.</param>
        public TmdbManager(ILoggerFactory loggerFactory, IMemoryCache memoryCache)
        {
            _logger = loggerFactory.CreateLogger<TmdbManager>();
            _memoryCache = memoryCache;

            _configuration = TmdbTrailerPlugin.Instance.Configuration;
            _client = new TMDbClient(_configuration.ApiKey);
            _youTubeService = new YouTubeServiceFactory().Create();
        }

        /// <summary>
        /// Get channel items.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The channel item result.</returns>
        public async Task<ChannelItemResult> GetChannelItems(InternalChannelItemQuery query, CancellationToken cancellationToken)
        {
            try
            {
                ChannelItemResult result = null;
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

                // Get upcoming movies.
                if (query.FolderId.Equals("upcoming", StringComparison.OrdinalIgnoreCase))
                {
                    var movies = await GetUpcomingMoviesAsync(query, cancellationToken).ConfigureAwait(false);
                    result = GetChannelItemResult(movies, TrailerType.ComingSoonToTheaters);
                }

                // Get now playing movies.
                else if (query.FolderId.Equals("nowplaying", StringComparison.OrdinalIgnoreCase))
                {
                    var movies = await GetNowPlayingMoviesAsync(query, cancellationToken).ConfigureAwait(false);
                    result = GetChannelItemResult(movies, TrailerType.ComingSoonToTheaters);
                }

                // Get popular movies.
                else if (query.FolderId.Equals("popular", StringComparison.OrdinalIgnoreCase))
                {
                    var movies = await GetPopularMoviesAsync(query, cancellationToken).ConfigureAwait(false);
                    result = GetChannelItemResult(movies, TrailerType.Archive);
                }

                // Get top rated movies.
                else if (query.FolderId.Equals("toprated", StringComparison.OrdinalIgnoreCase))
                {
                    var movies = await GetTopRatedMoviesAsync(query, cancellationToken).ConfigureAwait(false);
                    result = GetChannelItemResult(movies, TrailerType.Archive);
                }

                // Get video streams for item.
                else if (int.TryParse(query.FolderId, out var movieId))
                {
                    var videos = await GetMovieStreamsAsync(movieId, cancellationToken).ConfigureAwait(false);
                    result = await GetVideoItem(videos, false).ConfigureAwait(false);
                }

                if (result != null)
                {
                    _memoryCache.Set(query.FolderId, result, TimeSpan.FromDays(1));
                }

                return result ?? new ChannelItemResult();
            }
            catch (Exception e)
            {
                _logger.LogError(e, nameof(GetChannelItems));
                throw;
            }
        }

        /// <summary>
        /// Get All Channel Items.
        /// </summary>
        /// <param name="ignoreCache">Ignore cache.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The channel item result.</returns>
        public async Task<ChannelItemResult> GetAllChannelItems(bool ignoreCache, CancellationToken cancellationToken)
        {
            try
            {
                if (!ignoreCache && _memoryCache.TryGetValue("all-trailer", out ChannelItemResult cachedValue))
                {
                    _logger.LogDebug("Function={function} Cache Hit", nameof(GetAllChannelItems));
                    return cachedValue;
                }

                var query = new InternalChannelItemQuery();

                var channelItemsResult = new ChannelItemResult();
                var movieTasks = new List<Task<ResultContainer<Video>>>();
                var channelTasks = new List<Task<ChannelItemResult>>();

                if (TmdbTrailerPlugin.Instance.Configuration.EnableTrailersUpcoming)
                {
                    var upcomingMovies = await GetUpcomingMoviesAsync(query, cancellationToken).ConfigureAwait(false);
                    movieTasks.AddRange(upcomingMovies.Select(movie => GetMovieStreamsAsync(movie.Id, cancellationToken)));
                }

                if (TmdbTrailerPlugin.Instance.Configuration.EnableTrailersNowPlaying)
                {
                    var nowPlayingMovies = await GetNowPlayingMoviesAsync(query, cancellationToken).ConfigureAwait(false);
                    movieTasks.AddRange(nowPlayingMovies.Select(movie => GetMovieStreamsAsync(movie.Id, cancellationToken)));
                }

                if (TmdbTrailerPlugin.Instance.Configuration.EnableTrailersPopular)
                {
                    var popularMovies = await GetPopularMoviesAsync(query, cancellationToken).ConfigureAwait(false);
                    movieTasks.AddRange(popularMovies.Select(movie => GetMovieStreamsAsync(movie.Id, cancellationToken)));
                }

                if (TmdbTrailerPlugin.Instance.Configuration.EnableTrailersTopRated)
                {
                    var topRatedMovies = await GetTopRatedMoviesAsync(query, cancellationToken).ConfigureAwait(false);
                    movieTasks.AddRange(topRatedMovies.Select(movie => GetMovieStreamsAsync(movie.Id, cancellationToken)));
                }

                await Task.WhenAll(movieTasks).ConfigureAwait(false);
                foreach (var task in movieTasks)
                {
                    var result = await task.ConfigureAwait(false);
                    channelTasks.Add(GetVideoItem(result, true));
                }

                await Task.WhenAll(channelTasks).ConfigureAwait(false);
                foreach (var task in channelTasks)
                {
                    var videoItems = await task.ConfigureAwait(false);
                    channelItemsResult.Items.AddRange(videoItems.Items);
                }

                _memoryCache.Set("all-trailer", channelItemsResult);
                return channelItemsResult;
            }
            catch (Exception e)
            {
                _logger.LogError(e, nameof(GetAllChannelItems));
                throw;
            }
        }

        /// <summary>
        /// Get channel image.
        /// </summary>
        /// <param name="type">Image type.</param>
        /// <returns>The image response.</returns>
        public Task<DynamicImageResponse> GetChannelImage(ImageType type)
        {
            try
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
            catch (Exception e)
            {
                _logger.LogError(e, nameof(GetChannelImage));
                throw;
            }
        }

        /// <summary>
        /// Get supported channel images.
        /// </summary>
        /// <returns>The supported channel images.</returns>
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
        private string GetImageUrl(string imagePath)
        {
            try
            {
                if (string.IsNullOrEmpty(imagePath))
                {
                    return null;
                }

                return string.Format(
                    CultureInfo.InvariantCulture,
                    "https://image.tmdb.org/t/p/original/{0}",
                    imagePath.TrimStart('/'));
            }
            catch (Exception e)
            {
                _logger.LogError(e, nameof(GetImageUrl));
                throw;
            }
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
            try
            {
                if (site.Equals("youtube", StringComparison.OrdinalIgnoreCase))
                {
                    var streamingData = await _youTubeService.GetStreamingDataAsync(key).ConfigureAwait(false);

                    // Invalid video.
                    if (streamingData?.Formats == null)
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
                    _logger.LogDebug("{function} Site={site} Key={key} is not implemented.", nameof(GetPlaybackUrlAsync), site, key);
                }

                return null;
            }
            catch (Exception e)
            {
                _logger.LogError(e, nameof(GetPlaybackUrlAsync));
                throw;
            }
        }

        /// <summary>
        /// Create channel item result from search result.
        /// </summary>
        /// <param name="movies">Search container of movies.</param>
        /// <param name="trailerType">The trailer type.</param>
        /// <returns>The channel item result.</returns>
        private ChannelItemResult GetChannelItemResult(IEnumerable<SearchMovie> movies, TrailerType trailerType)
        {
            try
            {
                var channelItems = new List<ChannelItemInfo>();
                foreach (var item in movies)
                {
                    var posterUrl = GetImageUrl(item.PosterPath);
                    _memoryCache.Set($"{item.Id}-poster", posterUrl, TimeSpan.FromDays(1));
                    _memoryCache.Set($"{item.Id}-trailer", trailerType, TimeSpan.FromDays(1));
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
                    Items = channelItems
                };
            }
            catch (Exception e)
            {
                _logger.LogError(e, nameof(GetChannelItemResult));
                throw;
            }
        }

        /// <summary>
        /// Get upcoming movies.
        /// </summary>
        /// <param name="query">Channel query.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The upcoming movies.</returns>
        private async Task<List<SearchMovie>> GetUpcomingMoviesAsync(InternalChannelItemQuery query, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogDebug(nameof(GetUpcomingMoviesAsync));
                var pageNumber = GetPageNumber(query.StartIndex);
                var itemLimit = TmdbTrailerPlugin.Instance.Configuration.TrailerLimit;
                var movies = new List<SearchMovie>();
                bool hasMore;

                do
                {
                    var results = await _client.GetMovieUpcomingListAsync(
                            _configuration.Language,
                            pageNumber,
                            _configuration.Region,
                            cancellationToken)
                        .ConfigureAwait(false);

                    pageNumber++;
                    movies.AddRange(results.Results);
                    hasMore = results.Results.Count != 0;
                }
                while (hasMore && movies.Count < itemLimit);

                return movies.Take(itemLimit).ToList();
            }
            catch (Exception e)
            {
                _logger.LogError(e, nameof(GetUpcomingMoviesAsync));
                throw;
            }
        }

        /// <summary>
        /// Get now playing movies.
        /// </summary>
        /// <param name="query">Channel query.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The now playing movies.</returns>
        private async Task<List<SearchMovie>> GetNowPlayingMoviesAsync(InternalChannelItemQuery query, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogDebug(nameof(GetNowPlayingMoviesAsync));
                var pageNumber = GetPageNumber(query.StartIndex);
                var itemLimit = TmdbTrailerPlugin.Instance.Configuration.TrailerLimit;
                var movies = new List<SearchMovie>();
                bool hasMore;

                do
                {
                    var results = await _client.GetMovieNowPlayingListAsync(
                            _configuration.Language,
                            pageNumber,
                            _configuration.Region,
                            cancellationToken)
                        .ConfigureAwait(false);

                    pageNumber++;
                    movies.AddRange(results.Results);
                    hasMore = results.Results.Count != 0;
                }
                while (hasMore && movies.Count < itemLimit);

                return movies.Take(itemLimit).ToList();
            }
            catch (Exception e)
            {
                _logger.LogError(e, nameof(GetNowPlayingMoviesAsync));
                throw;
            }
        }

        /// <summary>
        /// Get popular movies.
        /// </summary>
        /// <param name="query">Channel query.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The popular movies.</returns>
        private async Task<List<SearchMovie>> GetPopularMoviesAsync(InternalChannelItemQuery query, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogDebug(nameof(GetPopularMoviesAsync));
                var pageNumber = GetPageNumber(query.StartIndex);
                var itemLimit = TmdbTrailerPlugin.Instance.Configuration.TrailerLimit;
                var movies = new List<SearchMovie>();
                bool hasMore;

                do
                {
                    var results = await _client.GetMoviePopularListAsync(
                            _configuration.Language,
                            pageNumber,
                            _configuration.Region,
                            cancellationToken)
                        .ConfigureAwait(false);

                    pageNumber++;
                    movies.AddRange(results.Results);
                    hasMore = results.Results.Count != 0;
                }
                while (hasMore && movies.Count < itemLimit);

                return movies.Take(itemLimit).ToList();
            }
            catch (Exception e)
            {
                _logger.LogError(e, nameof(GetPopularMoviesAsync));
                throw;
            }
        }

        /// <summary>
        /// Get top rated movies.
        /// </summary>
        /// <param name="query">Channel query.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The top rated movies.</returns>
        private async Task<List<SearchMovie>> GetTopRatedMoviesAsync(InternalChannelItemQuery query, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogDebug(nameof(GetTopRatedMoviesAsync));
                var pageNumber = GetPageNumber(query.StartIndex);
                var itemLimit = TmdbTrailerPlugin.Instance.Configuration.TrailerLimit;
                var movies = new List<SearchMovie>();
                bool hasMore;

                do
                {
                    var results = await _client.GetMovieTopRatedListAsync(
                            _configuration.Language,
                            pageNumber,
                            _configuration.Region,
                            cancellationToken)
                        .ConfigureAwait(false);

                    pageNumber++;
                    movies.AddRange(results.Results);
                    hasMore = results.Results.Count != 0;
                }
                while (hasMore && movies.Count < itemLimit);

                return movies.Take(itemLimit).ToList();
            }
            catch (Exception e)
            {
                _logger.LogError(e, nameof(GetTopRatedMoviesAsync));
                throw;
            }
        }

        /// <summary>
        /// Get available movie streams.
        /// </summary>
        /// <param name="id">Movie id.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The movie streams.</returns>
        private async Task<ResultContainer<Video>> GetMovieStreamsAsync(int id, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogDebug("{function} Id={id}", nameof(GetMovieStreamsAsync), id);
                var response = await _client.GetMovieVideosAsync(id, cancellationToken).ConfigureAwait(false);
                _logger.LogDebug("{function} Response={@response}", nameof(GetMovieStreamsAsync), response);
                return response;
            }
            catch (Exception e)
            {
                _logger.LogError(e, nameof(GetMovieStreamsAsync));
                throw;
            }
        }

        private async Task<ChannelItemResult> GetVideoItem(ResultContainer<Video> videoResult, bool trailerChannel)
        {
            try
            {
                _logger.LogDebug("{function} VideoResult={@videoResult}", nameof(GetVideoItem), videoResult);
                var streamTasks = new List<Task<ChannelItemInfo>>(videoResult.Results.Count);
                streamTasks.AddRange(videoResult.Results.Select(o => GetVideoChannelItem(videoResult.Id, o, trailerChannel)));

                await Task.WhenAll(streamTasks).ConfigureAwait(false);
                var channelItems = new List<ChannelItemInfo>(videoResult.Results.Count);
                foreach (var task in streamTasks)
                {
                    // Only add first trailer
                    if (trailerChannel && channelItems.Count != 0)
                    {
                        break;
                    }

                    var channelItemInfo = await task.ConfigureAwait(false);
                    if (channelItemInfo == null)
                    {
                        continue;
                    }

                    channelItems.Add(channelItemInfo);
                }

                return new ChannelItemResult
                {
                    Items = channelItems,
                    TotalRecordCount = channelItems.Count
                };
            }
            catch (Exception e)
            {
                _logger.LogError(e, nameof(GetVideoItem));
                throw;
            }
        }

        private async Task<ChannelItemInfo> GetVideoChannelItem(int id, Video video, bool trailerChannel)
        {
            try
            {
                // Returning only trailers
                if (trailerChannel && !string.Equals(video.Type, "trailer", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                _logger.LogDebug("{function} Id={id} Video={@video}", nameof(GetVideoChannelItem), id, video);
                _memoryCache.TryGetValue($"{id}-poster", out string posterUrl);
                _memoryCache.TryGetValue($"{id}-trailer", out TrailerType? trailerType);
                trailerType ??= TrailerType.Archive;

                var channelItemInfo = await GetChannelItemInfoAsync(video).ConfigureAwait(false);
                if (channelItemInfo == null)
                {
                    return null;
                }

                if (!string.IsNullOrEmpty(posterUrl))
                {
                    channelItemInfo.ImageUrl = posterUrl;
                }

                // only add additional properties if sourced from trailer channel.
                if (trailerChannel)
                {
                    channelItemInfo.ExtraType = ExtraType.Trailer;
                    channelItemInfo.TrailerTypes = new List<TrailerType>
                    {
                        trailerType.Value
                    };
                    channelItemInfo.ProviderIds = new Dictionary<string, string>
                    {
                        {
                            MetadataProvider.Tmdb.ToString(), id.ToString(CultureInfo.InvariantCulture)
                        }
                    };
                }

                return channelItemInfo;
            }
            catch (Exception e)
            {
                _logger.LogError(e, nameof(GetVideoChannelItem));
                throw;
            }
        }

        /// <summary>
        /// Get stream information from video item.
        /// </summary>
        /// <param name="item">Video item.</param>
        /// <returns>Stream information.</returns>
        private async Task<ChannelItemInfo> GetChannelItemInfoAsync(Video item)
        {
            try
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
            catch (Exception e)
            {
                _logger.LogError(e, nameof(GetChannelItemInfoAsync));
                throw;
            }
        }
    }
}