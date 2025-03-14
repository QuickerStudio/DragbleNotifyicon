using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace WallpaperTool
{
    public class WallpaperApp : IDisposable
    {
        private NotifyIcon _notifyIcon;
        private TrayDropManager _dropManager;
        
        public WallpaperApp()
        {
            // 初始化NotifyIcon
            _notifyIcon = new NotifyIcon
            {
                Visible = true,
                Icon = SystemIcons.Application, // 替换为自己的图标
                Text = "壁纸应用 - 拖放文件到此处设置壁纸"
            };
            
            // 设置上下文菜单
            SetupContextMenu();
            
            // 初始化拖放管理器
            _dropManager = new TrayDropManager(_notifyIcon);
            _dropManager.FileDropped += OnFileDropped;
            
            // 显示启动提示
            _notifyIcon.ShowBalloonTip(
                3000, 
                "壁纸应用已启动", 
                "拖放图片或视频文件到此图标可设置壁纸", 
                ToolTipIcon.Info);
        }
        
        private void SetupContextMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            
            // 添加菜单项
            menu.Items.Add("设置", null, (s, e) => ShowSettings());
            menu.Items.Add("关于", null, (s, e) => ShowAbout());
            menu.Items.Add("-");
            menu.Items.Add("退出", null, (s, e) => Application.Exit());
            
            _notifyIcon.ContextMenuStrip = menu;
        }
        
        private void OnFileDropped(string[] files)
        {
            if (files == null || files.Length == 0)
                return;
                
            // 处理第一个拖放的文件
            string filePath = files[0];
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            
            try
            {
                if (IsImageFile(extension))
                {
                    // 设置图片壁纸
                    SetStaticWallpaper(filePath);
                    ShowNotification("已设置壁纸", $"图片：{Path.GetFileName(filePath)}");
                }
                else if (IsVideoFile(extension))
                {
                    // 设置视频壁纸
                    SetVideoWallpaper(filePath);
                    ShowNotification("已设置视频壁纸", $"视频：{Path.GetFileName(filePath)}");
                }
                else
                {
                    ShowNotification("不支持的文件类型", "请使用图片或视频文件");
                }
            }
            catch (Exception ex)
            {
                ShowNotification("设置壁纸失败", ex.Message, ToolTipIcon.Error);
            }
        }
        
        private void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
        {
            _notifyIcon.ShowBalloonTip(2000, title, message, icon);
        }
        
        private bool IsImageFile(string extension)
        {
            return extension == ".jpg" || extension == ".jpeg" || 
                   extension == ".png" || extension == ".bmp" || 
                   extension == ".gif";
        }
        
        private bool IsVideoFile(string extension)
        {
            return extension == ".mp4" || extension == ".webm" || 
                   extension == ".mov" || extension == ".avi" ||
                   extension == ".mkv";
        }
        
        private void SetStaticWallpaper(string filePath)
        {
            // 实现静态壁纸设置逻辑
            Console.WriteLine($"设置静态壁纸: {filePath}");
            
            // TODO: 调用您的壁纸设置API
        }
        
        private void SetVideoWallpaper(string filePath)
        {
            // 实现视频壁纸设置逻辑
            Console.WriteLine($"设置视频壁纸: {filePath}");
            
            // TODO: 调用您的视频壁纸设置API
        }
        
        private void ShowSettings()
        {
            // 显示设置界面
            MessageBox.Show("显示设置界面", "壁纸设置");
        }
        
        private void ShowAbout()
        {
            // 显示关于信息
            MessageBox.Show("壁纸应用 v1.0\n支持拖放设置壁纸功能", "关于");
        }
        
        public void Dispose()
        {
            _dropManager?.Dispose();
            _notifyIcon?.Dispose();
        }
    }
}