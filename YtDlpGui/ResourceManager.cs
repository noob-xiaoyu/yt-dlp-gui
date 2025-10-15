using System;
using System.IO;
using System.Reflection;

namespace YtDlpGui
{
    public static class ResourceManager
    {
        /// <summary>
        /// 从嵌入的资源中提取文件到临时目录，并返回其完整路径。
        /// 如果文件已存在，则直接返回路径，避免重复提取。
        /// </summary>
        /// <param name="resourceName">嵌入资源的完整名称 (格式: ProjectName.FolderName.FileName)</param>
        /// <param name="outputFileName">要保存到临时目录的文件名</param>
        /// <returns>提取出的文件的完整路径</returns>
        public static string ExtractEmbeddedResource(string resourceName, string outputFileName)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            // 构造在临时文件夹中的目标路径
            string outputPath = Path.Combine(Path.GetTempPath(), outputFileName);

            // 如果文件已存在，则直接返回，提高效率
            if (File.Exists(outputPath))
            {
                return outputPath;
            }

            // 从程序集中读取嵌入的资源流
            using (Stream resourceStream = assembly.GetManifestResourceStream(resourceName))
            {
                if (resourceStream == null)
                {
                    throw new Exception($"无法找到嵌入的资源: '{resourceName}'。请检查命名空间和文件名是否正确。");
                }

                // 将资源流写入到临时文件
                using (FileStream fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                {
                    resourceStream.CopyTo(fileStream);
                }
            }

            return outputPath;
        }
    }
}