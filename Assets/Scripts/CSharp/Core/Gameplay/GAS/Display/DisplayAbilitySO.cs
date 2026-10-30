using System.Collections.Generic;
using UnityEngine;

namespace Framework.Gameplay.Abilities.Display
{
    /// <summary>
    /// 单位信息显示能力配置：一份 SO = 一种显示方案（字段条目列表 + buff 段开关）。
    /// 敌我差异 = 各自一份 SO 条目不同，不做按阵营的代码分派；将来其他对象（守点等）
    /// 复用面板形态 = 新建一份 SO，逻辑零改动。非 sealed——未来显示需求外漏网时
    /// 子类可覆写逃生（基类惯例：能力是可配置的默认实现，不是封闭契约）。
    /// 实例：Assets/Configurations/Abilities/DisplayAbility_Character.asset 等（对接期建）。
    /// </summary>
    [CreateAssetMenu(fileName = "DisplayAbility", menuName = "Framework/Gameplay/Abilities/Display")]
    public class DisplayAbilitySO : AbilityDefinitionSO
    {
        [Header("字段条目（顺序 = 面板行序；敌人/角色差异 = 各自 SO 的条目不同）")]
        [SerializeField] private List<DisplayFieldEntry> fieldEntries = new List<DisplayFieldEntry>();

        [Header("Buff 汇总段（v1 关闸；buff 系统就绪后置 enabled 开）")]
        [SerializeField] private DisplayBuffSectionConfig buffSection = new DisplayBuffSectionConfig();

        /// <summary>字段条目列表（顺序 = 面板行序；只读出口供 Runtime/测试）。</summary>
        public IReadOnlyList<DisplayFieldEntry> FieldEntries => fieldEntries;

        /// <summary>buff 汇总段配置（只读出口）。</summary>
        public DisplayBuffSectionConfig BuffSection => buffSection;

        /// <summary>创建显示能力运行时。</summary>
        /// <returns>使用当前配置的显示能力运行时实例。</returns>
        public override AbilityRuntime CreateRuntime()
        {
            return new DisplayAbilityRuntime(this);
        }
    }
}
