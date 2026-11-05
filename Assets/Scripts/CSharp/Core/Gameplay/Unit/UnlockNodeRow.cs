using Core.Gear;
using MessagePack;

namespace Framework.Gameplay.Unit
{
    /// <summary>
    /// 解锁节点表行：对应 Assets/Module/Unit/Character/Excel/UnlockNodeTable.xlsx。
    /// 天赋（被动触发）与属性加成共用一张表：都是"无等级、一次解锁、带一个效果"。
    /// type = talent 时 effectKey 是行为逻辑键（同 BehaviorKey 惯例），effectValue 是其参数；
    /// type = bonus 时 effectKey 是属性名，effectValue 是加成值（进属性公式固定加成区）。
    /// 解锁两条路并存：cost 非空 = 独立材料解锁；cost 空 = 随突破解锁（见 BreakthroughTable.unlockNodeIds）。
    /// </summary>
    [MessagePackObject]
    public sealed class UnlockNodeRow : IConfigTableRow<int>
    {
        /// <summary>节点类型值（type 列的约定取值）。</summary>
        public static class NodeTypes
        {
            // 天赋：被动触发逻辑，effectKey = 行为逻辑键
            public const string Talent = "talent";
            // 属性加成：effectKey = 属性名（hp_pct/atk_pct/crit_rate/crit_dmg…）
            public const string Bonus = "bonus";
        }

        // 节点 id，全表唯一主键
        [Key("nodeId")]
        public int NodeId;

        // 节点类型：talent / bonus
        [Key("type")]
        public string Type;

        // 效果键：talent = 行为逻辑键；bonus = 属性名
        [Key("effectKey")]
        public string EffectKey;

        // 加成数值（talent 时为行为参数，bonus 时为加成值）
        [Key("effectValue")]
        public float EffectValue;

        // 天赋行为参数（2026-11-04）：{键:初值:成长} 逗号分隔；talent 行用，bonus 行留空
        [Key("params")]
        public string Params;

        // 独立解锁消耗 itemId:count 逗号分隔；空 = 随突破解锁
        [Key("cost")]
        public string Cost;

        // 说明文本
        [Key("desc")]
        public string Desc;

        /// <summary>解析后的独立解锁消耗；空列返回空数组（即随突破解锁）。</summary>
        [IgnoreMember]
        public ItemCost[] CostList => ItemCost.ParseList(Cost);

        /// <summary>解析后的天赋行为参数；空列返回空数组。</summary>
        [IgnoreMember]
        public SkillParam[] ParamList => SkillParam.ParseList(Params);

        /// <summary>按等级读取天赋参数值（键缺失返回 0）。</summary>
        public float GetParamValue(string key, int level)
        {
            SkillParam[] list = ParamList;
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i].Key == key) return list[i].GetValue(level);
            }
            return 0f;
        }

        /// <summary>是否随突破解锁（独立消耗列为空）。</summary>
        [IgnoreMember]
        public bool UnlockWithBreakthrough => string.IsNullOrWhiteSpace(Cost);

        /// <summary>表行主键。</summary>
        [IgnoreMember]
        public int PrimaryKey => NodeId;
    }
}
