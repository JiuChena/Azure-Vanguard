using System.IO;
using UnityEngine;

namespace Core.Gear
{
    /// <summary>
    /// 二进制数据管理器，基于 MessagePack 实现持久化数据的序列化与反序列化。
    /// </summary>
    public class BinaryDataManager
    {
        // 单例实例
        private static readonly BinaryDataManager _instance = new BinaryDataManager();
        public static BinaryDataManager Instance => _instance;

        // 数据存储根目录：Application.persistentDataPath/Data/
        private static readonly string DataPath = Path.Combine(Application.persistentDataPath, "Data");

        // MessagePack 序列化运行时
        private readonly MessagePackRuntime _runtime = new MessagePackRuntime();

        private BinaryDataManager() { }

        /// <summary>
        /// 将对象序列化为 MessagePack 二进制并写入文件。
        /// </summary>
        /// <param name="path">子目录路径（相对于 Data/）</param>
        /// <param name="fileName">文件名（不含扩展名）</param>
        /// <param name="data">要保存的数据对象</param>
        public void Save<T>(string path, string fileName, T data)
        {
            // 确保目标目录存在
            string directoryPath = GetDirectoryPath(path);
            if (!Directory.Exists(directoryPath)) Directory.CreateDirectory(directoryPath);

            // 序列化并写入文件
            byte[] bytes = _runtime.Serialize(data);
            File.WriteAllBytes(GetFilePath(path, fileName), bytes);
        }

        /// <summary>
        /// 从文件读取并反序列化为对象。文件不存在或反序列化失败时返回 default。
        /// </summary>
        /// <param name="path">子目录路径（相对于 Data/）</param>
        /// <param name="fileName">文件名（不含扩展名）</param>
        /// <returns>反序列化出的数据对象；文件不存在或反序列化失败时返回 default。</returns>
        public T Load<T>(string path, string fileName)
        {
            string filePath = GetFilePath(path, fileName);
            if (!File.Exists(filePath)) return default;

            // 读取并反序列化
            byte[] bytes = File.ReadAllBytes(filePath);
            return _runtime.Deserialize<T>(bytes);
        }

        /// <summary>
        /// 检查 Data/ 根目录下指定文件是否存在。
        /// </summary>
        /// <param name="fileName">文件名（不含扩展名）</param>
        /// <returns>文件存在时返回 true。</returns>
        public bool FileExists(string fileName)
        {
            return File.Exists(Path.Combine(DataPath, fileName + ".bin"));
        }

        /// <summary>
        /// 检查指定路径和名称的文件是否存在。
        /// </summary>
        /// <param name="path">子目录路径（相对于 Data/）</param>
        /// <param name="fileName">文件名（不含扩展名）</param>
        /// <returns>文件存在时返回 true。</returns>
        public bool FileExists(string path, string fileName)
        {
            return File.Exists(GetFilePath(path, fileName));
        }

        /// <summary>
        /// 规范化子目录路径并拼接为 Data/ 下的完整目录。
        /// </summary>
        /// <param name="path">子目录路径（相对于 Data/）</param>
        /// <returns>Data/ 下的完整目录路径。</returns>
        private static string GetDirectoryPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return DataPath;

            string normalizedPath = path.Replace("\\", "/").Trim('/');
            return string.IsNullOrEmpty(normalizedPath) ? DataPath : Path.Combine(DataPath, normalizedPath);
        }

        /// <summary>
        /// 拼接目录与文件名，生成带 .bin 扩展名的完整文件路径。
        /// </summary>
        /// <param name="path">子目录路径（相对于 Data/）</param>
        /// <param name="fileName">文件名（不含扩展名）</param>
        /// <returns>完整文件路径。</returns>
        private static string GetFilePath(string path, string fileName)
        {
            return Path.Combine(GetDirectoryPath(path), fileName + ".bin");
        }
    }
}
