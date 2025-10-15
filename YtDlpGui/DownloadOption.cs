namespace YtDlpGui
{
    public class DownloadOption
    {
        // 显示在下拉列表中的文本
        public string Description { get; set; }

        // 传递给 yt-dlp.exe 的命令行参数
        public string Arguments { get; set; }
    }
}