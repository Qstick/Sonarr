using System;
using System.Linq;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Configuration.Events;
using NzbDrone.Core.Localization;

namespace NzbDrone.Core.HealthCheck.Checks
{
    [CheckOn(typeof(ConfigSavedEvent))]
    public class ReleaseBranchCheck : HealthCheckBase
    {
        private readonly IConfigFileProvider _configFileService;

        public ReleaseBranchCheck(IConfigFileProvider configFileService)
            : base()
        {
            _configFileService = configFileService;
        }

        public override HealthCheck Check()
        {
            return new HealthCheck(GetType(), HealthCheckResult.Warning, "You are using an experimental build which is now merged to Sonarr 'develop', once available please switch as this build will no longer receive updates", "#widowmaker");
        }

        public enum ReleaseBranches
        {
            Nightly
        }
    }
}
