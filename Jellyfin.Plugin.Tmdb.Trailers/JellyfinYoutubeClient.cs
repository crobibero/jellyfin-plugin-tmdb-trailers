using System.Net.Http;
using MediaBrowser.Common.Net;
using VideoLibrary;

namespace Jellyfin.Plugin.Tmdb.Trailers;

/// <summary>
/// Jellyfin specific YouTube client.
/// </summary>
public class JellyfinYouTubeClient : YouTube
{
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="JellyfinYouTubeClient"/> class.
    /// </summary>
    /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
    public JellyfinYouTubeClient(
        IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc />
    protected override HttpClient MakeClient(HttpMessageHandler handler)
    {
        return _httpClientFactory.CreateClient(NamedClient.Default);
    }
}
