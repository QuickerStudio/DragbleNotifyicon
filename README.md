# WallpaperTool

* 这是一个用于管理和设置壁纸的工具，通知图标文件拖拽管理器。

# 本程序由Github Copilot大家族 赞助

## 功能特性

- 实时获取通知图标的位置
- 管理和设置壁纸
- 提供友好的用户界面

## 安装

1. 克隆仓库到本地：
   ```bash
   git clone https://github.com/QuickerStudio/WallpaperTool.git
   ```
2. 打开项目文件并构建解决方案。

## 使用

1. 在项目中引入必要的库和工具。
2. 调用`TrayHelper`类中的方法获取通知区域的位置。
* 我们尝试了多种查找通知图标的算法，只有这个运行最稳定，项目仍然缺少一种追踪通知图标位置的有效算法，如果又更好的算法可以提交代码。

### 示例代码

```csharp
using System;
using System.Windows;

namespace YourNamespace
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // 获取通知区域的位置
                var notifyRect = TrayHelper.GetNotifyAreaRect();
                // 可以将信息显示在TextBlock上，也可以输出日志
                this.LogTextBlock.Text = $"通知区域的位置：\nX={notifyRect.X}\nY={notifyRect.Y}\n宽度={notifyRect.Width}\n高度={notifyRect.Height}";
            }
            catch (Exception ex)
            {
                this.LogTextBlock.Text = $"获取通知区域位置失败：{ex.Message}";
            }
        }
    }
}
```

### TrayHelper 类

```csharp
using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace YourNamespace
{
    public static class TrayHelper
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        public static Rect GetNotifyAreaRect()
        {
            IntPtr taskBarWnd = FindWindow("Shell_TrayWnd", null);
            if (taskBarWnd == IntPtr.Zero)
            {
                throw new Exception("找不到任务栏窗口");
            }

            IntPtr notifyWnd = FindWindowEx(taskBarWnd, IntPtr.Zero, "TrayNotifyWnd", null);
            if (notifyWnd == IntPtr.Zero)
            {
                throw new Exception("找不到通知区域窗口");
            }

            if (GetWindowRect(notifyWnd, out RECT rect))
            {
                double width = rect.Right - rect.Left;
                double height = rect.Bottom - rect.Top;
                return new Rect(rect.Left, rect.Top, width, height);
            }
            else
            {
                throw new Exception("获取通知区域窗口坐标失败");
            }
        }
    }
}
```

## 贡献

欢迎提交pull request来贡献代码。

## 许可证

该项目使用MIT许可证。

---

## 技术支持

- 算法支持：Claude 3.5
- 代码质量检测：GPT-4o  GPT-o3 mini
- 测试员：QuickerStudio
