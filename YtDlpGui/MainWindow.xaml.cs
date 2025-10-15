using System.Windows;
using System.Windows.Controls;

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
    }
}