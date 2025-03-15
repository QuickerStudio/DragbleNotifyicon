using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace WallpaperTool
{
    public class TrayDropWindow : Window
    {
        public event Action<string[]>? FileDropped;

        public TrayDropWindow()
        {
            // 基本窗口设置
            this.AllowDrop = true;
            this.ShowInTaskbar = false;
            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Topmost = true;

            // 调试时使用半透明蓝色背景，便于观察位置
            this.Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 255));
            // 设置半透明背景以便于调试
          //  this.Background = new SolidColorBrush(Color.FromArgb(1, 255, 0, 0));
            // 添加边框便于观察
            this.BorderBrush = new SolidColorBrush(Colors.Red);
            this.BorderThickness = new Thickness(1);

            // 添加调试信息标签
            var debugLabel = new TextBlock
            {
                Text = "拖放区域",
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            this.Content = debugLabel;
            // 注册事件
            this.DragEnter += OnDragEnter;
            this.DragOver += OnDragOver;
            this.Drop += OnDrop;

            // 调试信息
            this.MouseEnter += (s, e) => Console.WriteLine("鼠标进入拖放窗口");
            this.MouseLeave += (s, e) => Console.WriteLine("鼠标离开拖放窗口");


        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                Console.WriteLine("文件拖入");
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
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
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                Console.WriteLine($"收到文件：{string.Join(", ", files)}");
                FileDropped?.Invoke(files);
            }
            this.Hide();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var hwnd = new WindowInteropHelper(this).Handle;
            var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_LAYERED);
        }

        // Win32 API 定义
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_LAYERED = 0x00080000;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);
    }
}