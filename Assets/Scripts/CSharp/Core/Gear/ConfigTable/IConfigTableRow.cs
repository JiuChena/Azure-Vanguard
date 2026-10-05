namespace Core.Gear
{
    /// <summary>
    /// 配置表行约定：提供主键访问，供 <see cref="DataTable{TRow,TKey}"/> 建立索引。
    /// </summary>
    /// <typeparam name="TKey">主键类型，常用 int 或 string。</typeparam>
    public interface IConfigTableRow<TKey>
    {
        /// <summary>当前行的主键，在同一张表内必须唯一。</summary>
        TKey PrimaryKey { get; }
    }
}
