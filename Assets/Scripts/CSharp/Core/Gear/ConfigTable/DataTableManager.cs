using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Core.Gear
{
    /// <summary>
    /// 配置表管理器：按表名加载并缓存 <see cref="DataTable{TRow,TKey}"/>，数据来源可替换。
    /// </summary>
    public sealed class DataTableManager
    {
        // 单例实例
        private static readonly DataTableManager _instance = new DataTableManager();
        public static DataTableManager Instance => _instance;

        // 表缓存：表名 → 已构建的 DataTable（object 存储，取用时按类型校验）
        private readonly Dictionary<string, object> _tables = new Dictionary<string, object>();

        // 表二进制数据来源；未注入时默认走 Addressables
        private ITableSource _source;

        // MessagePack 反序列化封装，与存档共用统一选项
        private readonly MessagePackRuntime _runtime = new MessagePackRuntime();

        // 当前工具导出的文件格式版本约定
        private const int CurrentFileVersion = 1;

        private DataTableManager() { }

        /// <summary>
        /// 注入表数据来源；不注入时默认使用 Addressables。应在首次加载前调用。
        /// </summary>
        /// <param name="source">新的数据来源，不允许为 null。</param>
        public void SetSource(ITableSource source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        /// <summary>
        /// 加载配置表；已加载过的表直接返回缓存。
        /// </summary>
        /// <typeparam name="TRow">行类型，需与导出工具的列一一对应。</typeparam>
        /// <typeparam name="TKey">主键类型。</typeparam>
        /// <param name="tableName">表名（不含扩展名，与 Excel 页签名一致）。</param>
        /// <returns>配置表；数据为空或反序列化失败时返回 null。</returns>
        public async Task<DataTable<TRow, TKey>> LoadTableAsync<TRow, TKey>(string tableName)
            where TRow : class, IConfigTableRow<TKey>
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentException("表名不能为空。", nameof(tableName));
            }

            // 命中缓存：类型一致直接返回，不一致说明调用方行类型写错
            if (_tables.TryGetValue(tableName, out object cached))
            {
                if (cached is DataTable<TRow, TKey> typed) return typed;
                throw new InvalidOperationException(
                    $"配置表 '{tableName}' 已按 {cached.GetType().Name} 加载，与请求的行类型 {typeof(TRow).Name} 不符。");
            }

            // 加载二进制并反序列化
            _source ??= new AddressableTableSource();
            byte[] bytes = await _source.LoadTableBytesAsync(tableName);
            if (bytes == null || bytes.Length == 0)
            {
                Debug.LogError($"配置表 '{tableName}' 数据为空，请检查导出工具是否已生成该表。");
                return null;
            }

            ConfigTableFile<TRow> file = _runtime.Deserialize<ConfigTableFile<TRow>>(bytes);
            if (file?.Rows == null)
            {
                Debug.LogError($"配置表 '{tableName}' 反序列化失败，请检查行类型 [Key] 列名是否与 Excel 表头一致。");
                return null;
            }
            if (file.Version > CurrentFileVersion)
            {
                Debug.LogWarning($"配置表 '{tableName}' 文件版本 {file.Version} 高于运行时约定的 {CurrentFileVersion}，可能需要更新客户端。");
            }

            // 构建表并缓存
            DataTable<TRow, TKey> table = new DataTable<TRow, TKey>(file.Rows, tableName, file.Version);
            _tables.Add(tableName, table);
            return table;
        }
    }
}
