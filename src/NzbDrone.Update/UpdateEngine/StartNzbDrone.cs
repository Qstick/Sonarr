using System;
using System.IO;
using NLog;
using NzbDrone.Common;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Processes;
using IServiceProvider = NzbDrone.Common.IServiceProvider;

namespace NzbDrone.Update.UpdateEngine
{
    public interface IStartNzbDrone
    {
        void Start(AppType appType, string installationFolder);
    }

    public class StartNzbDrone : IStartNzbDrone
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IProcessProvider _processProvider;
        private readonly IStartupContext _startupContext;
        private readonly IDiskProvider _diskProvider;
        private readonly Logger _logger;

        public StartNzbDrone(IServiceProvider serviceProvider, IProcessProvider processProvider, IStartupContext startupContext, IDiskProvider diskProvider, Logger logger)
        {
            _serviceProvider = serviceProvider;
            _processProvider = processProvider;
            _startupContext = startupContext;
            _diskProvider = diskProvider;
            _logger = logger;
        }

        public void Start(AppType appType, string installationFolder)
        {
            _logger.Info("Starting Sonarr");
            if (appType == AppType.Service)
            {
                try
                {
                    StartService();
                }
                catch (InvalidOperationException e)
                {
                    _logger.Warn(e, "Couldn't start Sonarr Service (Most likely due to permission issues). falling back to console.");
                    StartConsole(installationFolder);
                }
            }
            else if (appType == AppType.Console)
            {
                StartConsole(installationFolder);
            }
            else
            {
                StartWinform(installationFolder);
            }
        }

        private void StartService()
        {
            _logger.Info("Starting Sonarr service");
            _serviceProvider.Start(ServiceProvider.SERVICE_NAME);
        }

        private void StartWinform(string installationFolder)
        {
            Start(installationFolder, "Sonarr.exe");
        }

        private void StartConsole(string installationFolder)
        {
            Start(installationFolder, "Sonarr.Console.exe");
        }

        private void Start(string installationFolder, string fileName)
        {
            _logger.Info("Starting {0}", fileName);
            var path = Path.Combine(installationFolder, fileName);

            if (!_startupContext.Flags.Contains(StartupContext.NO_BROWSER))
            {
                _startupContext.Flags.Add(StartupContext.NO_BROWSER);
            }

            if (OsInfo.IsOsx)
            {
                if (installationFolder.EndsWith(".app/Contents/MacOS/bin"))
                {
                    // New MacOS App stores Sonarr binaries in MacOS/bin and has a shim in MacOS
                    // Run the app bundle instead
                    path = Path.GetDirectoryName(installationFolder);
                    path = Path.GetDirectoryName(path);
                    path = Path.GetDirectoryName(path);
                }
                else if (installationFolder.EndsWith(".app/Contents/MacOS"))
                {
                    // Old MacOS App stores Sonarr binaries in MacOS together with shell script
                    // Run the app bundle instead
                    path = Path.GetDirectoryName(installationFolder);
                    path = Path.GetDirectoryName(path);
                }
            }

            _processProvider.SpawnNewProcess(path, _startupContext.PreservedArguments);
        }
    }
}
