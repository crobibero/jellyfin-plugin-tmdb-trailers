#pragma warning disable CA1819

using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Tmdb.Trailers.Configuration
{
    /// <inheritdoc />
    public class PluginConfiguration : BasePluginConfiguration
    {
        /// <summary>
        /// Gets or sets the api key.
        /// Used to authenticate with tmdb.
        /// Using Jellyfin ApiKey.
        /// </summary>
        public string ApiKey { get; set; } = "4219e299c89411838049ab0dab19ebd5";

        /// <summary>
        /// Gets or sets the language.
        /// Pass a ISO 639-1 value to display translated data for the fields that support it.
        /// </summary>
        public string Language { get; set; } = "en-US";

        /// <summary>
        /// Gets or sets region.
        /// Specify a ISO 3166-1 code to filter release dates. Must be uppercase.
        /// </summary>
        public string Region { get; set; }
    }
}