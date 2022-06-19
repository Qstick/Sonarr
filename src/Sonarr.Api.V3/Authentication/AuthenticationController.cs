using System.Collections.Generic;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Notifications.Plex.PlexTv;
using Sonarr.Http;

namespace Sonarr.Api.V3.Authentication
{
    [V3ApiController]
    public class AuthenticationController : Controller
    {
        private readonly IPlexTvService _plex;

        public AuthenticationController(IPlexTvService plex)
        {
            _plex = plex;
        }

        [HttpGet("plex/resources")]
        public List<PlexTvResource> GetResources(string accessToken)
        {
            return _plex.GetResources(accessToken);
        }
    }
}
