using System;
using Framework.Gameplay.Abilities.Stat;
using UnityEngine;

namespace Framework.Gameplay.Abilities.Display
{
    /// <summary>
    /// 显示字段的数据来源。正交于 <see cref="DisplayStyle"/>——来源决定"取什么值"，
    /// 风格决定"怎么渲染"，两者独立配置（不是行为分派：新组合 = 新 SO 条目，不动代码）。
    /// 数值源（Stat/CurrentHp/MaxHp/HpPercent）产出数值经风格格式化；文本源（其余）原样走 Text 风格。
    /// </summary>
    public enum DisplaySource
    {
        /// <summary>从单位 StatRuntimeData 按 <see cref="DisplayFieldEntry.StatType"/> 取 final 值。</summary>
        Stat,
        /// <summary>单位当前生命（BattleUnitState.Hp）。</summary>
        CurrentHp,
        /// <summary>单位最大生命（Stat final MaxHp，缺失回退单位 Stats.MaxHp）。</summary>
        MaxHp,
        /// <summary>生命比例 0..1（CurrentHp / MaxHp）。</summary>
        HpPercent,
        /// <summary>显示名（异步回填：角色取 UnitConfig.Name，敌人取 EnemyRow.Name；未回填回退 UnitId）。</summary>
        DisplayName,
        /// <summary>单位 ID（CHxxxx / ENxxxx）。</summary>
        UnitId,
        /// <summary>阵营（CHARACTER / ENEMY）。</summary>
        Camp,
        /// <summary>生命状态（ALIVE / DOWNED）。</summary>
        Vital,
    }

    /// <summary>
    /// 显示字段的渲染风格。正交于 <see cref="DisplaySource"/>——新显示种类只需换一组
    /// 条目+风格，Runtime 不做按种类 Switch（基类 virtual 逃生门兜需求外漏网）。
    /// </summary>
    public enum DisplayStyle
    {
        /// <summary>整型（FloorToInt，与快照取整口径一致）。</summary>
        Number,
        /// <summary>一位小数。</summary>
        Float,
        /// <summary>百分数（值 ×100 取整 + %）。</summary>
        Percent,
        /// <summary>原样字符串（文本源专用）。</summary>
        Text,
        /// <summary>比例条填充（0..1，值越界截断）。</summary>
        Bar,
    }

    /// <summary>
    /// 显示方案的一个字段条目：一条 = 面板上的一行（标签 + 值）。
    /// 字段顺序即面板渲染顺序（SO 列表序），敌人/角色差异 = 各自 SO 条目不同，无阵营代码分派。
    /// </summary>
    [Serializable]
    public sealed class DisplayFieldEntry
    {
        [Header("标识")]
        [Tooltip("稳定键（View 槽位映射/测试断言用；不显示）")]
        [SerializeField] private string key;

        [Tooltip("标签（英文，字体缺中文字形）")]
        [SerializeField] private string label;

        [Header("取数与渲染")]
        [Tooltip("数据来源（数值源或文本源）")]
        [SerializeField] private DisplaySource source;

        [Tooltip("来源为 Stat 时目标属性；其余来源忽略")]
        [SerializeField] private StatType statType;

        [Tooltip("渲染风格（数值源常用 Number/Float/Percent/Bar；文本源用 Text）")]
        [SerializeField] private DisplayStyle style;

        /// <summary>稳定键（View 槽位映射/测试断言用；不显示）。</summary>
        public string Key => key;

        /// <summary>标签（英文，字体缺中文字形）。</summary>
        public string Label => label;

        /// <summary>数据来源。</summary>
        public DisplaySource Source => source;

        /// <summary>来源为 Stat 时目标属性；其余来源忽略。</summary>
        public StatType StatType => statType;

        /// <summary>渲染风格。</summary>
        public DisplayStyle Style => style;
    }

    /// <summary>
    /// 面板 buff 汇总段配置。v1 buff 系统未落地（仅裸 StatModifier 带 SourceId），
    /// enabled 默认 false —— 模型恒空，结构先通；真 buff（时长/图标/名称）就绪后置 true 开闸。
    /// 取数口径：StatRuntimeData.Modifiers 按 SourceId 分组 → 每条一组叠层计数 + 各属性边际贡献。
    /// </summary>
    [Serializable]
    public sealed class DisplayBuffSectionConfig
    {
        [Tooltip("是否显示 buff 汇总段（v1 恒 false；buff 系统就绪后置 true）")]
        [SerializeField] private bool enabled;

        [Tooltip("段标题（英文，字体缺中文字形）")]
        [SerializeField] private string headerLabel = "BUFFS";

        [Tooltip("条目渲染风格（v1 文本列表，预留其他）")]
        [SerializeField] private DisplayStyle style = DisplayStyle.Text;

        [Tooltip("最多显示的条目数（0 = 不限制）")]
        [SerializeField] private int maxShown;

        /// <summary>是否显示 buff 汇总段。</summary>
        public bool Enabled => enabled;

        /// <summary>段标题。</summary>
        public string HeaderLabel => headerLabel;

        /// <summary>条目渲染风格。</summary>
        public DisplayStyle Style => style;

        /// <summary>最多显示的条目数（0 = 不限制）。</summary>
        public int MaxShown => maxShown;
    }
}
