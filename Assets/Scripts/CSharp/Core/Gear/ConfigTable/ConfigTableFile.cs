using System.Collections.Generic;
using MessagePack;

namespace Core.Gear
{
    /// <summary>
    /// 配置表二进制文件根对象：Excel 导出工具写出与运行时反序列化共用的信封结构。
    /// 使用 map 格式（[Key("名称")]）与工具端按列名匹配，列增删不破坏契约。
    /// </summary>
    /// <typeparam name="TRow">配置表行类型。</typeparam>
    [MessagePackObject]
    public sealed class ConfigTableFile<TRow>
    {
        // 文件格式版本，未来表结构迁移的唯一抓手；[Key] 契约只追加不重排
        [Key("version")]
        public int Version;

        // 表名，与 Excel 页签名、文件名及 Addressable 地址一致
        [Key("tableName")]
        public string TableName;

        // 全部数据行
        [Key("rows")]
        public List<TRow> Rows = new List<TRow>();
    }
}
