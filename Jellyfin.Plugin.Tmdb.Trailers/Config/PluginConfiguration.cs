using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Tmdb.Trailers.Config
{
    /// <inheritdoc />
    public class PluginConfiguration : BasePluginConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
        /// </summary>
        public PluginConfiguration()
        {
            ApiKey = "4219e299c89411838049ab0dab19ebd5";
            Language = "en-US";
            EnableTrailersChannel = true;
            EnableTrailersUpcoming = true;
            EnableTrailersNowPlaying = true;
            TrailerLimit = 20;
        }

        /// <summary>
        /// Gets or sets the api key.
        /// Used to authenticate with tmdb.
        /// Using Jellyfin ApiKey.
        /// </summary>
        public string ApiKey { get; set; }

        /// <summary>
        /// Gets or sets the language.
        /// Pass a ISO 639-1 value to display translated data for the fields that support it.
        /// </summary>
        public string Language { get; set; }

        /// <summary>
        /// Gets or sets region.
        /// Specify a ISO 3166-1 code to filter release dates. Must be uppercase.
        /// </summary>
        public string Region { get; set; }

        /// <summary>
        /// Gets or sets the max bitrate.
        /// </summary>
        public int? MaxBitrate { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to enable the extras channel.
        /// </summary>
        public bool EnableExtrasChannel { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to enable the trailers channel.
        /// </summary>
        public bool EnableTrailersChannel { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to enable the upcoming trailers.
        /// </summary>
        public bool EnableTrailersUpcoming { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to enable the now playing trailers.
        /// </summary>
        public bool EnableTrailersNowPlaying { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to enable the popular trailers.
        /// </summary>
        public bool EnableTrailersPopular { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to enable the top rated trailers.
        /// </summary>
        public bool EnableTrailersTopRated { get; set; }

        /// <summary>
        /// Gets or sets the trailer limit per category.
        /// </summary>
        public int TrailerLimit { get; set; }
    }
}