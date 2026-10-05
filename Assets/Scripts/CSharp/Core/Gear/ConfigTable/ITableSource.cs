using System.Threading.Tasks;

namespace Core.Gear
{
    /// <summary>
    /// 配置表数据来源抽象：按表名取二进制字节。
    /// 后续接入热更下载或服务端推送的表，实现此接口替换 <see cref="DataTableManager"/> 的来源即可。
    /// </summary>
    public interface ITableSource
    {
        /// <summary>
        /// 按表名加载表二进制。
        /// </summary>
        /// <param name="tableName">表名（不含扩展名）。</param>
        /// <returns>表二进制内容；来源中不存在该表时返回 null。</returns>
        Task<byte[]> LoadTableBytesAsync(string tableName);
    }
}
