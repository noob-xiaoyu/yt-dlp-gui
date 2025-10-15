using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Win32;

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

        // --- 绑定到UI的公共属性 ---

        public string VideoUrl
        {
            get => _videoUrl;
            set { _videoUrl = value; OnPropertyChanged(); }
        }

        public string SelectedFormatArgs
        {
            get => _selectedFormatArgs;
            set { _selectedFormatArgs = value; OnPropertyChanged(); }
        }

        public string LogOutput
        {
            get => _logOutput;
            set { _logOutput = value; OnPropertyChanged(); }
        }

        public double DownloadProgress
        {
            get => _downloadProgress;
            set { _downloadProgress = value; OnPropertyChanged(); }
        }

        public bool IsNotDownloading
        {
            get => !_isDownloading;
        }

        // --- ComboBox的数据源 ---
        public ObservableCollection<DownloadOption> DownloadOptions { get; }

        // --- 命令 ---
        public ICommand DownloadCommand { get; }
        public ICommand OpenFolderCommand { get; }
        public ICommand BrowseFolderCommand { get; }
        public string SavePath { get; set; } = Directory.GetCurrentDirectory();

        // --- 构造函数 ---
        public MainViewModel()
        {
            // 初始化下载选项
            DownloadOptions = new ObservableCollection<DownloadOption>
            {
                new DownloadOption { Description = "最佳视频 + 音频 (MP4)", Arguments = "-f bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]/best" },
                new DownloadOption { Description = "仅最佳音频 (提取为MP3)", Arguments = "-x --audio-format mp3" },
                new DownloadOption { Description = "最佳视频 (WebM)", Arguments = "-f bestvideo[ext=webm]+bestaudio[ext=webm]/best[ext=webm]/best" },
                new DownloadOption { Description = "1080p 视频 (MP4)", Arguments = "-f \"bestvideo[height<=1080][ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]/best\"" }
            };
            // 默认选择第一个
            SelectedFormatArgs = DownloadOptions[0].Arguments;

            // 初始化命令
            DownloadCommand = new RelayCommand(async (o) => await ExecuteDownloadAsync(), (o) => CanExecuteDownload());
            OpenFolderCommand = new RelayCommand(ExecuteOpenFolder, (o) => CanExecuteOpenFolder());
            BrowseFolderCommand = new RelayCommand(ExecuteBrowseFolder);
        }

        // ====== 逻辑修正 ======
        // 修正了这里不应该影响下载状态的逻辑
        private void ExecuteBrowseFolder(object obj)
        {
            var dialog = new SaveFileDialog()
            {
                Title = "选择保存文件夹",
                Filter = "文件夹 |*.*", // 这是一个让用户选择文件夹的技巧
                FileName = "选择此文件夹"
            };

            if (dialog.ShowDialog() == true)
            {
                SavePath = Path.GetDirectoryName(dialog.FileName);
                LogOutput += $"下载路径已设置为: {SavePath}\n";
            }
        }

        private bool CanExecuteDownload()
        {
            return !string.IsNullOrWhiteSpace(VideoUrl) && !_isDownloading;
        }

        // ====== 乱码终极修复 ======
        // 使用 cmd.exe 和 chcp 65001 命令来彻底解决中文乱码问题
        private async Task ExecuteDownloadAsync()
        {
            _lastUsedSavePath = SavePath;
            _isDownloading = true;
            OnPropertyChanged(nameof(IsNotDownloading));
            CommandManager.InvalidateRequerySuggested();

            LogOutput = "开始准备下载...\n";
            DownloadProgress = 0;

            string batchFilePath = null;
            string urlFilePath = null;

            try
            {
                // ================ 核心改造：从嵌入资源中提取可执行文件 ================
                LogOutput += "正在准备执行环境...\n";
                // 提取yt-dlp.exe，注意替换 "YtDlpGui" 为你的实际项目名
                var ytDlpPath = ResourceManager.ExtractEmbeddedResource("YtDlpGui.Tools.yt-dlp.exe", "yt-dlp.exe");

                // 提取ffmpeg.exe并获取其所在目录
                var ffmpegExePath = ResourceManager.ExtractEmbeddedResource("YtDlpGui.Tools.ffmpeg.exe", "ffmpeg.exe");
                var ffmpegPath = Path.GetDirectoryName(ffmpegExePath);
                LogOutput += "环境准备完成。\n";
                // =========================================================================

                urlFilePath = Path.Combine(Path.GetTempPath(), $"ytdlp_url_{Guid.NewGuid()}.txt");
                await File.WriteAllTextAsync(urlFilePath, VideoUrl, new UTF8Encoding(false));

                var ytDlpCommand = new StringBuilder();
                string outputTemplate = Path.Combine(SavePath, "%(title)s [%(id)s].%(ext)s");

                ytDlpCommand.Append($"\"{ytDlpPath}\"");
                ytDlpCommand.Append($" --batch-file \"{urlFilePath}\"");
                ytDlpCommand.Append($" --ffmpeg-location \"{ffmpegPath}\"");
                ytDlpCommand.Append($" -o \"{outputTemplate}\"");
                ytDlpCommand.Append($" {SelectedFormatArgs}");
                ytDlpCommand.Append(" --progress");
                ytDlpCommand.Append(" --force-overwrites");

                var batchContent = new StringBuilder();
                batchContent.AppendLine("@echo off");
                batchContent.AppendLine("chcp 65001 > nul");
                batchContent.AppendLine(ytDlpCommand.ToString());

                batchFilePath = Path.Combine(Path.GetTempPath(), $"ytdlp_runner_{Guid.NewGuid()}.bat");
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
                    // 日志处理部分保持不变
                    process.OutputDataReceived += (sender, args) => {
                        if (args.Data != null && !args.Data.Contains(batchFilePath.Replace(Path.GetTempPath(), "").TrimStart('\\')))
                        {
                            App.Current.Dispatcher.Invoke(() => { LogOutput += args.Data + "\n"; ParseProgress(args.Data); });
                        }
                    };

                    process.ErrorDataReceived += (sender, args) => {
                        if (!string.IsNullOrWhiteSpace(args.Data) &&
                            !args.Data.Contains("@echo off") &&
                            !args.Data.Contains("chcp 65001"))
                        {
                            App.Current.Dispatcher.Invoke(() => LogOutput += $"错误: {args.Data}\n");
                        }
                    };

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
                CommandManager.InvalidateRequerySuggested();
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

        private void ExecuteOpenFolder(object obj)
        {
            if (!string.IsNullOrEmpty(_lastUsedSavePath) && Directory.Exists(_lastUsedSavePath))
            {
                Process.Start(new ProcessStartInfo(_lastUsedSavePath) { UseShellExecute = true });
            }
        }

        private bool CanExecuteOpenFolder()
        {
            return !string.IsNullOrEmpty(_lastUsedSavePath) && Directory.Exists(_lastUsedSavePath);
        }
    }
}