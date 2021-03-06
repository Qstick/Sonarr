using Sonarr.Http;

namespace Sonarr.Api.V3
{
    public abstract class SonarrV3Module : SonarrModule
    {
        protected SonarrV3Module(string resource)
            : base("/api/v3/" + resource.Trim('/'))
        {
        }
    }
}
