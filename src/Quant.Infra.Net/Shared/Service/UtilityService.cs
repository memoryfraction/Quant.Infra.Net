using System;
using System.IO;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Shared.Service
{
    public class UtilityService 
    {
        public static async Task IsPathExistAsync(string fullPathFilename)
        {
            // 检查入参有效性
            if (string.IsNullOrEmpty(fullPathFilename))
                throw new ArgumentNullException($"Invalid parameter:{fullPathFilename}");

            // 从完整路径中获取目录路径
            var directoryPath = Path.GetDirectoryName(fullPathFilename);
            if (directoryPath == null)
            {
                throw new ArgumentException("Invalid path");
            }

            // 检查文件夹是否存在
            if (!Directory.Exists(directoryPath))
            {
                try
                {
                    // 异步创建文件夹
                    await Task.Run(() => Directory.CreateDirectory(directoryPath));
                    Console.WriteLine("Folder created: " + directoryPath);
                }
                catch (Exception ex)
                {
                    // 处理可能出现的异常（例如权限问题）
                    Console.WriteLine("An error occurred: " + ex.Message);
                    throw;
                }
            }
        }
    }
}
