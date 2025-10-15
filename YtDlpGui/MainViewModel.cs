using Microsoft.VisualBasic;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;

// MVVM基础类 - 用于属性变更通知
public class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

// MVVM基础类 - 用于绑定命令
public class RelayCommand : ICommand
{
    private readonly Action<object> _execute;
    private readonly Func<object, bool> _canExecute;

    public event EventHandler CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }

    public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object parameter) => _canExecute == null || _canExecute(parameter);
    public void Execute(object parameter) => _execute(parameter);
}


namespace YtDlpGui
{
    public class MainViewModel : ViewModelBase
    {
        // --- 私有字段 ---
        private string _videoUrl;
        private string _selectedFormatArgs;
        private string _logOutput;
        private double _downloadProgress;
        private bool _isDownloading = false;
        private string _lastUsedSavePath;

        // --- 构造函数 ---
        public MainViewModel()
        {
            // 直接初始化下载选项
            DownloadOptions = new ObservableCollection<DownloadOption>
            {
                 // 这是需要ffmpeg的版本，功能更全
                new DownloadOption { Description = "最佳质量 (合并音视频)", Arguments = "-f bestvideo+bestaudio/best" },
                new DownloadOption { Description = "最佳MP4", Arguments = "-f bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]/best" },
                new DownloadOption { Description = "最佳MP3", Arguments = "-x --audio-format mp3" }
                // 这是不需要ffmpeg的版本，可能只能下720p
                // new DownloadOption { Description = "最佳质量 (不合并)", Arguments = "-f best" }, 
            };
            SelectedFormatArgs = DownloadOptions.FirstOrDefault()?.Arguments;

            // 初始化命令
            DownloadCommand = new RelayCommand(async (o) => await ExecuteDownloadAsync(), (o) => CanExecuteDownload());
            OpenFolderCommand = new RelayCommand(ExecuteOpenFolder, (o) => CanExecuteOpenFolder());
            BrowseFolderCommand = new RelayCommand(ExecuteBrowseFolder);
        }

        // --- 绑定到UI的公共属性 ---
        public string VideoUrl { get => _videoUrl; set { _videoUrl = value; OnPropertyChanged(); } }
        public string SelectedFormatArgs { get => _selectedFormatArgs; set { _selectedFormatArgs = value; OnPropertyChanged(); } }
        public string LogOutput { get => _logOutput; set { _logOutput = value; OnPropertyChanged(); } }
        public double DownloadProgress { get => _downloadProgress; set { _downloadProgress = value; OnPropertyChanged(); } }
        public bool IsNotDownloading => !_isDownloading;
        public string SavePath { get; set; } = Directory.GetCurrentDirectory();
        public ObservableCollection<DownloadOption> DownloadOptions { get; }

        // --- 命令 ---
        public ICommand DownloadCommand { get; }
        public ICommand OpenFolderCommand { get; }
        public ICommand BrowseFolderCommand { get; }

        private void ExecuteBrowseFolder(object obj)
        {
            var dialog = new SaveFileDialog()
            {
                Title = "选择保存文件夹",
                Filter = "文件夹|*.this-is-a-folder",
                FileName = "选择此文件夹"
            };

            if (dialog.ShowDialog() == true)
            {
                SavePath = Path.GetDirectoryName(dialog.FileName);
                LogOutput += $"下载路径已设置为: {SavePath}\n";
            }
        }

        private bool CanExecuteDownload() => !string.IsNullOrWhiteSpace(VideoUrl) && !_isDownloading;
        private void ExecuteOpenFolder(object obj)
        {
            if (!string.IsNullOrEmpty(_lastUsedSavePath) && Directory.Exists(_lastUsedSavePath))
            {
                Process.Start(new ProcessStartInfo(_lastUsedSavePath) { UseShellExecute = true });
            }
        }
        private bool CanExecuteOpenFolder() => !string.IsNullOrEmpty(_lastUsedSavePath) && Directory.Exists(_lastUsedSavePath);

        private async Task ExecuteDownloadAsync()
        {
            _lastUsedSavePath = SavePath;
            _isDownloading = true;
            OnPropertyChanged(nameof(IsNotDownloading));
            CommandManager.InvalidateRequerySuggested(); // <--- 已修正

            LogOutput = "开始准备下载...\n";
            DownloadProgress = 0;

            string batchFilePath = null;
            string urlFilePath = null;

            try
            {
                // 如果你确实要移除ffmpeg，请把下面两行相关的代码注释掉
                var ffmpegExePath = ResourceManager.ExtractEmbeddedResource("YtDlpGui.Tools.ffmpeg.exe", "ffmpeg.exe");
                var ffmpegPath = Path.GetDirectoryName(ffmpegExePath);

                var ytDlpPath = ResourceManager.ExtractEmbeddedResource("YtDlpGui.Tools.yt-dlp.exe", "yt-dlp.exe");

                urlFilePath = Path.Combine(Path.GetTempPath(), $"ytdlp_url_{Guid.NewGuid()}.txt");
                await File.WriteAllTextAsync(urlFilePath, VideoUrl, new UTF8Encoding(false));

                var ytDlpCommand = new StringBuilder();
                string outputTemplate = Path.Combine(SavePath, "%%(title)s [%%(id)s].%%(ext)s");

                ytDlpCommand.Append($"\"{ytDlpPath}\"");
                ytDlpCommand.Append($" --batch-file \"{urlFilePath}\"");
                //ytDlpCommand.Append($" --ffmpeg-location \"{ffmpegPath}\""); // 如果移除ffmpeg，请注释此行
                ytDlpCommand.Append($" -o \"{outputTemplate}\"");
                ytDlpCommand.Append($" {SelectedFormatArgs}");
                ytDlpCommand.Append(" --progress");
                ytDlpCommand.Append(" --force-overwrites");

                var batchContent = new StringBuilder();
                batchContent.AppendLine("@echo off");
                batchContent.AppendLine("chcp 65001 > nul"); // 修正笔误: 65001, not 6501
                batchContent.AppendLine(ytDlpCommand.ToString());

                batchFilePath = Path.Combine(Path.GetTempPath(), $"ytdlp_runner_{Guid.NewGuid()}.bat");
                // 已修正文件写入方法的调用
                await File.WriteAllTextAsync(batchFilePath, batchContent.ToString(), new UTF8Encoding(true));

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{batchFilePath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using (var process = new Process { StartInfo = processStartInfo })
                {
                    process.OutputDataReceived += (sender, args) => { if (args.Data != null) { App.Current.Dispatcher.Invoke(() => { LogOutput += args.Data + "\n"; ParseProgress(args.Data); }); } };
                    process.ErrorDataReceived += (sender, args) => { if (!string.IsNullOrWhiteSpace(args.Data)) { App.Current.Dispatcher.Invoke(() => LogOutput += $"错误: {args.Data}\n"); } };
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    await process.WaitForExitAsync();
                    App.Current.Dispatcher.Invoke(() => { LogOutput += "\n--- 下载完成! ---\n"; if (DownloadProgress < 100) DownloadProgress = 100; });
                }
            }
            catch (Exception ex)
            {
                LogOutput += $"\n!!! 发生严重错误: {ex.Message} !!!\n";
            }
            finally
            {
                if (urlFilePath != null && File.Exists(urlFilePath)) { try { File.Delete(urlFilePath); } catch { } }
                if (batchFilePath != null && File.Exists(batchFilePath)) { try { File.Delete(batchFilePath); } catch { } }

                _isDownloading = false;
                OnPropertyChanged(nameof(IsNotDownloading));
                CommandManager.InvalidateRequerySuggested(); // <--- 已修正
            }
        }

        private void ParseProgress(string output)
        {
            var regex = new Regex(@"\[download\]\s+([0-9\.]+)%");
            var match = regex.Match(output);
            if (match.Success)
            {
                if (double.TryParse(match.Groups[1].Value, out double progress))
                {
                    DownloadProgress = progress;
                }
            }
        }
    }
}