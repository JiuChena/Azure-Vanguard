using System.Collections.Generic;
using UnityEngine;

namespace Framework.Gameplay.Battle
{
    /// <summary>单位快照行（纯数据，②表现层/UI 消费）。</summary>
    public struct BattleUnitSnapshot
    {
        public string UnitId;
        public BattleCamp Camp;
        public UnitVitalState Vital;
        public int Hp;
        public Vector3 Position;
        public int ReviveCount;
    }

    /// <summary>
    /// 战场快照（③→② 的状态出口）：CombatService 持有单一实例每拍刷新，
    /// 表现层轮询/事件读取；②不得经快照反向写模拟状态。
    /// </summary>
    public sealed class BattleSnapshot
    {
        // 点数余额
        public int Points { get; set; }

        // 当前波号（1 基）
        public int WaveIndex { get; set; }

        // 全部波次已生成完
        public bool AllWavesSpawned { get; set; }

        public int GuardPointHp { get; set; }
        public int GuardPointMaxHp { get; set; }

        public BattleOutcome Outcome { get; set; }

        // 单位列表（复用 List，每拍覆盖刷新）
        public List<BattleUnitSnapshot> Units { get; } = new List<BattleUnitSnapshot>();
    }
}
