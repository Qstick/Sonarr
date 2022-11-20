using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Sonarr.Http.Authentication
{
    public class BypassableDenyAnonymousAuthorizationRequirement : DenyAnonymousAuthorizationRequirement
    {
    }
}
