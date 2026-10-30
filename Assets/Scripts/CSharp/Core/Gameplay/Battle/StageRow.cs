using Core.Gear;
using System;
using System.Collections.Generic;
using MessagePack;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 关卡波次序列表行：对应 Assets/Module/Battle/Excel/StageTable.xlsx。
    /// 一行 = 一关的波次推进序列（静态参数在 LevelConfig SO 配置）。
    /// waveIds 以分号分隔（如 W0001;W0002），按书写顺序驱动波次推进。行主键 = id 列。
    /// </summary>
    [MessagePackObject]
    public sealed class StageRow : IConfigTableRow<string>
    {
        // 行主键（如 LV_0001），全表唯一
        [Key("id")]
        public string Id;

        // 波次推进序列（分号分隔，如 W0001;W0002）
        [Key("waveIds")]
        public string WaveIdsText;

        [IgnoreMember]
        private string[] waveIds;

        /// <summary>波次推进序列（懒解析 waveIds 分号拆分 + trim；空串返回空数组）。</summary>
        [IgnoreMember]
        public string[] WaveIds => waveIds ??= ParseWaveIds();

        private string[] ParseWaveIds()
        {
            if (string.IsNullOrWhiteSpace(WaveIdsText)) return Array.Empty<string>();
            string[] parts = WaveIdsText.Split(';');
            List<string> ids = new List<string>(parts.Length);
            for (int i = 0; i < parts.Length; i++)
            {
                string waveId = parts[i].Trim();
                if (!string.IsNullOrEmpty(waveId)) ids.Add(waveId);
            }
            return ids.ToArray();
        }

        /// <summary>表行主键。</summary>
        [IgnoreMember]
        public string PrimaryKey => Id;
    }
}
