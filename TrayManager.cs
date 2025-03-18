using System;
using System.Windows;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;
using System.Windows.Controls;
using System.IO;
using System.Runtime.InteropServices;

namespace WallpaperTool
{
    public class TrayManager : IDisposable
    {
        private TaskbarIcon? _notifyIcon;
        private readonly MainWindow _mainWindow;
        private TrayDropWindow? _dropWindow;
        private DetectionZoneWindow? _detectionZoneWindow;
        private DispatcherTimer _monitorTimer;
        private Point _lastCursorPos;

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private const int VK_LBUTTON = 0x01;

        private DateTime _lastClickTime;
        private Point _lastClickPos;
        private bool _isClickHandling;
        private const double DoubleClickTimeThreshold = 500; // 双击时间阈值（毫秒）
        private const double ClickThreshold = 5; // 点击判定阈值

        private bool _isMonitoringActive;
        private Rect _monitoringArea;
        private const double MONITOR_INTERVAL_INACTIVE = 500; // 非活动状态检查间隔(ms)
        private const double MONITOR_INTERVAL_ACTIVE = 8;    // 活动状态检查间隔(ms)，提高采样率
        private const double DETECTION_ZONE_HEIGHT = 10;     // 检测区高度
        private const double DETECTION_ZONE_OFFSET = 5;     // 检测区与托盘区的距离
        private bool _isDraggingFromOutside = false;         // 标记是否从外部拖拽
        private Point _lastDetectionZoneEntry;               // 记录进入检测区的位置
        private bool _wasInDetectionZone;                    // 记录上一次是否在检测区内

        public TrayManager(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            InitializeNotifyIcon();
            InitializeDropWindow();
            InitializeMonitor();
            UpdateDropWindowPosition(); // 启动时更新透明窗口位置
        }

        private void InitializeNotifyIcon()
        {
            _notifyIcon = new TaskbarIcon
            {
                Icon = System.Drawing.SystemIcons.Application,
                ToolTipText = "壁纸设置工具 - 拖放文件到这里",
                Visibility = Visibility.Visible
            };

            // 创建上下文菜单
            var contextMenu = new ContextMenu();

            var showItem = new MenuItem { Header = "显示主窗口" };
            showItem.Click += (s, e) => _mainWindow.Show();

            var hideItem = new MenuItem { Header = "隐藏主窗口" };
            hideItem.Click += (s, e) => _mainWindow.Hide();

            var exitItem = new MenuItem { Header = "退出" };
            exitItem.Click += (s, e) => Application.Current.Shutdown();

            contextMenu.Items.Add(showItem);
            contextMenu.Items.Add(hideItem);
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(exitItem);

            _notifyIcon.ContextMenu = contextMenu;

            // 显示气泡提示
            _notifyIcon.ShowBalloonTip("壁纸设置工具", "已启动，可以拖放文件到图标处", BalloonIcon.Info);
        }

        private void InitializeDropWindow()
        {
            _dropWindow = new TrayDropWindow();
            _dropWindow.Hide(); // 确保窗口在启动时不可见
            _dropWindow.FileDropped += OnFileDropped;
            
            // 初始化检测区窗口
            _detectionZoneWindow = new DetectionZoneWindow();
            _detectionZoneWindow.Hide(); // 初始时隐藏
            // 添加拖拽事件处理
            _detectionZoneWindow.DragEnter += DetectionZoneWindow_DragEnter;
            _detectionZoneWindow.DragLeave += DetectionZoneWindow_DragLeave;
            _detectionZoneWindow.FileDropped += OnFileDropped; // 连接文件拖放事件处理器
        }

        private void InitializeMonitor()
        {
            // 初始化监控区域，只在启动时设置一次
            UpdateMonitorArea();
            _mainWindow.AddLog("托盘监控已创建");
        }

        private void UpdateMonitorArea()
        {
            var trayRect = GetTrayIconRect();
            // 创建一个位于托盘区上方的检测区，确保不覆盖托盘区
            _monitoringArea = new Rect(
                trayRect.X - 50,  // 向左扩展50像素
                trayRect.Y - DETECTION_ZONE_OFFSET - DETECTION_ZONE_HEIGHT,  // 在托盘区上方创建检测区，保持一定距离
                trayRect.Width + 100,  // 向右扩展100像素
                DETECTION_ZONE_HEIGHT  // 检测区高度
            );
            
            // 更新检测区窗口位置和大小
            _detectionZoneWindow.Left = _monitoringArea.X;
            _detectionZoneWindow.Top = _monitoringArea.Y;
            _detectionZoneWindow.Width = _monitoringArea.Width;
            _detectionZoneWindow.Height = _monitoringArea.Height;
            
            // 确保窗口显示但不获取焦点
            if (!_detectionZoneWindow.IsVisible)
            {
                _detectionZoneWindow.Show();
                _mainWindow.AddLog($"显示检测区窗口: X={_monitoringArea.X}, Y={_monitoringArea.Y}, W={_monitoringArea.Width}, H={_monitoringArea.Height}");
            }
        }

        // 新增拖拽事件处理方法
        private void DetectionZoneWindow_DragEnter(object sender, DragEventArgs e)
        {
            _mainWindow.AddLog("检测到拖拽进入检测区");
            
            // 检查是否拖拽的是文件
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // 允许拖放操作
                e.Effects = DragDropEffects.Copy;
                _isDraggingFromOutside = true;
                
                // 更新并显示拖放窗口
                UpdateDropWindowPosition(); // 拖放前更新一次位置
                var trayRect = GetTrayIconRect();
                ShowDropWindow(trayRect);
                
                _mainWindow.AddLog("启用拖放模式，显示透明窗口");
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            
            e.Handled = true;
        }
        
        private void DetectionZoneWindow_DragLeave(object sender, DragEventArgs e)
        {
            // 拖拽离开检测区但不取消拖放窗口，直到文件接收完成
            _mainWindow.AddLog("拖拽离开检测区");
            e.Handled = true;
        }

        private bool HandleTrayIconClick(Point currentPos, bool isLeftButtonDown)
        {
            var trayRect = GetTrayIconRect();
            bool isInTrayArea = IsPointNearRect(currentPos, trayRect);

            if (isLeftButtonDown)
            {
                if (!_isClickHandling)
                {
                    _isClickHandling = true;
                    _lastClickPos = currentPos;
                    _lastClickTime = DateTime.Now;
                    return true;
                }
                else
                {
                    var distance = Math.Sqrt(
                        Math.Pow(currentPos.X - _lastClickPos.X, 2) +
                        Math.Pow(currentPos.Y - _lastClickPos.Y, 2));

                    if (isInTrayArea)
                    {
                        // 在托盘区域内移动，不显示窗口
                        return true;
                    }
                    else
                    {
                        // 在托盘区域外移动，允许显示窗口
                        _isClickHandling = false;
                        return false;
                    }
                }
            }
            else
            {
                if (_isClickHandling)
                {
                    var distance = Math.Sqrt(
                        Math.Pow(currentPos.X - _lastClickPos.X, 2) +
                        Math.Pow(currentPos.Y - _lastClickPos.Y, 2));

                    if (distance <= ClickThreshold && isInTrayArea)
                    {
                        var timeSinceLastClick = (DateTime.Now - _lastClickTime).TotalMilliseconds;
                        if (timeSinceLastClick <= DoubleClickTimeThreshold)
                        {
                            OnDoubleClick();
                        }
                        else
                        {
                            OnSingleClick();
                        }
                    }
                    _isClickHandling = false;
                }
            }

            return false;
        }

        private void OnSingleClick()
        {
            _mainWindow.AddLog("托盘图标单击");
            // 添加单击处理逻辑
        }

        private void OnDoubleClick()
        {
            _mainWindow.AddLog("托盘图标双击");
            _mainWindow.Show(); // 显示主窗口
        }

        private bool IsPointNearRect(Point point, Rect rect)
        {
            // 扩大检测区域
            var expandedRect = new Rect(
                rect.X - 20,
                rect.Y - 20,
                rect.Width + 40,
                rect.Height + 40
            );

            return expandedRect.Contains(point);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct APPBARDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public int uCallbackMessage;
            public int uEdge;
            public RECT rc;
            public IntPtr lParam;
        }

        [DllImport("shell32.dll")]
        private static extern IntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        private Rect GetTrayIconRect()
        {
            try
            {
                // 找到任务栏
                IntPtr taskBar = FindWindow("Shell_TrayWnd", null);
                if (taskBar != IntPtr.Zero)
                {
                    // 找到通知区域
                    IntPtr trayNotify = FindWindowEx(taskBar, IntPtr.Zero, "TrayNotifyWnd", null);
                    if (trayNotify != IntPtr.Zero)
                    {
                        // 获取通知区域位置
                        RECT trayRect;
                        if (GetWindowRect(trayNotify, out trayRect))
                        {
                            // 获取屏幕工作区
                            var workArea = SystemParameters.WorkArea;

                            // 计算托盘图标的预期位置
                            double iconX = trayRect.Left;
                            double iconY = trayRect.Top;
                            double iconWidth = trayRect.Right - trayRect.Left;
                            double iconHeight = trayRect.Bottom - trayRect.Top;

                            _mainWindow.AddLog($"通知区域位置: Left={trayRect.Left}, Top={trayRect.Top}, Right={trayRect.Right}, Bottom={trayRect.Bottom}");

                            return new Rect(iconX, iconY, iconWidth, iconHeight);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _mainWindow.AddLog($"获取托盘位置错误: {ex.Message}");
            }

            // 如果无法获取准确位置，使用默认位置
            var screen = SystemParameters.WorkArea;
            return new Rect(
                screen.Right - 200,
                screen.Bottom - 40,
                32,
                32
            );
        }

        private void ShowDropWindow(Rect trayRect)
        {
            if (_dropWindow != null && !_dropWindow.IsVisible)
            {
                _dropWindow.Left = trayRect.X;
                _dropWindow.Top = trayRect.Y;
                _dropWindow.Width = trayRect.Width;
                _dropWindow.Height = trayRect.Height;

                _mainWindow.AddLog($"显示拖放窗口: X={_dropWindow.Left}, Y={_dropWindow.Top}, W={_dropWindow.Width}, H={_dropWindow.Height}");

                _dropWindow.Show();
                _dropWindow.Activate();
            }
        }

        public void UpdateDropWindowPosition()
        {
            UpdateMonitorArea(); // 更新监控区域
            var trayRect = GetTrayIconRect();
            // 仅更新位置，不显示窗口
            _dropWindow.Left = trayRect.X;
            _dropWindow.Top = trayRect.Y;
            _dropWindow.Width = trayRect.Width;
            _dropWindow.Height = trayRect.Height;
        }

        private void OnFileDropped(string[] files)
        {
            foreach (string file in files)
            {
                string extension = Path.GetExtension(file).ToLowerInvariant();
                _mainWindow.AddLog($"收到文件: {file}");
                _mainWindow.AddLog($"文件格式: {extension}");
                _mainWindow.AddLog("------------------------");

                // 如果窗口是隐藏的，显示窗口
                if (!_mainWindow.IsVisible)
                {
                    _mainWindow.Show();
                }
            }

            // 显示通知
            _notifyIcon?.ShowBalloonTip(
                "收到文件",
                $"成功接收 {files.Length} 个文件",
                BalloonIcon.Info);
                
            // 文件接收完成后，隐藏拖放窗口并重置拖拽状态
            _dropWindow?.Hide();
            _isDraggingFromOutside = false;
            _mainWindow.AddLog("文件接收完成，隐藏拖放窗口");
        }

        public void Dispose()
        {
            _notifyIcon?.Dispose();
            _dropWindow?.Close();
            _detectionZoneWindow?.Close();
        }
    }

    // 检测区可视化窗口
    public class DetectionZoneWindow : Window
    {
        private readonly TextBlock _debugInfo;
        
        public DetectionZoneWindow()
        {
            // 设置窗口样式
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Topmost = true;
            AllowsTransparency = true;
            Background = System.Windows.Media.Brushes.Transparent;
            
            // 创建内容
            var grid = new Grid();
            
            // 添加可视化边框
            var border = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(80, 255, 0, 0)), // 更透明的红色
                BorderBrush = System.Windows.Media.Brushes.Red,
                BorderThickness = new Thickness(1)
            };
            grid.Children.Add(border);
            
            // 添加调试信息区
            _debugInfo = new TextBlock
            {
                Foreground = System.Windows.Media.Brushes.White,
                FontWeight = FontWeights.Bold,
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(100, 0, 0, 0)), // 半透明黑色背景
                Padding = new Thickness(5),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Text = "拖动文件到此区域"
            };
            grid.Children.Add(_debugInfo);
            
            Content = grid;
            
            // 确保窗口不获取焦点但可以接收拖放
            Focusable = false;
            
            // 允许窗口接收拖放操作
            AllowDrop = true;
            
            // 注册事件处理器以允许处理拖放事件
            DragEnter += OnDragEnter;
            DragLeave += OnDragLeave;
            DragOver += OnDragOver;
            Drop += OnDrop;
            
            // 添加调试用MouseMove事件
            MouseMove += OnMouseMove;
            MouseEnter += OnMouseEnter;
            MouseLeave += OnMouseLeave;
            
            // 添加窗口激活/停用事件跟踪
            Activated += (s, e) => UpdateDebugInfo("窗口激活");
            Deactivated += (s, e) => UpdateDebugInfo("窗口停用");
            
            // 记录窗口创建信息
            UpdateDebugInfo("窗口已创建");
        }
        
        private void UpdateDebugInfo(string message)
        {
            _debugInfo.Text = $"{message}\n{DateTime.Now.ToString("HH:mm:ss.fff")}";
            Console.WriteLine($"DetectionZoneWindow: {message}");
        }
        
        private void OnMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            UpdateDebugInfo("鼠标进入窗口");
        }
        
        private void OnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            UpdateDebugInfo("鼠标离开窗口");
        }
        
        private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            // 检测鼠标是否在检测区内移动，用于调试
            var pos = e.GetPosition(this);
            if (pos.X > 0 && pos.Y > 0 && pos.X < this.Width && pos.Y < this.Height)
            {
                // 更新坐标信息，但不要过于频繁
                if (DateTime.Now.Millisecond % 100 == 0)
                {
                    UpdateDebugInfo($"鼠标位置: X={pos.X:F0}, Y={pos.Y:F0}");
                }
            }
        }
        
        private void OnDragEnter(object sender, DragEventArgs e)
        {
            UpdateDebugInfo("DragEnter 被触发");
            
            // 确保事件能传递到 TrayManager 中注册的处理程序
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                UpdateDebugInfo("拖拽的是文件");
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                UpdateDebugInfo("拖拽的不是文件");
                e.Effects = DragDropEffects.None;
            }
        }
        
        private void OnDragLeave(object sender, DragEventArgs e)
        {
            UpdateDebugInfo("DragLeave 被触发");
        }
        
        private void OnDragOver(object sender, DragEventArgs e)
        {
            // 显示拖拽坐标
            Point pos = e.GetPosition(this);
            UpdateDebugInfo($"DragOver: X={pos.X:F0}, Y={pos.Y:F0}");
            
            // 保持拖放效果有效
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }
        
        private void OnDrop(object sender, DragEventArgs e)
        {
            UpdateDebugInfo("Drop 被触发");
            
            // 转发文件到下方的拖放窗口
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // 获取拖放的文件
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                UpdateDebugInfo($"收到 {files.Length} 个文件");
                
                // 触发自定义事件
                FileDropped?.Invoke(files);
            }
            e.Handled = true;
        }
        
        // 声明自定义事件
        public event Action<string[]> FileDropped;
        
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            
            // 使用WS_EX_LAYERED来使窗口半透明但可以接收拖放
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            var extendedStyle = Win32.GetWindowLong(hwnd, Win32.GWL_EXSTYLE);
            
            // 设置窗口风格 - WS_EX_LAYERED允许透明度，但仍然允许拖放
            Win32.SetWindowLong(hwnd, Win32.GWL_EXSTYLE, extendedStyle | Win32.WS_EX_LAYERED);
            
            // 设置窗口透明度 - 128是半透明
            Win32.SetLayeredWindowAttributes(hwnd, 0, 180, Win32.LWA_ALPHA);
            
            UpdateDebugInfo("窗口样式设置完成");
        }
    }
    
    // Win32 API 辅助类
    public static class Win32
    {
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TRANSPARENT = 0x00000020;
        public const int WS_EX_LAYERED = 0x00080000;
        public const int LWA_ALPHA = 0x00000002;
        
        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hwnd, int index);
        
        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);
        
        [DllImport("user32.dll")]
        public static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);
    }
}