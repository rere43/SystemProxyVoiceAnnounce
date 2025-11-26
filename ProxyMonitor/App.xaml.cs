using ProxyMonitor.Services;
using System;
using System.Diagnostics;
using System.Windows;
using System.Linq;

namespace ProxyMonitor
{
    public partial class App : Application
    {
        private MonitorService? _monitorService;
        private ConfigService? _configService;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Single Instance Logic: Kill other instances
            Process current = Process.GetCurrentProcess();
            foreach (Process process in Process.GetProcessesByName(current.ProcessName))
            {
                if (process.Id != current.Id)
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                        // Ignore if permission denied or already exiting
                    }
                }
            }

            _configService = new ConfigService();
            _monitorService = new MonitorService(_configService);

            bool isSilent = false;
            foreach (string arg in e.Args)
            {
                if (arg.Contains("--silent"))
                {
                    isSilent = true;
                    break;
                }
            }

            // Always start monitoring
            _monitorService.Start();

            if (!isSilent)
            {
                MainWindow mainWindow = new MainWindow(_configService, _monitorService);
                mainWindow.Show();
            }
        }
    }
}
