using System;
using System.ComponentModel;
using System.Windows.Forms;
using NLog;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Processes;
using NzbDrone.Host;

namespace NzbDrone.SysTray
{
    public interface ISystemTrayApp
    {
        void Start();
    }

    public class SystemTrayApp : Form, ISystemTrayApp
    {
        private readonly IBrowserService _browserService;
        private readonly IRuntimeInfo _runtimeInfo;
        private readonly IProcessProvider _processProvider;

        private readonly NotifyIcon _trayIcon = new NotifyIcon();
        private readonly ContextMenu _trayMenu = new ContextMenu();

        public SystemTrayApp(IBrowserService browserService, IRuntimeInfo runtimeInfo, IProcessProvider processProvider)
        {
            _browserService = browserService;
            _runtimeInfo = runtimeInfo;
            _processProvider = processProvider;
        }

        public void Start()
        {
            Application.ThreadException += OnThreadException;
            Application.ApplicationExit += OnApplicationExit;

            _trayMenu.MenuItems.Add("Launch Browser", LaunchBrowser);
            _trayMenu.MenuItems.Add("-");
            _trayMenu.MenuItems.Add("Exit", OnExit);

            _trayIcon.Text = string.Format("Sonarr - {0}", BuildInfo.Version);
            _trayIcon.Icon = Properties.Resources.NzbDroneIcon;

            _trayIcon.ContextMenu = _trayMenu;
            _trayIcon.Visible = true;
            _trayIcon.DoubleClick += LaunchBrowser;

            Application.Run(this);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            DisposeTrayIcon();
        }

        protected override void OnClosed(EventArgs e)
        {
            Console.WriteLine("Closing");
            base.OnClosed(e);
        }

        protected override void OnLoad(EventArgs e)
        {
            Visible = false;
            ShowInTaskbar = false;

            base.OnLoad(e);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                _trayIcon.Dispose();
            }

            if (InvokeRequired)
            {
                base.Invoke(new MethodInvoker(() => Dispose(isDisposing)));
            }

            else
            {
                base.Dispose(isDisposing);
            }
        }

        private void OnExit(object sender, EventArgs e)
        {
            LogManager.Configuration = null;
            Environment.Exit(0);
        }

        private void LaunchBrowser(object sender, EventArgs e)
        {
            try
            {
                _browserService.LaunchWebUI();
            }
            catch (Exception)
            {

            }
        }

        private void OnApplicationExit(object sender, EventArgs e)
        {
            if (_runtimeInfo.RestartPending)
            {
                _processProvider.SpawnNewProcess(_runtimeInfo.ExecutingApplication, "--restart --nobrowser");
            }

            DisposeTrayIcon();
        }

        private void OnThreadException(object sender, EventArgs e)
        {
            DisposeTrayIcon();
        }

        private void DisposeTrayIcon()
        {
            try
            {
                _trayIcon.Visible = false;
                _trayIcon.Icon = null;
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
            catch (Exception)
            {

            }
        }
    }
}
