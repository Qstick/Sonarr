using NzbDrone.Common.Http;

namespace NzbDrone.Common.Cloud
{
    public interface ISonarrCloudRequestBuilder
    {
        IHttpRequestBuilderFactory Services { get; }
        IHttpRequestBuilderFactory UpdateServices { get; }
        IHttpRequestBuilderFactory SkyHookTvdb { get; }
    }

    public class SonarrCloudRequestBuilder : ISonarrCloudRequestBuilder
    {
        public SonarrCloudRequestBuilder()
        {
            UpdateServices = new HttpRequestBuilder("https://sonarr.servarr.com/v1/")
                .CreateFactory();

            Services = new HttpRequestBuilder("https://services.sonarr.tv/v1/")
                .CreateFactory();

            SkyHookTvdb = new HttpRequestBuilder("https://skyhook.sonarr.tv/v1/tvdb/{route}/{language}/")
                .SetSegment("language", "en")
                .CreateFactory();
        }

        public IHttpRequestBuilderFactory Services { get; }
        public IHttpRequestBuilderFactory UpdateServices { get; }

        public IHttpRequestBuilderFactory SkyHookTvdb { get; }
    }
}
