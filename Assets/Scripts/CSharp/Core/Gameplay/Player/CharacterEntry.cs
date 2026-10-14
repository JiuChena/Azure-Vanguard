using System;
using System.Collections.Generic;
using MessagePack;

namespace Framework.Gameplay.Player
{
    /// <summary>
    /// 单个角色的养成条目：等级/经验/突破/技能等级/已解锁节点。
    /// 形状为 2026-09-28 养成方案定稿；新增养成维度时在此追加字段（[Key] 只追加不重排）。
    /// </summary>
    [Serializable]
    [MessagePackObject]
    public class CharacterEntry
    {
        // 单位 ID，与 CharacterTable 主键、CharacterUnitConfig.unitId 及头像图集 sprite 名对齐（如 CH0221）
        [Key(0)]
        public string unitId;

        // 角色等级，下限 1；上限由养成层按 LevelTable 控制，仓库只做下限防御
        [Key(1)]
        public int level = 1;

        // 当前等级内已累积经验；升级结算公式在养成业务逻辑中（公式是死的不进表），仓库只存值
        [Key(2)]
        public int exp;

        // 突破阶段：0 = 未突破（等级上限为初始值），1~N 对应 BreakthroughTable 行
        [Key(3)]
        public int breakStage;

        // 普攻技能等级，下限 1
        [Key(4)]
        public int skillLevelNormal = 1;

        // 小技能（E）等级，下限 1
        [Key(5)]
        public int skillLevelE = 1;

        // 爆发技能（Q）等级，下限 1
        [Key(6)]
        public int skillLevelQ = 1;

        // 已解锁节点 ID 列表（天赋 + 属性加成，对应 UnlockNodeTable 主键），解锁即永久
        [Key(7)]
        public List<int> unlockedNodes = new();

        // 绑定武器等级，下限 1；上限随角色突破（与角色等级共用 GetLevelCap）
        // 注意：旧档反序列化缺失键时为 0，消费方须按 Math.Max(1, weaponLevel) 兜底
        [Key(8)]
        public int weaponLevel = 1;

        // 绑定武器当前等级内已累积经验；升级结算公式在养成业务逻辑中，仓库只存值
        [Key(9)]
        public int weaponExp;

        // 天赋技能等级，下限 1
        // 注意：旧档反序列化缺失键时为 0，仓库加载校验会兜底为 1
        [Key(10)]
        public int skillLevelTalent = 1;
    }
}
