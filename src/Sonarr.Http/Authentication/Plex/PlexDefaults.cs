using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sonarr.Http.Authentication.Plex
{
    public static class PlexDefaults
    {
        public const string AuthenticationScheme = "Plex";
        public static readonly string DisplayName = "Plex";
        public static readonly string AuthorizationEndpoint = "https://plex.tv/api/v2/pins";
        public static readonly string TokenEndpoint = "https://app.plex.tv/auth/#!";
        public static readonly string UserInformationEndpoint = "https://www.googleapis.com/oauth2/v2/userinfo";
    }
}
