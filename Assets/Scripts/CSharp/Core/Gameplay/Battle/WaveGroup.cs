using System.Collections.Generic;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 波次编组：一个波次（waveId）的全部出怪行——一行 = 一个出怪点的时间点排程，
    /// 同波多出怪点 = 多行同 waveId。关卡波次序列 = WaveGroup 列表（按 StageTable.waveIds 顺序）。
    /// </summary>
    public sealed class WaveGroup
    {
        /// <summary>波次 ID（WaveTable.waveId 列；对应 StageTable.waveIds 元素）。</summary>
        public string WaveId { get; }

        /// <summary>该波次的出怪行（WaveTable 表序）。</summary>
        public IReadOnlyList<WaveRow> Rows { get; }

        public WaveGroup(string waveId, IReadOnlyList<WaveRow> rows)
        {
            WaveId = waveId;
            Rows = rows;
        }
    }
}
