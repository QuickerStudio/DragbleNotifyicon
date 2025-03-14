using System;
using System.Windows.Forms;

namespace WallpaperTool
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            // 设置DPI感知
            SetProcessDPIAware();
            
            // 创建并运行应用程序
            using (var app = new WallpaperApp())
            {
                Application.Run();
            }
        }
        
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();
    }
}