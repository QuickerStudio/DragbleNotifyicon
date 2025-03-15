using System.Windows;

namespace WallpaperTool
{
    public partial class App : Application
    {
        private TrayManager? _trayManager;
        private MainWindow? _mainWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 创建主窗口
            _mainWindow = new MainWindow();
            _mainWindow.Show();

            // 创建托盘管理器
            _trayManager = new TrayManager(_mainWindow);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _trayManager?.Dispose();
            base.OnExit(e);
        }
    }
}