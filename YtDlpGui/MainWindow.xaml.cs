using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace YtDlpGui
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        // 当日志文本框内容改变时，自动滚动到底部
        private void LogTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            LogTextBox.ScrollToEnd();
        }
        private void Run_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://github.com/noob-xiaoyu/yt-dlp-gui",
                UseShellExecute = true
            });
        }
    }
}