using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Gear
{
    /// <summary>
    /// 运行时配置表：只读行集合与主键索引，构建后不再修改。
    /// </summary>
    /// <typeparam name="TRow">配置表行类型。</typeparam>
    /// <typeparam name="TKey">主键类型。</typeparam>
    public sealed class DataTable<TRow, TKey> where TRow : IConfigTableRow<TKey>
    {
        // 全部数据行，保持表内原始顺序
        private readonly List<TRow> _rows;

        // 主键 → 行 的索引，构建期查重
        private readonly Dictionary<TKey, TRow> _lookup;

        // 表名，用于日志定位
        private readonly string _tableName;

        // 表文件格式版本，透传自 ConfigTableFile
        public int Version { get; }

        // 行数
        public int Count => _rows.Count;

        // 全部数据行（只读）
        public IReadOnlyList<TRow> Rows => _rows;

        /// <summary>
        /// 由反序列化出的行集合构建配置表，并建立主键索引。
        /// </summary>
        /// <param name="rows">反序列化出的数据行，允许为 null（视为空表）。</param>
        /// <param name="tableName">表名，用于日志定位。</param>
        /// <param name="version">表文件格式版本。</param>
        public DataTable(List<TRow> rows, string tableName, int version)
        {
            _rows = rows ?? new List<TRow>();
            _tableName = tableName ?? string.Empty;
            Version = version;
            _lookup = new Dictionary<TKey, TRow>(_rows.Count);

            // 逐行建立索引；主键重复时报错并保留先出现的行，避免脏数据静默覆盖
            for (int i = 0; i < _rows.Count; i++)
            {
                TRow row = _rows[i];
                if (row == null)
                {
                    Debug.LogError($"配置表 '{_tableName}' 第 {i} 行为空，已跳过。");
                    continue;
                }
                if (_lookup.TryGetValue(row.PrimaryKey, out TRow existing))
                {
                    Debug.LogError($"配置表 '{_tableName}' 主键 '{row.PrimaryKey}' 重复（保留先出现的行：{existing}）。");
                    continue;
                }
                _lookup.Add(row.PrimaryKey, row);
            }
        }

        /// <summary>
        /// 按主键查询行。
        /// </summary>
        /// <param name="key">行主键。</param>
        /// <param name="row">命中的行；未命中时为 default。</param>
        /// <returns>命中返回 true。</returns>
        public bool TryGet(TKey key, out TRow row)
        {
            return _lookup.TryGetValue(key, out row);
        }

        /// <summary>
        /// 按主键查询行；未命中时报错并返回 default。
        /// </summary>
        /// <param name="key">行主键。</param>
        /// <returns>命中的行；未命中返回 default。</returns>
        public TRow Get(TKey key)
        {
            if (_lookup.TryGetValue(key, out TRow row)) return row;

            Debug.LogError($"配置表 '{_tableName}' 中不存在主键 '{key}' 的行。");
            return default;
        }

        /// <summary>
        /// 遍历全部行主键（无序）。
        /// </summary>
        public IEnumerable<TKey> Keys => _lookup.Keys;
    }
}
