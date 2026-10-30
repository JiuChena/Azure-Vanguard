using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Gear;
using Framework.Gameplay.Abilities.Stat;

namespace Framework.Gameplay.Buff
{
    /// <summary>
    /// Buff 表运行时查询门面（展示层数据源）：BuffTable 的字典热缓存。
    /// 表经 DataTableManager 加载并全期缓存；职责单一——按 buffId 取行元数据。
    /// 未加载时查询返回 false（不抛错），展示层先 EnsureLoadedAsync 再查询。
    /// </summary>
    public static class BuffDataLibrary
    {
        // Buff 表缓存（EnsureLoadedAsync 成功后可用；跨编辑器 run_script 存活，验证后需置空重载）
        private static DataTable<BuffRow, int> table;

        // 加载任务（并发调用共享同一次加载）
        private static Task<DataTable<BuffRow, int>> loadTask;

        /// <summary>
        /// 确保 Buff 表已加载（幂等，并发调用共享同一次加载）；
        /// 表加载失败时保持未加载状态，下次调用重试。
        /// </summary>
        public static async Task EnsureLoadedAsync()
        {
            if (table != null) return;

            loadTask ??= DataTableManager.Instance.LoadTableAsync<BuffRow, int>("BuffTable");
            table = await loadTask;
        }

        /// <summary>
        /// 按 buffId 查 buff 元数据行。
        /// </summary>
        /// <param name="buffId">BuffTable 主键。</param>
        /// <param name="row">命中的表行；未命中或表未加载时为 null。</param>
        /// <returns>命中返回 true。</returns>
        public static bool TryGetBuffRow(int buffId, out BuffRow row)
        {
            row = null;
            if (table == null) return false;
            return table.TryGet(buffId, out row);
        }

        /// <summary>
        /// 按 buffId 取加成数据（2026-10-29 多属性组合：statType/value/growth 逗号分隔，一条 buff 多组属性）。
        /// 等级成长 value + growth×(level−1) 由调用方（效果函数）结算。
        /// </summary>
        /// <param name="buffId">BuffTable 主键。</param>
        /// <param name="statTypes">加成属性列表（StatType）。</param>
        /// <param name="values">1 级加成值（与 statTypes 对齐）。</param>
        /// <param name="growths">每级增量（与 statTypes 对齐）。</param>
        /// <param name="duration">持续秒数。</param>
        /// <returns>命中且至少一组可映射返回 true。</returns>
        public static bool TryGetBuffModifiers(int buffId, out List<StatType> statTypes, out List<float> values,
            out List<float> growths, out float duration)
        {
            statTypes = new List<StatType>();
            values = new List<float>();
            growths = new List<float>();
            duration = 0f;
            if (!TryGetBuffRow(buffId, out BuffRow row) || string.IsNullOrEmpty(row.StatType)) return false;

            string[] types = row.StatType.Split(',');
            string[] vals = string.IsNullOrEmpty(row.Value) ? null : row.Value.Split(',');
            string[] grs = string.IsNullOrEmpty(row.Growth) ? null : row.Growth.Split(',');

            for (int i = 0; i < types.Length; i++)
            {
                string typeName = types[i].Trim();
                StatType statType;
                switch (typeName)
                {
                    case "atk": statType = StatType.Atk; break;
                    case "critrate": statType = StatType.CritRate; break;
                    case "critdmg": statType = StatType.CritDmg; break;
                    case "def": statType = StatType.Def; break;
                    case "movespeed": statType = StatType.MoveSpeed; break;
                    default: continue;
                }
                float value = vals != null && i < vals.Length && float.TryParse(vals[i].Trim(), out float v) ? v : 0f;
                float growth = grs != null && i < grs.Length && float.TryParse(grs[i].Trim(), out float g) ? g : 0f;
                statTypes.Add(statType);
                values.Add(value);
                growths.Add(growth);
            }
            duration = row.Duration;
            return statTypes.Count > 0;
        }
    }
}
