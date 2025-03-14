using System;
using System.Runtime.InteropServices;

namespace WallpaperTool
{
    public static class DpiHelper
    {
        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hwnd);
        
        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();
        
        public static float GetScaleFactor()
        {
            try
            {
                uint dpi = GetDpiForWindow(GetDesktopWindow());
                return dpi > 0 ? dpi / 96.0f : 1.0f;
            }
            catch
            {
                return 1.0f; // 默认缩放
            }
        }
        
        public static int Scale(int value)
        {
            return (int)(value * GetScaleFactor());
        }
    }
}