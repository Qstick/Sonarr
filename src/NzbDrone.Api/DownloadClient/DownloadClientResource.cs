using NzbDrone.Core.Indexers;

namespace NzbDrone.Api.DownloadClient
{
    public class DownloadClientResource : ProviderResource
    {
        public bool Enable { get; set; }
        public DownloadProtocol Protocol { get; set; }
        public int Priority { get; set; }
        public bool RemoveCompletedDownloads { get; set; }
        public bool RemoveFailedDownloads { get; set; }
    }
}