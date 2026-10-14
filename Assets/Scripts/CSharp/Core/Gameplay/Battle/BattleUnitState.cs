using UnityEngine;

namespace Framework.Gameplay.Battle
{
    /// <summary>战斗阵营（模拟层用枚举；表内 camp 为 string，装配时映射）。</summary>
    public enum BattleCamp
    {
        Character,
        Enemy,
    }

    /// <summary>单位生命状态：Alive 可行动；Downed 倒地（不进索敌候选、不结算，保留注册表等待复活）。</summary>
    public enum UnitVitalState
    {
        Alive,
        Downed,
    }

    /// <summary>
    /// 单位模拟状态（纯 C#，③注册表持有所有权）。
    /// 写权限纪律：意图字段（MoveDestination/Target*）由 ④决策层/角色策略写、③每拍消费；
    /// 结算字段（Hp/Vital/ReviveCount/AttackCooldown）仅 BattleController 改写；
    /// Position 是 ②表现层回写 transform 的唯一豁免写入口（移动执行结果），其余场景禁写。
    /// </summary>
    public sealed class BattleUnitState
    {
        // 单位 id（CHxxxx / ENxxxx，①数据层统一键）
        public string UnitId { get; }

        // 阵营
        public BattleCamp Camp { get; }

        // 统一数值（装配期定，战斗内不变）
        public BattleUnitStats Stats { get; }

        public BattleUnitState(string unitId, BattleCamp camp, BattleUnitStats stats, Vector3 position)
        {
            UnitId = unitId;
            Camp = camp;
            Stats = stats;
            Position = position;
            Hp = stats.MaxHp;
        }

        // ---- 意图字段（④/策略写，③消费） ----

        // 移动目的地意图（⑤ Move 指令/敌人 AI 写，②寻路执行）
        public Vector3 MoveDestination;

        // 攻击目标：单位 id（null = 无单位目标）
        public string TargetUnitId;

        // 攻击目标为守点（守点单一，bool 足够；与 TargetUnitId 互斥）
        public bool TargetIsGuardPoint;

        // 是否有攻击意图
        public bool HasAttackIntent => TargetUnitId != null || TargetIsGuardPoint;

        // ---- 结算状态（仅 BattleController 改写） ----

        // 当前生命
        public int Hp { get; internal set; }

        // 生命状态
        public UnitVitalState Vital { get; internal set; } = UnitVitalState.Alive;

        // 已复活次数（复活费用递增依据）
        public int ReviveCount { get; internal set; }

        // 攻击冷却剩余（秒；0 = 可出手）
        public float AttackCooldown { get; internal set; }

        // 模拟位置（②NavMesh 执行结果每帧回写；验证脚本可直接写以模拟走位）
        public Vector3 Position;

        public bool IsAlive => Vital == UnitVitalState.Alive;
        public bool IsDowned => Vital == UnitVitalState.Downed;
    }
}
