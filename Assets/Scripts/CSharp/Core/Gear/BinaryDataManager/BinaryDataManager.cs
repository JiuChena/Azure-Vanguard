using System.IO;
using UnityEngine;

namespace Core.Gear
{
    /// <summary>
    /// 存档读取结果状态：Missing = 从未写过（真新档）；Ok = 读取成功；Corrupt = 正式档与备份均无法反序列化。
    /// </summary>
    public enum BinaryLoadStatus
    {
        // 文件不存在（首次游玩）
        Missing,

        // 读取成功（含正式档损坏后回退备份成功的情形）
        Ok,

        // 正式档与备份均损坏
        Corrupt,
    }

    /// <summary>
    /// 二进制数据管理器，基于 MessagePack 实现持久化数据的序列化与反序列化。
    /// 写入采用 临时文件 → File.Replace 原子替换（旧档自动写为 .bak） 两步提交：
    /// 任一步被中断，正式档要么是完整的旧数据要么是完整的新数据，不会留下半截文件。
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
        /// 将对象序列化为 MessagePack 二进制并持久化。
        /// 两步提交：写 .tmp → File.Replace 原子替换（旧档自动写为 .bak 保留上一代）。
        /// </summary>
        /// <param name="path">子目录路径（相对于 Data/）</param>
        /// <param name="fileName">文件名（不含扩展名）</param>
        /// <param name="data">要保存的数据对象</param>
        public void Save<T>(string path, string fileName, T data)
        {
            // 确保目标目录存在
            string directoryPath = GetDirectoryPath(path);
            if (!Directory.Exists(directoryPath)) Directory.CreateDirectory(directoryPath);

            string filePath = GetFilePath(path, fileName);
            string tempPath = filePath + ".tmp";
            string backupPath = filePath + ".bak";

            // 先写临时文件：写入中途被中断时正式档与备份都不受影响
            byte[] bytes = _runtime.Serialize(data);
            File.WriteAllBytes(tempPath, bytes);

            // 原子替换：正式档存在时 File.Replace 一步完成「替换 + 旧档写为备份」；
            // 首次保存直接改名落位（File.Replace 三参重载在本项目 API 级别不可用，见 CS1501）
            if (File.Exists(filePath)) File.Replace(tempPath, filePath, backupPath);
            else File.Move(tempPath, filePath);
        }

        /// <summary>
        /// 从文件读取并反序列化为对象。文件不存在或反序列化失败时返回 default。
        /// 正式档损坏时自动回退上一代备份（.bak），备份成功读取则返回备份数据。
        /// </summary>
        /// <param name="path">子目录路径（相对于 Data/）</param>
        /// <param name="fileName">文件名（不含扩展名）</param>
        /// <returns>反序列化出的数据对象；文件不存在或正式档与备份均损坏时返回 default。</returns>
        public T Load<T>(string path, string fileName) where T : class
        {
            return TryLoad(path, fileName, out T data, out _) ? data : default;
        }

        /// <summary>
        /// 从文件读取并反序列化为对象，区分「从未写过」「读取成功」「损坏」三种结果；
        /// 正式档损坏时自动回退上一代备份。需要区分新账号与损坏档的调用方用本入口。
        /// </summary>
        /// <param name="path">子目录路径（相对于 Data/）</param>
        /// <param name="fileName">文件名（不含扩展名）</param>
        /// <param name="data">读取成功时的数据对象；未命中或损坏为 default。</param>
        /// <param name="status">读取结果状态。</param>
        /// <returns>读取成功（含备份回退成功）返回 true。</returns>
        public bool TryLoad<T>(string path, string fileName, out T data, out BinaryLoadStatus status)
            where T : class
        {
            data = default;
            status = BinaryLoadStatus.Missing;

            string filePath = GetFilePath(path, fileName);
            if (!File.Exists(filePath)) return false;

            // 正式档：反序列化失败时运行时归并为 null（不抛出）
            data = _runtime.Deserialize<T>(File.ReadAllBytes(filePath));
            if (data != null)
            {
                status = BinaryLoadStatus.Ok;
                return true;
            }

            // 正式档损坏 → 自动回退上一代备份；备份也缺/坏才算真损坏
            string backupPath = filePath + ".bak";
            if (File.Exists(backupPath))
            {
                T backup = _runtime.Deserialize<T>(File.ReadAllBytes(backupPath));
                if (backup != null)
                {
                    Debug.LogWarning($"[BinaryDataManager] 存档损坏，已回退上一代备份：{filePath}");
                    data = backup;
                    status = BinaryLoadStatus.Ok;
                    return true;
                }
            }

            Debug.LogError($"[BinaryDataManager] 存档与备份均无法读取：{filePath}");
            data = default;
            status = BinaryLoadStatus.Corrupt;
            return false;
        }

        /// <summary>
        /// 删除 Data/ 下的指定存档文件（含遗留临时/备份文件）；文件不存在时静默返回。
        /// </summary>
        /// <param name="path">子目录路径（相对于 Data/）</param>
        /// <param name="fileName">文件名（不含扩展名）</param>
        public void Delete(string path, string fileName)
        {
            string filePath = GetFilePath(path, fileName);
            if (File.Exists(filePath)) File.Delete(filePath);
            if (File.Exists(filePath + ".tmp")) File.Delete(filePath + ".tmp");
            if (File.Exists(filePath + ".bak")) File.Delete(filePath + ".bak");
        }

        /// <summary>
        /// 删除 Data/ 下的指定子目录及其全部内容（调试用；目录不存在时静默返回）。
        /// </summary>
        /// <param name="path">子目录路径（相对于 Data/）</param>
        public void WipeFolder(string path)
        {
            string directoryPath = GetDirectoryPath(path);
            if (Directory.Exists(directoryPath)) Directory.Delete(directoryPath, true);
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
        /// 检查 Data/ 下的指定子目录是否存在。
        /// </summary>
        /// <param name="path">子目录路径（相对于 Data/）</param>
        /// <returns>目录存在时返回 true。</returns>
        public bool FolderExists(string path)
        {
            return Directory.Exists(GetDirectoryPath(path));
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
