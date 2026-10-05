using System.Threading.Tasks;
using UnityEngine;

namespace Core.Gear
{
    /// <summary>
    /// 默认配置表来源：从 Addressables 读取 TextAsset(.bytes)，地址 = "ConfigTables/&lt;表名&gt;"，常驻不释放。
    /// </summary>
    public sealed class AddressableTableSource : ITableSource
    {
        // 表文件所在的 Addressables 地址前缀
        private const string AddressPrefix = "ConfigTables/";

        /// <summary>
        /// 按表名从 Addressables 加载表二进制。Lease 挂 Persistent 作用域，表随进程常驻。
        /// </summary>
        /// <param name="tableName">表名（不含扩展名）。</param>
        /// <returns>表二进制内容；加载失败时返回 null 并由上层报错。</returns>
        public async Task<byte[]> LoadTableBytesAsync(string tableName)
        {
            ResourceLease<TextAsset> lease = await AddressableManager.Instance
                .AcquirePersistentAssetAsync<TextAsset>(AddressPrefix + tableName);

            // 常驻资源不主动释放 Lease；TextAsset.bytes 即工具导出的表二进制
            return lease.Asset != null ? lease.Asset.bytes : null;
        }
    }
}
