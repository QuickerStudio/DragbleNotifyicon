using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace WallpaperTool
{
    /// <summary>
    /// 监视任务栏通知区域和拖放操作
    /// </summary>
    internal class TrayRegionMonitor : IDisposable
    {
        // 事件：当检测到拖放操作时触发
        public event Action<Rectangle, Point> DragOperationDetected;
        
        private Timer _monitorTimer;
        private Rectangle _cachedTrayRegion = Rectangle.Empty;
        private DateTime _lastRegionUpdateTime = DateTime.MinValue;
        
        #region Windows API

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);
        
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
        
        private const int VK_LBUTTON = 0x01;
        
        #endregion
        
        public TrayRegionMonitor()
        {
            _monitorTimer = new Timer();
            _monitorTimer.Interval = 100; // 100ms检查一次
        }
        
        public void StartMonitoring()
        {
            _monitorTimer.Tick += MonitorTimer_Tick;
            _monitorTimer.Start();
        }
        
        public void StopMonitoring()
        {
            _monitorTimer.Stop();
            _monitorTimer.Tick -= MonitorTimer_Tick;
        }
        
        private void MonitorTimer_Tick(object sender, EventArgs e)
        {
            // 检查鼠标左键是否按下（可能是拖放操作）
            if ((GetKeyState(VK_LBUTTON) & 0x8000) != 0)
            {
                // 获取当前鼠标位置
                Point cursorPos = Cursor.Position;
                
                // 获取通知区域矩形 (每5秒更新一次,避免频繁调用)
                if (_cachedTrayRegion.IsEmpty || (DateTime.Now - _lastRegionUpdateTime).TotalSeconds > 5)
                {
                    _cachedTrayRegion = GetAppNotifyIconRegion();
                    _lastRegionUpdateTime = DateTime.Now;
                }
                
                // 检查鼠标是否在通知区域附近
                if (!_cachedTrayRegion.IsEmpty && IsPointNearRect(cursorPos, _cachedTrayRegion, 20))
                {
                    // 触发事件
                    DragOperationDetected?.Invoke(_cachedTrayRegion, cursorPos);
                }
            }
        }
        
        // 检查点是否靠近矩形
        private bool IsPointNearRect(Point p, Rectangle r, int threshold)
        {
            Rectangle expandedRect = new Rectangle(
                r.X - threshold,
                r.Y - threshold,
                r.Width + threshold * 2,
                r.Height + threshold * 2
            );
            
            return expandedRect.Contains(p);
        }
        
        // 获取应用程序通知图标区域
        private Rectangle GetAppNotifyIconRegion()
        {
            // 找到任务栏
            IntPtr taskbarHandle = FindWindow("Shell_TrayWnd", null);
            if (taskbarHandle == IntPtr.Zero)
                return Rectangle.Empty;

            // 找到通知区域
            IntPtr trayNotifyHandle = FindWindowEx(taskbarHandle, IntPtr.Zero, "TrayNotifyWnd", null);
            if (trayNotifyHandle == IntPtr.Zero)
                return Rectangle.Empty;

            // 找到系统托盘图标容器
            IntPtr sysPagerHandle = FindWindowEx(trayNotifyHandle, IntPtr.Zero, "SysPager", null);
            if (sysPagerHandle == IntPtr.Zero)
                return Rectangle.Empty;

            // 找到应用程序通知图标容器
            IntPtr toolbarHandle = FindWindowEx(sysPagerHandle, IntPtr.Zero, "ToolbarWindow32", null);
            if (toolbarHandle == IntPtr.Zero)
            {
                // 如果找不到应用通知区,预留任务栏右侧区域
                RECT trayRect;
                if (GetWindowRect(trayNotifyHandle, out trayRect))
                {
                    int trayWidth = trayRect.Right - trayRect.Left;
                    int trayHeight = trayRect.Bottom - trayRect.Top;
                    
                    // 预留足够15个图标的空间
                    int iconWidth = trayHeight - 4; // 图标大小略小于任务栏高度
                    int reservedWidth = iconWidth * 15;
                    
                    return new Rectangle(
                        trayRect.Right - reservedWidth - 150, // 减去系统图标和时钟区域
                        trayRect.Top,
                        reservedWidth,
                        trayHeight
                    );
                }
                
                return Rectangle.Empty;
            }

            // 获取应用通知图标区域矩形
            RECT appIconRect;
            if (GetWindowRect(toolbarHandle, out appIconRect))
            {
                Rectangle region = new Rectangle(
                    appIconRect.Left,
                    appIconRect.Top,
                    appIconRect.Right - appIconRect.Left,
                    appIconRect.Bottom - appIconRect.Top
                );
                
                // 如果区域太窄(可能是折叠状态),使用估计的宽度
                if (region.Width < 50)
                {
                    // 获取任务栏矩形
                    RECT taskbarRect;
                    if (GetWindowRect(taskbarHandle, out taskbarRect))
                    {
                        int taskbarHeight = taskbarRect.Bottom - taskbarRect.Top;
                        int iconSize = taskbarHeight - 4;
                        int estimatedWidth = iconSize * 15;
                        
                        return new Rectangle(
                            taskbarRect.Right - estimatedWidth - 150, // 减去系统图标和时钟区域
                            region.Y,
                            estimatedWidth,
                            region.Height
                        );
                    }
                }
                
                return region;
            }
            
            return Rectangle.Empty;
        }
        
        public void Dispose()
        {
            StopMonitoring();
            _monitorTimer?.Dispose();
        }
    }
}