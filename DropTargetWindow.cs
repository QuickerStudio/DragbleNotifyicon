using System;
using System.Drawing;
using System.Windows.Forms;

namespace WallpaperTool
{
    /// <summary>
    /// 透明的拖放目标窗口
    /// </summary>
    internal class DropTargetWindow : Form
    {
        public event Action<string[]> FileDropped;
        
        public DropTargetWindow()
        {
            InitializeWindow();
        }
        
        private void InitializeWindow()
        {
            // 基本窗口设置
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.AllowDrop = true;
            
            // 使窗口透明
            this.BackColor = Color.Fuchsia; // 指定透明色
            this.TransparencyKey = Color.Fuchsia;
            this.Opacity = 0.01; // 几乎完全透明
            
            // 注册拖放事件
            this.DragEnter += DropWindow_DragEnter;
            this.DragDrop += DropWindow_DragDrop;
            
            // 自动隐藏
            this.Deactivate += (s, e) => this.Hide();
            
            // 防止窗口闪烁
            this.DoubleBuffered = true;
        }
        
        private void DropWindow_DragEnter(object sender, DragEventArgs e)
        {
            // 仅接受文件拖放
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }
        
        private void DropWindow_DragDrop(object sender, DragEventArgs e)
        {
            // 处理拖放的文件
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                FileDropped?.Invoke(files);
                this.Hide(); // 完成后隐藏窗口
            }
        }
        
        // 以下属性确保窗口不会干扰其他窗口
        protected override bool ShowWithoutActivation => true;
        
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                return cp;
            }
        }
    }
}