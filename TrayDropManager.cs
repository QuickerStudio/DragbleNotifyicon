using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WallpaperTool
{
    /// <summary>
    /// 托盘图标文件拖放管理器
    /// </summary>
    public class TrayDropManager : IDisposable
    {
        // 事件：当文件被拖放到托盘图标时触发
        public delegate void FileDroppedEventHandler(string[] files);
        public event FileDroppedEventHandler FileDropped;
        
        private NotifyIcon _notifyIcon;
        private TrayRegionMonitor _regionMonitor;
        private DropTargetWindow _dropWindow;
        
        public TrayDropManager(NotifyIcon notifyIcon)
        {
            _notifyIcon = notifyIcon ?? throw new ArgumentNullException(nameof(notifyIcon));
            
            // 创建通知区域监视器
            _regionMonitor = new TrayRegionMonitor();
            
            // 创建透明的拖放窗口
            _dropWindow = new DropTargetWindow();
            _dropWindow.FileDropped += (files) => FileDropped?.Invoke(files);
            
            // 开始监控
            _regionMonitor.DragOperationDetected += OnDragOperationDetected;
            _regionMonitor.StartMonitoring();
        }
        
        private void OnDragOperationDetected(Rectangle trayRegion, Point cursorPosition)
        {
            // 仅在鼠标靠近通知区域时显示拖放窗口
            _dropWindow.SetBounds(trayRegion.X, trayRegion.Y, trayRegion.Width, trayRegion.Height);
            
            if (!_dropWindow.Visible)
            {
                _dropWindow.Show();
                _dropWindow.Activate();
            }
        }
        
        public void Dispose()
        {
            _regionMonitor?.Dispose();
            _dropWindow?.Dispose();
        }
    }
}