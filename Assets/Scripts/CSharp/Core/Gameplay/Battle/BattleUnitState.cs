using System.Collections.Generic;
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
    /// 结算字段（Hp/Vital/ReviveCount/AttackCooldown/Energy）由 BattleController 与
    /// BE 链回能回调（表现层经 GAS 访问）改写——同 Hp 伤害链模式；
    /// Position 是 ②表现层回写 transform 的唯一豁免写入口（移动执行结果），其余场景禁写。
    /// </summary>
    public sealed class BattleUnitState
    {
        // 单位 id（CHxxxx / ENxxxx，①数据层统一键）
        public string UnitId { get; }

        // 数据缓存键（= BattleDataModule 三缓存统一键；角色 = UnitId，敌人 = id@等级 如 EN_Solider_AR_0001@1）。
        // 仅数据查找（数值/普攻/stat 基底）用；单位身份、索敌匹配、视图 prefab 地址一律 UnitId。
        public string StatsKey { get; }

        // 阵营
        public BattleCamp Camp { get; }

        // 统一数值（装配期定，战斗内不变）
        public BattleUnitStats Stats { get; }

        public BattleUnitState(string unitId, BattleCamp camp, BattleUnitStats stats, Vector3 position,
            IReadOnlyList<Vector3> route = null, string statsKey = null)
        {
            UnitId = unitId;
            StatsKey = statsKey ?? unitId;
            Camp = camp;
            Stats = stats;
            Position = position;
            // 移动目的地初始为出生点：语义"原地已到位"，防目的地默认零向量被距离判定误读为移动中
            MoveDestination = position;
            Route = route;
            Hp = stats.MaxHp;
        }

        // ---- 路线制机动数据（出生时定，战斗内不变） ----

        // 行进路线（敌人波次行配给：有序路点，严格沿折线走、无视障碍，聚堆为设计预期；
        // null = 无路线，意图策略兜底直奔守点。角色不配路线，走 NavMesh 自由寻路）
        public IReadOnlyList<Vector3> Route { get; }

        // 路点进度索引（敌人策略写：抵达即前移；单调不减——途中遇敌停走/目标死亡后从该路点继续，
        // 永不开倒车。无路线时恒 0 不参与）
        public int RouteIndex;

        // ---- 意图字段（④/策略写，③消费） ----

        // 移动目的地意图（⑤ Move 指令/敌人 AI 写，②寻路执行）
        public Vector3 MoveDestination;

        // 攻击目标：单位 id（null = 无单位目标）
        public string TargetUnitId;

        // 攻击目标为守点（守点单一，bool 足够；与 TargetUnitId 互斥）
        public bool TargetIsGuardPoint;

        // 是否有攻击意图
        public bool HasAttackIntent => TargetUnitId != null || TargetIsGuardPoint;

        // ---- 停走距离抖动（基准 = 索敌范围 range 列；出生时 ③ 采样，战斗内不变） ----

        // 索敌/停走距离随机偏移（米）：同种敌人停在略有差异的半径、聚堆点错开，
        // 替代软分离推挤（推挤会把敌人挤过障碍/挤进高台）。范围判定统一读 StopRange。
        public float StopRangeOffset;

        // 实际停走距离 = 索敌范围（range 列）+ 随机偏移（角色不走出生采样入口，恒 0 偏移行为不变）
        public float StopRange => Stats.Range + StopRangeOffset;

        // ---- 结算状态（仅 BattleController 改写） ----

        // 当前生命
        public int Hp { get; internal set; }

        // 生命状态
        public UnitVitalState Vital { get; internal set; } = UnitVitalState.Alive;

        // 已复活次数（复活费用递增依据）
        public int ReviveCount { get; internal set; }

        // 攻击冷却剩余（秒；0 = 可出手）
        public float AttackCooldown { get; internal set; }

        // 最后命中本单位的攻击者 id（子弹命中回调写；击杀归属——Step 死亡扫描据此回能，null = 无来源）
        public string LastHitBy;

        // 当前能量（0~MaxEnergy；命中/击杀/技能施放回能入账，Q 大招消耗；上限 = Stats.MaxEnergy，
        // 角色表 maxEnergy 列，敌人无能量恒 0——能量管理器统一入账，clamp 到各自上限）
        public int Energy { get; internal set; }

        // 当前释放中的技能 id（skill 态进入写、结束清；技能效果执行器读它取 effectIds）
        public int CastingSkillId;

        // 减速效果剩余秒（slow 效果行 duration 写入，Step 推进恢复；0 = 未减速）
        public float SlowRemaining;

        // 移动速度乘数（slow 效果应用，默认 1 = 不减速；移动执行乘此值）
        public float MoveSlowFactor = 1f;

        // 能量上限（装配自角色表 maxEnergy 列；能量管理器注册时传参依据）
        public int MaxEnergy => Stats.MaxEnergy;

        // 模拟位置（②NavMesh 执行结果每帧回写；验证脚本可直接写以模拟走位）
        public Vector3 Position;

        public bool IsAlive => Vital == UnitVitalState.Alive;
        public bool IsDowned => Vital == UnitVitalState.Downed;
    }
}
