using System.Collections.Generic;
using Framework.Gameplay.Abilities.Stat;
using Framework.Gameplay.Battle;

namespace Framework.Gameplay.Abilities.Display
{
    /// <summary>
    /// 单位信息显示模型：能力层 ↔ UI 层的唯一数据契约（纯 C#，零 Unity 依赖）。
    /// 由 <see cref="DisplayRuntimeData.BuildModel"/> 每次读取现场产出全新实例（HP 每帧变，
    /// 面板本就逐帧读，零缓存无脏标记）。View 只消费本模型渲染，不反查任何运行时。
    /// </summary>
    public sealed class DisplayModel
    {
        // ---- 头部（恒产，与 SO 条目无关） ----

        /// <summary>单位 ID。</summary>
        public string UnitId;

        /// <summary>显示名（异步回填：角色 UnitConfig.Name / 敌人 EnemyRow.Name；未回填 = UnitId）。</summary>
        public string DisplayName;

        /// <summary>阵营。</summary>
        public BattleCamp Camp;

        /// <summary>生命状态。</summary>
        public UnitVitalState Vital;

        /// <summary>当前生命。</summary>
        public int CurrentHp;

        /// <summary>最大生命（Stat final MaxHp，缺失回退单位 Stats.MaxHp）。</summary>
        public int MaxHp;

        /// <summary>生命比例 0..1（MaxHp ≤ 0 时取 0）。</summary>
        public float HpRatio;

        // ---- 条目段（按 SO 条目序） ----

        /// <summary>字段条目（顺序 = SO 列表序 = 面板行序）。</summary>
        public List<DisplayFieldValue> Fields = new List<DisplayFieldValue>();

        // ---- buff 汇总段（v1 恒空，结构预留） ----

        /// <summary>buff 汇总（v1 buff 系统未落地恒空；按 SourceId 分组）。</summary>
        public List<DisplayBuffValue> Buffs = new List<DisplayBuffValue>();
    }

    /// <summary>
    /// 一条显示字段：由 <see cref="DisplayFieldEntry"/>（key/label/style）与当前值格式化而成。
    /// </summary>
    public sealed class DisplayFieldValue
    {
        /// <summary>条目稳定键（View 槽位映射/测试断言）。</summary>
        public string Key;

        /// <summary>标签（英文）。</summary>
        public string Label;

        /// <summary>渲染风格（View 按此决定文本直写还是驱动比例条）。</summary>
        public DisplayStyle Style;

        /// <summary>格式化后的文本（Bar 风格为百分数文本，同时填 Ratio）。</summary>
        public string Text;

        /// <summary>比例条填充 0..1（仅 Bar 风格有效，其余 0）。</summary>
        public float Ratio;

        /// <summary>是否被 buff 影响（该属性存在 Modifier 条目，View 可高亮）。</summary>
        public bool Modified;
    }

    /// <summary>
    /// 一条 buff 汇总：同一 SourceId 的叠层计数 + 各属性边际贡献。
    /// v1 无时长/图标/名称表——SourceId 即 DisplayName 兜底，RemainingTime/MaxTime 预留 0。
    /// </summary>
    public sealed class DisplayBuffValue
    {
        /// <summary>来源标识（装备/buff 唯一键）。</summary>
        public string SourceId;

        /// <summary>显示名（v1 = SourceId 兜底；真 buff 系统带名称表后替换）。</summary>
        public string DisplayName;

        /// <summary>叠层数（同 SourceId 的 Modifier 条数）。</summary>
        public int StackCount;

        /// <summary>剩余时长（预留；v1 无时长恒 0）。</summary>
        public float RemainingTime;

        /// <summary>总时长（预留；v1 无时长恒 0）。</summary>
        public float MaxTime;

        /// <summary>各被影响属性的边际贡献（final 全量 − 剔除本来源后）。</summary>
        public List<DisplayBuffStatEffect> Effects = new List<DisplayBuffStatEffect>();
    }

    /// <summary>一条 buff 对某属性的边际贡献。</summary>
    public sealed class DisplayBuffStatEffect
    {
        /// <summary>受影响属性。</summary>
        public StatType Type;

        /// <summary>边际贡献（final 全量 − 剔除本来源后的 final）。</summary>
        public float Delta;
    }
}
